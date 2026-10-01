# 0002. SHA-256 bucketing with a per-flag salt

## Context

Percentage rollouts must give a context the same variation every time (on any server, and in any future SDK that
evaluates locally), must not correlate across flags (being in the first 10% of one flag should say nothing about
another), and must let a rollout grow from 10% to 100% without moving anyone who already has the new variation.

## Decision

`bucket = BigEndianUInt32(SHA256(UTF8("{flagKey}.{salt}.{bucketValue}"))[0..4]) % 100000`, where `salt` is 16 random
hex characters created with the flag and never changed. Weights are integers in thousandths of a percent (100000 is
100%) and are always stored in the flag's variation order, with boolean flags ordered `true` first. A context without
a usable bucket value gets bucket 0.

## Consequences

- Deterministic and portable: SHA-256 exists in every language, so a .NET, JavaScript, or Go SDK can reproduce the
  exact bucket. Numbers are formatted in invariant culture without trailing zeros so `31` and `31.0` agree.
- The salt makes flags independent even when their keys are similar, and a flag can be "reshuffled" only by creating a
  new flag, which is explicit.
- Variation-ordered weights make ramp-ups monotonic; tests check this along with determinism, independence, and a
  distribution within 1.5 points over 20,000 keys.
- SHA-256 is slower than a non-cryptographic hash such as MurmurHash, but at 32 bytes of input per evaluation the
  difference is negligible next to network time, and it avoids a dependency.
