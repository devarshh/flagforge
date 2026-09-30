#!/usr/bin/env bash
# Renders every kustomization under deploy/k8s and validates the output with kubeconform (PROJECT_SPEC §20.6):
# the local, preview, and aks overlays, the migrator Job, and both platforms. The preview overlay is also rendered
# the way the preview workflow deploys it, with a sample namespace, hostname, and image tags.
#
# Usage: scripts/k8s-validate.sh
# Needs kubectl (for `kubectl kustomize`) and kubeconform (on PATH or in ./.tools). kubeconform downloads schemas,
# including CRD schemas for Gateway API and Envoy Gateway resources from the datreeio CRDs catalog.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

kubeconform="$(command -v kubeconform || true)"
if [[ -z "$kubeconform" && -x .tools/kubeconform ]]; then
  kubeconform="$repo_root/.tools/kubeconform"
fi

if [[ -z "$kubeconform" ]]; then
  echo "kubeconform is required: https://github.com/yannh/kubeconform#installation" >&2
  exit 1
fi

if ! command -v kubectl > /dev/null 2>&1; then
  echo "kubectl is required: https://kubernetes.io/docs/tasks/tools/" >&2
  exit 1
fi

# A preview as the workflow renders it: the overlay wrapped with a namespace, hostname, and image tags.
sample="deploy/k8s/overlays/.generated/preview-sample"
mkdir -p "$sample"
cat > "$sample/kustomization.yaml" <<'YAML'
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: ff-pr-0
resources:
  - ../../preview
images:
  - name: flagforge/management-api
    newName: flagforgeacr.azurecr.io/flagforge/management-api
    newTag: pr-0-0000000
  - name: flagforge/evaluation-api
    newName: flagforgeacr.azurecr.io/flagforge/evaluation-api
    newTag: pr-0-0000000
  - name: flagforge/worker
    newName: flagforgeacr.azurecr.io/flagforge/worker
    newTag: pr-0-0000000
  - name: flagforge/dashboard
    newName: flagforgeacr.azurecr.io/flagforge/dashboard
    newTag: pr-0-0000000
  - name: flagforge/demo
    newName: flagforgeacr.azurecr.io/flagforge/demo
    newTag: pr-0-0000000
patches:
  - target:
      kind: HTTPRoute
      name: flagforge
    patch: |-
      - op: add
        path: /spec/hostnames
        value: ["pr-0.203.0.113.10.nip.io"]
YAML

directories=(
  deploy/k8s/overlays/local
  deploy/k8s/overlays/preview
  "$sample"
  deploy/k8s/overlays/aks
  deploy/k8s/jobs/migrator
  deploy/k8s/platform/local
  deploy/k8s/platform/aks
)

failed=0
for directory in "${directories[@]}"; do
  echo "== $directory"
  if ! kubectl kustomize "$directory" | "$kubeconform" -strict -summary -ignore-missing-schemas \
    -schema-location default \
    -schema-location 'https://raw.githubusercontent.com/datreeio/CRDs-catalog/main/{{.Group}}/{{.ResourceKind}}_{{.ResourceAPIVersion}}.json'; then
    failed=1
  fi
done

if [[ $failed -ne 0 ]]; then
  echo "Some manifests are invalid." >&2
  exit 1
fi

echo "All kustomizations are valid."
