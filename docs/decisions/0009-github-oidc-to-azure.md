# 0009. GitHub OIDC federation to Azure instead of stored credentials

## Context

CD and preview workflows push images to Azure Container Registry and deploy to AKS. A client secret stored in GitHub
would be long-lived, need rotation, and work from anywhere if leaked.

## Decision

A user-assigned managed identity with **federated credentials** trusts GitHub's OIDC issuer for exactly three subjects:
pushes to `main`, pull requests, and the `production` environment. Workflows sign in with `azure/login` and
`id-token: write`. The identity's roles are each scoped to one resource: AcrPush, AcrDelete, and Reader on the
registry, and the AKS Cluster User Role on the cluster. The kubelet identity pulls images with AcrPull, so no image pull
secrets exist.

## Consequences

- No Azure credential is stored in GitHub; tokens are short-lived and issued per job.
- A token only works for this repository and those three contexts; for example, a workflow run from a fork cannot sign in.
- The subjects are part of the infrastructure: renaming the repository or adding another deploying environment means
  redeploying the Bicep.
- Until the Azure variables are set, the Azure workflows skip with a notice instead of failing.
