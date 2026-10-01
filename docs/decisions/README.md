# Architecture decision records

Each record is short: the situation, what was decided, and what follows from it.

| # | Decision |
|---|---|
| [0001](0001-minimal-apis-layered-solution.md) | Minimal APIs with a layered solution (Evaluation, Domain, Application, Infrastructure) |
| [0002](0002-sha256-bucketing-per-flag-salt.md) | SHA-256 bucketing with a per-flag salt |
| [0003](0003-redis-pubsub-with-snapshot-ttl.md) | Redis pub/sub with a snapshot TTL instead of a transactional outbox |
| [0004](0004-signalr-without-backplane.md) | SignalR without a backplane, WebSockets only with negotiation skipped |
| [0005](0005-targeting-as-json-columns.md) | Targeting stored as JSON columns |
| [0006](0006-gateway-api-envoy-gateway.md) | Gateway API with Envoy Gateway instead of ingress-nginx |
| [0007](0007-ephemeral-preview-environments.md) | Ephemeral per-PR preview environments with in-namespace SQL Server |
| [0008](0008-in-memory-access-token-refresh-cookie.md) | In-memory access token plus a rotating httpOnly refresh cookie |
| [0009](0009-github-oidc-to-azure.md) | GitHub OIDC federation to Azure instead of stored credentials |
| [0010](0010-sdk-keys-public-identifiers.md) | SDK keys hashed at rest and treated as public identifiers |
