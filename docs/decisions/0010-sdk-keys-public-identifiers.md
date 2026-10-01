# 0010. SDK keys hashed at rest and treated as public identifiers

## Context

Browser SDKs must send a key to the evaluation API, so the key ends up in the page's JavaScript where anyone can read
it. It still identifies an environment and should be revocable, and a database leak should not hand out working keys.

## Decision

- An SDK key is `ffk_` plus 32 random bytes (base64url). Only its SHA-256 hash and a short display prefix are stored;
  the plaintext is shown once, at creation.
- A key only grants **evaluation**: responses contain served values and reasons, never rules, target lists, or other
  configuration. An integration test asserts this.
- Keys are looked up by hash through a 60-second cache; revoking a key publishes a message that clears the caches, so it
  stops working within seconds. Plaintext keys are never logged, and hub URLs (which carry the key as `access_token`) are
  kept out of request logs.
- Per-key rate limits bound what anyone holding a key can do.

## Consequences

- Leaking a key reveals flag values for contexts the holder makes up, which the app shows its users anyway; it reveals
  no targeting logic, and it can be revoked.
- Server-side SDKs that need to hide targeting logic, or evaluate locally, would need a different, secret key type;
  that is out of scope.
- A database dump does not contain usable keys.
