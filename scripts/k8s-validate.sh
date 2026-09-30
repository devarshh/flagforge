#!/usr/bin/env bash
# Renders every kustomization under deploy/k8s and validates the output with kubeconform (PROJECT_SPEC §20.6):
# the local, preview, and aks overlays, the migrator Job, and both platforms. The preview overlay is also rendered
# the way the preview workflow deploys it, with a sample namespace and hostname.
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

# A preview as the preview workflow renders it, with a sample namespace and hostname.
sample="$(scripts/k8s-generate-overlay.sh preview preview-sample --namespace ff-pr-0 --hostname pr-0.203.0.113.10.nip.io)"

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
