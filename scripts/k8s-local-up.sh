#!/usr/bin/env bash
# Runs FlagForge on a local kind cluster, the same way it runs on AKS: Envoy Gateway, the platform Gateway, the
# local overlay (with in-cluster SQL Server), and the migrator Job.
#
# Usage: scripts/k8s-local-up.sh [--smoke]
#   --smoke   run the smoke test (full mode) against http://localhost:8090 when everything is up
#
# Safe to run again: it reuses the cluster, upgrades Envoy Gateway, rebuilds and reloads the images, and reruns the
# migrator. Secrets come from .env when it exists, otherwise from the development defaults.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

cluster="flagforge"
context="kind-$cluster"
namespace="flagforge"
envoy_gateway_version="1.9.2"
base_url="http://localhost:8090"
images=(management-api evaluation-api worker migrator dashboard demo)
sql_image="mcr.microsoft.com/mssql/server:2022-latest"

smoke=false
case "${1:-}" in
  "") ;;
  --smoke) smoke=true ;;
  *)
    echo "usage: $0 [--smoke]" >&2
    exit 2
    ;;
esac

step() { printf '\n==> %s\n' "$*"; }

# 1. Tools.
missing=0
require() {
  if ! command -v "$1" > /dev/null 2>&1; then
    echo "$1 is required: $2" >&2
    missing=1
  fi
}
require docker "https://docs.docker.com/get-docker/"
require kind "https://kind.sigs.k8s.io/docs/user/quick-start/#installation"
require kubectl "https://kubernetes.io/docs/tasks/tools/"
require helm "https://helm.sh/docs/intro/install/"
if $smoke; then
  require npm "https://nodejs.org/ (Node.js 24)"
fi
if [[ $missing -ne 0 ]]; then
  exit 1
fi

k() { kubectl --context "$context" "$@"; }

# 2. Cluster.
if kind get clusters 2> /dev/null | grep -qx "$cluster"; then
  step "Using the existing kind cluster $cluster"
else
  step "Creating kind cluster $cluster"
  kind create cluster --config scripts/kind-config.yaml
fi

# 3. Envoy Gateway. The chart also installs the Gateway API CRDs.
step "Installing Envoy Gateway $envoy_gateway_version"
helm upgrade --install eg oci://docker.io/envoyproxy/gateway-helm --version "$envoy_gateway_version" \
  --namespace envoy-gateway-system --create-namespace --kube-context "$context" --wait --timeout 5m
k wait --namespace envoy-gateway-system --for=condition=Available deployment/envoy-gateway --timeout=5m

# 4. Platform: GatewayClass, Gateway, and the NodePort EnvoyProxy.
step "Applying the local platform (Gateway on NodePort 30080)"
k apply -k deploy/k8s/platform/local
k wait --namespace flagforge-system --for=condition=Programmed gateway/flagforge-gateway --timeout=5m
k wait --namespace envoy-gateway-system --for=condition=Ready pod \
  --selector gateway.envoyproxy.io/owning-gateway-name=flagforge-gateway --timeout=5m

# 5. Images: build with the tag "local" and load them into the cluster's node.
build_image() {
  local image="$1" dockerfile context="."
  case "$image" in
    management-api) dockerfile="src/backend/FlagForge.ManagementApi/Dockerfile" ;;
    evaluation-api) dockerfile="src/backend/FlagForge.EvaluationApi/Dockerfile" ;;
    worker) dockerfile="src/backend/FlagForge.Worker/Dockerfile" ;;
    migrator) dockerfile="src/backend/FlagForge.Migrator/Dockerfile" ;;
    dashboard | demo)
      dockerfile="src/frontend/apps/$image/Dockerfile"
      context="src/frontend"
      ;;
  esac
  docker build -f "$dockerfile" -t "flagforge/$image:local" "$context"
}

for image in "${images[@]}"; do
  step "Building flagforge/$image:local"
  build_image "$image"
  kind load docker-image --name "$cluster" "flagforge/$image:local"
done

# SQL Server publishes only amd64 images. Pulling on the host and loading the image lets the node use it even when
# the node is arm64 (Apple silicon), where it runs under emulation.
step "Loading $sql_image into the cluster"
docker pull --platform linux/amd64 "$sql_image"
kind load docker-image --name "$cluster" "$sql_image"

# 6. Namespace and Secret. Values from .env when present, else the development defaults (the same as Compose).
step "Creating namespace $namespace and Secret flagforge-secrets"
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi
sa_password="${MSSQL_SA_PASSWORD:-FlagForge_Dev_Sql_2026}"
k create namespace "$namespace" --dry-run=client -o yaml | k apply -f -
k create secret generic flagforge-secrets --namespace "$namespace" \
  --from-literal=sql-connection-string="Server=sqlserver,1433;Database=flagforge;User Id=sa;Password=$sa_password;TrustServerCertificate=True" \
  --from-literal=mssql-sa-password="$sa_password" \
  --from-literal=jwt-signing-key="${JWT_SIGNING_KEY:-dev-only-jwt-signing-key-replace-me-0123456789abcdef}" \
  --from-literal=seed-admin-password="${FF_SEED_ADMIN_PASSWORD:-FlagForge!2026}" \
  --from-literal=demo-user-password="${FF_SEED_DEMO_USER_PASSWORD:-FlagForge!2026}" \
  --from-literal=demo-sdk-key="${DEMO_SDK_KEY:-${FF_SEED_DEMO_SDK_KEY:-ffk_local_demo_key_for_development_only_000}}" \
  --dry-run=client -o yaml | k apply -f -

# 7. Workloads, then SQL Server, the migrator Job, and every rollout.
step "Applying the local overlay"
rerun=false
if k get deployment management-api --namespace "$namespace" > /dev/null 2>&1; then
  rerun=true
fi
k apply -k deploy/k8s/overlays/local
if $rerun; then
  # Images keep the tag "local", so a rerun would not roll out new builds (or a changed Secret) on its own.
  k rollout restart deployment --namespace "$namespace"
fi

if $rerun; then
  # A StatefulSet does not replace a pod that never became ready, even after its template is fixed (Kubernetes
  # "forced rollback"), so delete a pod that is not ready once the controller has seen the new spec. SQL Server's
  # data is disposable here: the migrator seeds it again.
  generation="$(k get statefulset sqlserver --namespace "$namespace" -o jsonpath='{.metadata.generation}')"
  until [[ "$(k get statefulset sqlserver --namespace "$namespace" -o jsonpath='{.status.observedGeneration}')" -ge "$generation" ]]; do
    sleep 1
  done
  if [[ "$(k get pod sqlserver-0 --namespace "$namespace" -o jsonpath='{.status.containerStatuses[0].ready}' 2> /dev/null)" != "true" ]]; then
    k delete pod sqlserver-0 --namespace "$namespace" --ignore-not-found --wait=false
  fi
fi

step "Waiting for SQL Server"
k rollout status statefulset/sqlserver --namespace "$namespace" --timeout=10m

step "Running the migrator"
KUBECTL_CONTEXT="$context" scripts/k8s-run-migrator.sh "$namespace"

step "Waiting for the deployments"
for deployment in redis management-api evaluation-api worker dashboard demo; do
  k rollout status "deployment/$deployment" --namespace "$namespace" --timeout=5m
done

# 8. Done.
printf '\nFlagForge is running on kind:\n  Dashboard  %s\n  Demo       %s/demo/\n' "$base_url" "$base_url"

if $smoke; then
  step "Running the smoke test against $base_url"
  (
    cd tests/smoke
    [[ -d node_modules ]] || npm ci --no-audit --no-fund
    BASE_URL="$base_url" SMOKE_MODE=full npm run smoke
  )
fi
