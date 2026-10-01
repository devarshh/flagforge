# 0004. SignalR without a backplane, WebSockets only with negotiation skipped

## Context

SDKs keep a live connection to learn about changes. The evaluation API runs several replicas behind a load balancer.
SignalR normally needs a backplane (Redis or Azure SignalR) so any server can reach any client, and its negotiate step
followed by a separate connection needs sticky sessions.

## Decision

- Every evaluation pod subscribes to Redis itself and, on a change, sends `FlagsChanged` only to its **own** connections
  in the group `env:{environmentId}`. Since every pod hears every message, no backplane is needed.
- The hub accepts **WebSockets only**, and clients skip negotiation (`skipNegotiation: true`), so a connection is a single
  upgraded request that any pod can accept. No sticky sessions.
- The message carries only `{ environmentVersion }`; clients fetch values over HTTP, so the hub never sends configuration.

## Consequences

- Scaling the evaluation API is just adding pods; the HPA can scale on CPU.
- Browsers or proxies that cannot do WebSockets fall back to polling in the SDK (every 30 seconds), and the SDK keeps
  retrying the live connection.
- SignalR's 15-second keepalive keeps idle connections open through Envoy's idle timeouts (checked for 6.5 minutes
  through the kind gateway).
- Server-sent events would work too; SignalR was chosen for its reconnect handling and .NET integration.
