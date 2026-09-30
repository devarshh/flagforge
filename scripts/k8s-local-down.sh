#!/usr/bin/env bash
# Deletes the local kind cluster created by scripts/k8s-local-up.sh, with everything in it.
set -euo pipefail

cluster="flagforge"
if ! command -v kind > /dev/null 2>&1; then
  echo "kind is not installed, so there is no cluster to delete." >&2
  exit 1
fi

if kind get clusters 2> /dev/null | grep -qx "$cluster"; then
  kind delete cluster --name "$cluster"
else
  echo "No kind cluster named $cluster."
fi
