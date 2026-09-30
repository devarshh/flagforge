#!/usr/bin/env bash
# Runs the migrator Job in a namespace and waits for it. Jobs are immutable, so it deletes the previous Job, applies
# deploy/k8s/jobs/migrator (set its image first with `kustomize edit set image` when not using the local tag), and
# waits until the Job succeeds or fails. On failure, or after the timeout, it prints the Job's logs and exits 1.
#
# Usage: scripts/k8s-run-migrator.sh <namespace> [timeout-seconds]   (default timeout: 600)
# Uses the current kubectl context, or KUBECTL_CONTEXT when it is set.
set -euo pipefail

namespace="${1:?usage: $0 <namespace> [timeout-seconds]}"
timeout="${2:-600}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

k() {
  if [[ -n "${KUBECTL_CONTEXT:-}" ]]; then
    kubectl --context "$KUBECTL_CONTEXT" --namespace "$namespace" "$@"
  else
    kubectl --namespace "$namespace" "$@"
  fi
}

k delete job flagforge-migrator --ignore-not-found --wait=true
k apply -k deploy/k8s/jobs/migrator

deadline=$((SECONDS + timeout))
while true; do
  succeeded="$(k get job flagforge-migrator -o jsonpath='{.status.succeeded}')"
  failed="$(k get job flagforge-migrator -o jsonpath='{.status.conditions[?(@.type=="Failed")].status}')"
  if [[ "$succeeded" == "1" ]]; then
    break
  fi

  if [[ "$failed" == "True" || $SECONDS -gt $deadline ]]; then
    echo "The migrator Job did not complete. Its logs:" >&2
    k logs job/flagforge-migrator --all-containers --tail=200 >&2 || true
    exit 1
  fi

  sleep 5
done

k logs job/flagforge-migrator --tail=20
