# 0006. Gateway API with Envoy Gateway instead of ingress-nginx

## Context

The cluster needs one public entry point with path routing and working WebSockets. The Ingress API is frozen, and
ingress-nginx is being retired. The same routing should run locally on kind and on AKS.

## Decision

Use the Kubernetes **Gateway API** with **Envoy Gateway** as the implementation, installed with its Helm chart (pinned),
which also installs the Gateway API CRDs. A GatewayClass `envoy` and a Gateway `flagforge-gateway` (one HTTP listener,
routes allowed from all namespaces) are applied once per cluster from `deploy/k8s/platform/`. Each FlagForge namespace
attaches an HTTPRoute with `PathPrefix` rules for `/api`, `/sdk`, `/demo`, and `/`. On kind an `EnvoyProxy` resource
makes the proxy a NodePort (30080, mapped to host port 8090); on AKS it gets a public LoadBalancer IP.

## Consequences

- Routing is the same in both clusters and matches the Compose gateway, with no path rewriting anywhere.
- Previews reuse the one Gateway: each preview namespace adds its own HTTPRoute with its own hostname.
- WebSocket upgrades work with Envoy Gateway's defaults.
- Envoy Gateway is another controller to run and upgrade (the bootstrap workflow does it), and some features are still
  in the experimental channel; FlagForge uses only stable resources.
