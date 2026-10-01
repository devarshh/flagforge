# Evaluation semantics

This is the contract of `FlagForge.Evaluation`, the pure library that decides which variation a context receives. The
evaluation API uses it for SDK requests, and the management API uses it for the dashboard's test panel. It has no I/O
and no dependencies, and it is deterministic: the same flag, configuration, and context always give the same result.
An SDK that evaluates locally must reproduce these rules exactly.

## Inputs

### Evaluation context

```json
{
  "key": "user-123",
  "attributes": {
    "email": "sam@acme.com",
    "plan": "premium",
    "age": 31,
    "beta": true,
    "groups": ["staff", "qa"]
  }
}
```

- `key` is required: a string of 1 to 256 characters. Clauses address it as the attribute `key`.
- `attributes` is optional, with at most 50 entries. Names match `^[A-Za-z_][A-Za-z0-9_.-]{0,63}$`, and `key` is
  reserved. A value is a string (at most 1024 characters), a number, a boolean, or an array of at most 100 of those.
- A `null` attribute is accepted and treated as missing. Arrays may not contain `null`. Duplicate attribute names are
  rejected, and unknown top-level properties are ignored.
- Numbers use invariant-culture decimal syntax: a sign, a decimal point, and an exponent are allowed; thousands
  separators are not (`"1,000"` is not a number).

### Targeting configuration

One per flag per environment:

```json
{
  "enabled": true,
  "offVariationId": "false",
  "targets": [{ "variationId": "true", "contextKeys": ["alice", "qa-bot"] }],
  "rules": [
    {
      "id": "r_7f3a2c",
      "description": "Internal staff",
      "clauses": [{ "attribute": "email", "operator": "endsWith", "values": ["@acme.com"], "negate": false }],
      "serve": { "variationId": "true" }
    },
    {
      "id": "r_91bc04",
      "description": "Gradual rollout for paid plans",
      "clauses": [{ "attribute": "plan", "operator": "in", "values": ["premium", "enterprise"], "negate": false }],
      "serve": {
        "rollout": {
          "bucketBy": "key",
          "weights": [
            { "variationId": "true", "weight": 25000 },
            { "variationId": "false", "weight": 75000 }
          ]
        }
      }
    }
  ],
  "fallthrough": { "variationId": "false" }
}
```

A **serve** is exactly one of `{ "variationId": "..." }` or `{ "rollout": { "bucketBy": "...", "weights": [...] } }`.
Weights are thousandths of a percent: `100000` is 100%, and `12500` is 12.5%.

## Algorithm

```
evaluate(flag, config, context):
  if the flag is missing or archived:
      return no value, reason FLAG_NOT_FOUND           # the SDK returns the caller's default
  if not config.enabled:
      return variation(config.offVariationId), reason OFF
  for each target list, in order:
      if context.key is in the list:
          return variation(target.variationId), reason TARGET_MATCH
  for each rule, in order (index from 0):
      if every clause of the rule matches:
          return resolve(rule.serve), reason RULE_MATCH { ruleId, ruleIndex, inRollout }
  return resolve(config.fallthrough), reason FALLTHROUGH { inRollout }
```

The order is the precedence: a disabled flag ignores everything else, individual targets beat rules, the first
matching rule wins, and the default rule (fallthrough) applies only when nothing else matched.

If a configuration refers to a variation that does not exist (validation prevents this), the result has no value and
the reason `ERROR`, and the SDK returns the caller's default.

## Clause matching

```
clauseMatches(clause, context):
  value = the attribute: "key" is context.key; any other name is looked up case-sensitively
  if clause.operator is "exists":
      matched = value is present and not null
      return clause.negate ? not matched : matched
  if value is missing or null:
      return false                     # a missing attribute never matches, even when negated
  if value is an array:
      matched = any element matches    # each element is a scalar
  else:
      matched = the scalar matches
  return clause.negate ? not matched : matched
```

A scalar matches when it matches **any** of the clause's values under the operator:

| Operator | Attribute type | A clause value `v` matches when |
|---|---|---|
| `in` | string, number, boolean | String: exact, case-sensitive equality. Number: `v` parses as a decimal and is numerically equal (so `31` equals `31.0`). Boolean: `v` is `true` or `false` (any case) and equal. |
| `contains` | string | `v` is a substring (case-sensitive). |
| `startsWith` | string | `v` is a prefix (case-sensitive). |
| `endsWith` | string | `v` is a suffix (case-sensitive). |
| `lt`, `lte`, `gt`, `gte` | number | The attribute compares to `v` parsed as a decimal. |
| `exists` | any | The attribute is present and not null (`values` must be empty). |

- There is no `notIn`: "is not one of" is `in` with `negate: true`. The dashboard shows every operator and negation as a
  plain-language label ("does not contain", "does not exist").
- A type mismatch never matches (for example `contains` on a number, or `gt` on a string).
- **String comparisons are case-sensitive.** Normalize values in your application before sending them: lower-case
  emails, for example, and write rule values the same way.
- There are no regular-expression operators, so a rule can never be a denial-of-service vector (ReDoS).

## Percentage rollouts

```
bucketValue = the rollout's bucketBy attribute     # default "key"
    strings are used as they are; numbers use invariant culture without trailing zeros (31.0 becomes "31");
    booleans, arrays, missing, and null values have no bucket value

bucket = no bucket value ? 0
       : BigEndianUInt32(SHA256(UTF8("{flagKey}.{flagSalt}.{bucketValue}"))[0..4]) % 100000

cumulative = 0
for each weight, in the flag's variation order:
    cumulative += weight.weight
    if bucket < cumulative:
        return weight.variationId, inRollout = true
```

Each flag has a random 16-character salt, created with the flag and never changed. The properties that matter:

- **Deterministic.** A context stays in the same bucket for a flag, so it keeps its variation between requests,
  devices, and servers.
- **Independent across flags.** The flag key and salt are part of the hash, so being in the first 10% of one flag says
  nothing about another.
- **Monotonic ramp-up.** Weights are always stored in the flag's variation order, and boolean flags list `true` first.
  A context that gets `true` at 10% still gets it at 20%, 50%, and 100%, so a gradual rollout only ever adds people.
- **Accurate.** Over 20,000 synthetic keys, every variation's share is within 1.5 percentage points of its weight.

A context without a bucket value (for example, bucketing by an attribute it does not have) lands in bucket 0, which
means the first variation with a non-zero weight.

## Validation

`TargetingValidator` checks a configuration before it is saved and before the test panel evaluates a draft. Each
error has a path, such as `rules[1].clauses[0].values`, that the dashboard uses to highlight the field.

- Every referenced variation exists on the flag.
- Rollout weights are integers from 0 to 100000, reference each variation at most once, and add up to exactly 100000.
- Rule ids are unique (at most 64 characters); a configuration has at most 50 rules; each rule has 1 to 10 conditions
  and a description of at most 200 characters.
- `exists` conditions have no values; every other condition has 1 to 500 values; values for numeric operators all
  parse as decimals.
- Each target list has at most 1000 context keys, and a key appears in at most one list.
- Attribute names and `bucketBy` are valid attribute names.

Saving also normalizes the configuration: rollout weights are put in variation order, duplicate keys inside a target
list are removed, and empty target lists are dropped.

## Result

```json
{
  "flagKey": "new-product-layout",
  "value": true,
  "variationId": "true",
  "reason": { "kind": "RULE_MATCH", "ruleId": "r_91bc04", "ruleIndex": 1, "inRollout": true }
}
```

Reason kinds: `OFF`, `TARGET_MATCH`, `RULE_MATCH`, `FALLTHROUGH`, `FLAG_NOT_FOUND`, `ERROR`. `ruleId` and `ruleIndex`
(zero-based) appear for rule matches, and `inRollout` for rule matches and fallthroughs.

## Performance

The evaluation API compiles each environment's configuration into a snapshot once: target lists become hash sets,
numeric clause values are parsed ahead of time, and variations are indexed by id, so evaluating a context touches no
parsing and allocates little. The target is well under a millisecond for a hundred flags; it has not been measured
yet (a BenchmarkDotNet suite is on the roadmap). The snapshot is rebuilt when the environment changes (see
[architecture](architecture.md#change-propagation)).

## Tests

`tests/backend/FlagForge.Evaluation.Tests` covers each operator (positive, negative, and type mismatches), negation
(including that a missing attribute never matches even when negated), `exists`, array attributes, the `key`
attribute, precedence, rollout determinism, independence, monotonic ramp-up, distribution accuracy, bucket 0 for a
missing `bucketBy` attribute, `FLAG_NOT_FOUND` and `ERROR`, and every validation rule with its path. CI fails when
line coverage of `FlagForge.Evaluation` drops below 95%.
