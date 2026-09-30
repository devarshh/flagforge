# Local Kubernetes with kind

`scripts/k8s-local-up.sh` runs FlagForge on a one-node [kind](https://kind.sigs.k8s.io/) cluster the same way it runs
on AKS: Envoy Gateway serves the Gateway API, one HTTPRoute sends `/api`, `/sdk`, `/demo`, and `/` to the services,
and a migrator Job prepares the database before the apps become ready. The only local differences come from the
`local` overlay: SQL Server runs inside the namespace, every Deployment has one replica, and there is no autoscaler.

## Prerequisites

| Tool | Notes |
|---|---|
| Docker | Give it at least 8 GB of memory; SQL Server alone asks for 2 GB. |
| kind, kubectl, helm | Any recent version. The script checks for them and prints install links. |
| Node.js 24 | Only for `--smoke`, which runs `tests/smoke`. |

SQL Server publishes only amd64 images. On Apple silicon, turn on **Use Rosetta for x86_64/amd64 emulation** in
Docker Desktop (Settings, General). The script pulls the amd64 image on the host and loads it into the kind node,
where it runs under emulation.

## Start and stop

```sh
scripts/k8s-local-up.sh            # or: make k8s-up
scripts/k8s-local-up.sh --smoke    # also runs the full smoke test against the cluster
scripts/k8s-local-down.sh          # or: make k8s-down (deletes the cluster and everything in it)
```

When it finishes:

- Dashboard: <http://localhost:8090> (sign in with the seeded admin, `admin@flagforge.local` / `FlagForge!2026`
  unless `.env` says otherwise)
- Demo store: <http://localhost:8090/demo/>

On a machine that has never built the images, the first run takes up to about ten minutes, mostly image builds; with
Docker's build cache warm, a run from scratch (new cluster included) takes about two minutes. Running the script again
is safe: it keeps the cluster, rebuilds and reloads the images, restarts the Deployments so they use them, and runs
the migrator again.

## What the script does

1. Checks for `docker`, `kind`, `kubectl`, and `helm`.
2. Creates the cluster `flagforge` from `scripts/kind-config.yaml` if it does not exist. The node publishes port
   30080 (the Envoy NodePort) as `127.0.0.1:8090` on your machine.
3. Installs Envoy Gateway with Helm (`oci://docker.io/envoyproxy/gateway-helm`, pinned to 1.9.2) into
   `envoy-gateway-system`. The chart also installs the Gateway API CRDs (experimental channel, bundle v1.6.1), so no
   separate CRD step is needed.
4. Applies `deploy/k8s/platform/local`: the `envoy` GatewayClass, the `flagforge-gateway` Gateway in
   `flagforge-system`, and an `EnvoyProxy` that makes Envoy's Service a NodePort on 30080. It waits until the Gateway
   is programmed and the Envoy proxy pod is ready.
5. Builds every image with the tag `local` and loads it into the node with `kind load docker-image`. The `local`
   tag keeps the default `imagePullPolicy` at `IfNotPresent`, so the node never tries a registry.
6. Creates the namespace `flagforge` and the Secret `flagforge-secrets`, from `.env` when it exists and otherwise from
   the same development defaults Compose uses. The connection string points at the in-cluster `sqlserver` Service.
7. Applies `deploy/k8s/overlays/local`, waits for SQL Server, runs the migrator Job (printing its logs if it fails),
   and waits for every Deployment. The APIs' startup probes wait on `/health/ready`, which stays unready until the
   migrations are applied.
8. Prints the URLs, and with `--smoke` runs the smoke test in full mode against `http://localhost:8090`.

## Secret keys

| Key | Used by |
|---|---|
| `sql-connection-string` | APIs, worker, migrator |
| `mssql-sa-password` | SQL Server (in-cluster SQL only) |
| `jwt-signing-key` | management API |
| `seed-admin-password`, `demo-user-password` | migrator |
| `demo-sdk-key` | migrator (seeds the key) and demo (serves it as `/demo/config.json`) |

## Exploring the deployment

```sh
kubectl config use-context kind-flagforge

kubectl get pods -n flagforge                      # every workload
kubectl get gateway,httproute -A                   # routing: the Gateway and the flagforge HTTPRoute
kubectl get pods -n envoy-gateway-system           # the Envoy Gateway controller and the proxy
kubectl logs -n flagforge deploy/evaluation-api -f # JSON logs
kubectl logs -n flagforge job/flagforge-migrator   # the last migration run (kept for an hour)

# Scale the evaluation API and watch live updates keep working: every pod subscribes to Redis on its own,
# so SignalR needs no backplane.
kubectl scale -n flagforge deploy/evaluation-api --replicas=3

# Health endpoints are not routed through the gateway; reach them through a port-forward.
kubectl port-forward -n flagforge deploy/management-api 8081:8080
curl http://localhost:8081/health/ready

# Query the database.
kubectl exec -n flagforge sqlserver-0 -- /bin/sh -c \
  '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d flagforge -Q "SELECT [Key] FROM Flags"'
```

Validate every kustomization without a cluster:

```sh
scripts/k8s-validate.sh
```

## Streaming through Envoy

WebSocket upgrades pass through Envoy Gateway with its default settings, so `/sdk/hubs/flags` needs no extra
policy. SignalR's 15-second keepalive keeps an otherwise quiet connection active: a connection held open and idle
for six and a half minutes through the kind gateway (past Envoy's five-minute stream idle timeout) stayed connected
and still received `FlagsChanged`.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `sqlserver-0` restarts with `sqlservr: Operation not permitted` | The container must keep `NET_BIND_SERVICE` in its bounding set (the manifest adds it back after dropping all capabilities). |
| `sqlserver-0` exits quickly or never gets ready on Apple silicon | Turn on Rosetta emulation in Docker Desktop, and give Docker at least 8 GB of memory. |
| A fixed SQL Server manifest does not take effect | A StatefulSet does not replace a pod that never became ready. The script deletes such a pod on reruns; by hand: `kubectl delete pod -n flagforge sqlserver-0`. |
| Port 8090 is already in use | Stop whatever listens on it, or change `hostPort` in `scripts/kind-config.yaml` and use that port instead. |
| Pods keep old code | Rerun the script: it rebuilds, reloads, and restarts the Deployments. |
| The APIs are not ready | Check the migrator: `kubectl logs -n flagforge job/flagforge-migrator`. |
