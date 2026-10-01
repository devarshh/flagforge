# 0003. Redis pub/sub with a snapshot TTL instead of a transactional outbox

## Context

After a change is committed, every evaluation pod must drop its cached snapshot for that environment and tell its SDK
clients. The usual way to publish reliably after a database write is a transactional outbox, which needs an outbox
table, a relay process, and cleanup.

## Decision

Publish `{ environmentId, configVersion }` to the Redis channel `flagforge:config-changed` **after** the transaction
commits. A failed publish is logged as a warning and not reported to the user. Two safety nets bound how long a missed
message can matter: snapshots expire after 60 seconds (configurable), and a pod whose Redis subscription reconnects
evicts every snapshot and tells its own clients to refetch.

## Consequences

- Simple and fast: one publish per change, no extra table or relay, and Redis holds no state (it can restart freely).
- The worst case is bounded staleness: a lost message delays a change by at most the snapshot TTL, never forever.
- Every change is still durable (it is committed before publishing) and every change is audited.
- If exactly-once, ordered delivery to downstream systems were ever needed (for example webhooks), an outbox would be
  the right tool; flag delivery only needs "eventually, and usually within a second".
