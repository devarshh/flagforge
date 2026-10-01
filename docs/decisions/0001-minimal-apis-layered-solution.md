# 0001. Minimal APIs with a layered solution

## Context

FlagForge has two APIs, a worker, and a migrator that share one domain. The evaluation logic must be identical in the
evaluation API (serving SDKs) and the management API (the dashboard's test panel), and it is the part that most needs
exhaustive tests. Controllers, a mediator library, or a repository layer would add ceremony without adding safety.

## Decision

- **ASP.NET Core minimal APIs** with route groups, one file of endpoints per feature, returning `TypedResults`.
  Endpoints stay thin: they bind the request and call an application service.
- **Four layers with a strict dependency direction:** `FlagForge.Evaluation` (the pure engine and validator, with no
  dependencies at all) is referenced by `Domain`, then `Application` (use cases, validation, interfaces such as
  `IChangeNotifier` and `IAuditWriter`), then `Infrastructure` (EF Core, SQL, Redis). The hosts reference what they need.
- The application services use the EF Core `DbContext` directly (through an interface) instead of repositories; the
  few places that need raw SQL (the version bump, schedule claims, usage upserts, stale detection) sit behind their
  own small interfaces.

## Consequences

- The engine is testable in isolation: 245 fast unit tests and at least 95% line coverage, with no mocks.
- The worker applies scheduled changes through the same services as the API, so validation, concurrency checks, and
  audit entries cannot drift apart.
- Minimal APIs keep each endpoint short and give OpenAPI metadata from the typed results.
- The layers are enforced by project references, not by convention, so a shortcut (for example, the domain reaching
  for EF Core) does not compile.
