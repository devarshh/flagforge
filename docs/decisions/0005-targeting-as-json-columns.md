# 0005. Targeting stored as JSON columns

## Context

A flag's configuration per environment is a tree: target lists, rules with clauses, and serves that are either a
variation or a weighted rollout. Variations hold arbitrary JSON values. It is always read and written as a whole, and
the evaluation engine compiles it as a whole.

## Decision

Store variations, tags, targets, rules, serves, schedule payloads, and audit before/after snapshots as `nvarchar(max)`
JSON columns. They are written with one System.Text.Json value converter that uses the API's JSON options, so stored
JSON has exactly the API's shape. Tags use EF Core's primitive-collection mapping so the tag filter becomes `OPENJSON`
in SQL. Each config row has an integer `Version` used for optimistic concurrency.

## Consequences

- One row per flag and environment, read and written in one statement; no joins across a dozen normalized tables.
- The dashboard's diff, the audit log, and the API all see the same document shape.
- EF Core's structured JSON mapping (owned or complex types) could not represent variation values, which can be any JSON;
  the converter can. The price is that SQL cannot query inside rules, which nothing needs.
- Validation lives in code (`TargetingValidator`), not in database constraints, and runs before every save.
