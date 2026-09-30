# Deploying to Azure

FlagForge's CD workflow deploys every push to `main` to an AKS cluster, and every pull request from this repository
gets its own preview namespace. This guide sets that up from an empty Azure subscription. Until it is done, the Azure
workflows skip themselves with a notice, so the repository still shows green checks.

## What gets created

`infra/main.bicep` creates, in one resource group:

| Resource | Details |
|---|---|
| Container registry | Basic tier, admin user disabled. AKS pulls with its kubelet identity (`AcrPull`). |
| AKS cluster | Free tier, one system node pool (`Standard_B2ms`, autoscaling 1 to 3 nodes), Azure CNI Overlay, OIDC issuer and workload identity enabled. |
| Azure SQL | A logical server (SQL authentication, TLS 1.2, "allow Azure services") and the database `flagforge`: serverless General Purpose, 0.5 to 2 vCores, auto-pause after 60 minutes, on the Azure SQL Database free offer. |
| GitHub identity | A user-assigned managed identity that GitHub Actions signs in as through OpenID Connect (no stored credentials). |

The GitHub identity trusts three federated subjects: pushes to `main`, pull requests, and the `production`
environment. Its roles, each scoped to a single resource:

| Role | Scope | Why |
|---|---|---|
| AcrPush | Registry | Push images. |
| AcrDelete | Registry | Delete a closed pull request's image tags. |
| Reader | Registry | `az acr login` and `az acr repository` look the registry up through Azure Resource Manager; the data-plane roles do not allow that. |
| Azure Kubernetes Service Cluster User Role | Cluster | `az aks get-credentials`. The cluster uses Kubernetes RBAC with local accounts, so the returned kubeconfig can deploy. |

## Costs

The **AKS node VM is the main cost**: a `Standard_B2ms` VM runs around the clock while the cluster is up, plus its OS
disk and the public IP of the gateway's load balancer. The AKS control plane (Free tier) costs nothing, the Basic
registry costs a few dollars a month, and the database uses the free offer (a monthly allowance of vCore seconds and 32
GB of storage; when the allowance runs out, the database pauses until the next month instead of billing). Check the
[Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/) for your region.

To pay for less:

```sh
az aks stop --resource-group flagforge-rg --name flagforge-aks    # stops the nodes; the cluster keeps its config
az aks start --resource-group flagforge-rg --name flagforge-aks
az group delete --name flagforge-rg --yes --no-wait                # deletes everything when you are finished
```

Workflows fail while the cluster is stopped; start it before pushing.

A subscription has **one** free-offer database. If yours is taken, deploy with `SQL_USE_FREE_LIMIT=false`, which bills
the serverless database by use (it still pauses when idle).

## Prerequisites

- An Azure subscription where you can create resource groups and role assignments (Owner, or Contributor plus User
  Access Administrator).
- The Azure CLI (2.60 or later), signed in: `az login`, then `az account set --subscription <id>` if you have several.
- The GitHub CLI (`gh auth login`) with admin access to your copy of this repository.

## 1. Deploy the infrastructure

```sh
export SQL_ADMIN_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')-Aa1"   # keep this shell open
scripts/azure-deploy-infra.sh
```

The script creates the resource group (`RESOURCE_GROUP`, default `flagforge-rg`, in `LOCATION`, default `eastus`),
deploys `infra/main.bicep`, and prints the commands for the next step. It reads the repository name from the `origin`
remote; set `GITHUB_REPOSITORY=owner/name` if that does not work. Deploying takes about ten minutes, mostly for AKS.

## 2. Connect the repository

Run the printed commands in the same shell. They set these repository **variables**:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | The GitHub identity and where it lives. |
| `AZURE_RESOURCE_GROUP` | The resource group. |
| `ACR_NAME`, `AKS_NAME` | The registry and cluster names. |

and these **secrets**:

| Secret | Value |
|---|---|
| `SQL_CONNECTION_STRING` | The Azure SQL connection string (built from the deployment outputs and `SQL_ADMIN_PASSWORD`). |
| `JWT_SIGNING_KEY` | A random signing key for dashboard sessions. |
| `SEED_ADMIN_PASSWORD` | The password of the seeded admin (`admin@flagforge.local`). You are prompted for it. |
| `DEMO_USER_PASSWORD` | The password of the seeded editor and viewer. You are prompted for it. |
| `DEMO_SDK_KEY` | A random `ffk_` key; the demo store uses it. |

Passwords need at least 10 characters.

## 3. Create the production environment

In the repository settings, open **Environments** and create `production`. Adding required reviewers there makes every
deployment wait for an approval; the CD workflow's deploy job runs in this environment.

## 4. Bootstrap the cluster

Run the **bootstrap-cluster** workflow (Actions, bootstrap-cluster, Run workflow). It installs Envoy Gateway with
Helm, applies `deploy/k8s/platform/aks`, and waits for the Gateway's public IP, which it writes to the job summary
together with the hostname `flagforge.<ip>.nip.io`. Run it once per cluster, and again to upgrade Envoy Gateway.

## 5. Deploy

Push to `main` (or run the **cd** workflow by hand). CD runs CI, builds and pushes the images tagged `sha-<commit>`,
runs the migrator Job, applies `deploy/k8s/overlays/aks`, waits for every rollout (rolling back a Deployment that
fails), and runs the smoke test in read-only mode. The job summary and the `production` environment link to the app:

- Dashboard: `http://flagforge.<gateway ip>.nip.io`
- Demo store: `http://flagforge.<gateway ip>.nip.io/demo/`

Sign in with `admin@flagforge.local` and `SEED_ADMIN_PASSWORD`.

## Pull request previews

A pull request from a branch of this repository deploys to the namespace `ff-pr-<number>` with its own in-cluster SQL
Server, at `http://pr-<number>.<gateway ip>.nip.io`, and runs the full smoke test. A sticky comment on the pull request
links to it. Closing the pull request deletes the namespace and the preview's image tags, and the daily
**preview-janitor** workflow removes previews whose pull request is no longer open. Pull requests from forks get no
preview, because they cannot use the repository's Azure credentials.

## HTTPS

The gateway serves plain HTTP (nip.io hostnames have no certificate), so the AKS overlay sets
`Auth__RefreshCookieSecure=false`. To serve HTTPS, give the Gateway an HTTPS listener with a certificate (for example
from cert-manager and a domain you own), point the workflows at that hostname, and remove the `Auth__RefreshCookieSecure`
patch from `deploy/k8s/overlays/aks/kustomization.yaml` so the refresh cookie requires HTTPS again.

## Troubleshooting

| Problem | Fix |
|---|---|
| `AADSTS700213` or `AADSTS70021` in `azure/login` | The workflow's subject does not match a federated credential. Check that `GITHUB_REPOSITORY` was right when you deployed, and that production deployments use the `production` environment. Redeploy the infrastructure after renaming the repository. |
| CD says the Gateway has no address | Run the bootstrap-cluster workflow and wait for it to finish. |
| The first request after a quiet period is slow or times out | The serverless database was paused; it resumes on the first connection (up to a minute), and EF Core retries transient failures. In CD, the migrator Job and the readiness probes wake it before the smoke test runs. |
| The database stays paused | The monthly free allowance is used up. It resumes next month, or set `SQL_USE_FREE_LIMIT=false` and deploy again. |
| Deploying the database fails because of the free offer | Another database in the subscription already uses it: set `SQL_USE_FREE_LIMIT=false` and deploy again. |
| Pods cannot pull images | Check the `AcrPull` assignment for the cluster's kubelet identity on the registry (created by `infra/modules/aks.bicep`). |
| The VM size is not available | Some regions or subscriptions lack `Standard_B2ms`; set `aksNodeVmSize` in `infra/main.bicepparam` to another size with at least 2 vCPUs and 8 GB. |
| Workflows fail after `az aks stop` | Start the cluster again with `az aks start`. |
