# FlagForge — build progress

This file tracks the build against `PROJECT_SPEC.md` §27. A fresh session should read this file,
verify the current state (build + tests), and continue from the first unchecked item.

## Phases

| Phase | Work | Checkpoint | Status |
|---|---|---|---|
| 0 | Repository scaffolding, tooling configs, solution and empty projects, npm workspace, `.env.example`, `PROGRESS.md`, LICENSE, `git init` | `dotnet build` succeeds; `npm ci && npm run build` succeeds in `src/frontend` | [x] |
| 1 | Evaluation engine and validator with full tests (§7) | Tests pass; coverage ≥95% | [x] |
| 2 | Domain, Infrastructure, EF Core model and initial migration, Migrator with seed data | Against the Compose SQL Server: the migrator runs twice and the second run changes nothing | [x] |
| 3 | ServiceDefaults and Management API with integration tests | Tests pass; `/scalar` lists every endpoint | [x] |
| 4 | Evaluation API with integration tests | Tests pass | [x] |
| 5 | Worker with integration tests | Tests pass | [x] |
| 6 | SDK and React bindings with tests | Tests pass; package builds with types | [x] |
| 7 | Dashboard with tests | Lint, typecheck, tests, and build pass; against locally running APIs you can sign in, create a flag, edit targeting, and save | [x] |
| 8 | Demo app | Builds; connects to the local evaluation API and updates live when a flag changes | [x] |
| 9 | Dockerfiles, Compose, gateway, smoke test, Makefile | `docker compose up --build -d` on a clean checkout, then the smoke test passes in full mode | [x] |
| 10 | Kubernetes base, component, overlays, migrator Job, platform, kind scripts | Every kustomization validates (§20.6); if kind and helm are available, `scripts/k8s-local-up.sh --smoke` passes | [ ] |
| 11 | Bicep, all GitHub workflows, Dependabot, PR template | `az bicep build` (if `az` is available) and `actionlint` pass | [ ] |
| 12 | Documentation (§25), polish, and final verification | Run everything again from a clean state (backend tests, frontend tests, Compose smoke test, manifest validation); `PROGRESS.md` complete with Decisions and Unverified sections | [ ] |

## Current state

Phases 0–9 complete. Backend: 366 tests. Frontend: 122 Vitest tests (SDK 22, dashboard 85, demo 15); root `npm run
lint`, `typecheck`, `test`, and `build` pass.

Phase 9 checkpoint: a copy of exactly the files git tracks (no `.env`, no build output) ran as its own Compose project
with fresh volumes. `docker compose up --build -d` succeeded in one pass, and the smoke test passed in full mode
(FlagsChanged 72 ms after the toggle) and in readonly mode; bad settings exit 1 with a clear message. Through the
gateway: security headers, immutable asset caching with gzip, `no-cache` HTML (including deep links), `/demo` to
`/demo/`, the runtime `/demo/config.json`, 413 for bodies over 1 MB, and no warnings or errors in any service log. In
a browser, the demo showed Live through the gateway and the dashboard's sign-in and cookie session restore worked.

Earlier manual checks: Phase 7 against the Compose SQL Server and Redis (sign in, create a flag, edit targeting,
save, test panel, light mode, 360 px); Phase 8 through the demo's dev server (a flag change reached the page in
702 ms; `identify` on shopper switch). Next: Phase 10 (Kubernetes and kind).

## Tooling (Phase 0 check, 2026-09-29, macOS arm64)

| Tool | Version | Notes |
|---|---|---|
| dotnet | SDK 10.0.401, runtime 10.0.12 | |
| node / npm | 24.21.0 / 11.19.0 | |
| docker / compose | 29.8.1 / v5.5.1 | Docker Desktop, 10 CPUs, 7.7 GiB |
| kind | 0.33.0 | |
| kubectl | 1.37.1 (Kustomize 5.8.1) | |
| helm | 4.3.0 | |
| az | 2.90.0 | |
| actionlint | 1.7.12 | Downloaded to `./.tools/` (checksum verified) |
| kubeconform | 0.8.0 | Downloaded to `./.tools/` (checksum verified) |

## Decisions

- **TypeScript 6.0.3, not 7.x.** TypeScript 7.0.2 is the latest release, but typescript-eslint 8.71 (latest) declares
  `typescript >=4.8.4 <6.1.0`. 6.0.3 is the newest version inside that range.
- **React Router 7.18.4.** The spec requires v7 in library mode; 8.x exists but is out of scope.
- **`@types/node` 24.x** to match the Node 24 LTS runtime (`.nvmrc`), not the 26.x "latest" tag.
- **Tests run on Microsoft Testing Platform.** xUnit v3 4.x ships `xunit.v3.mtp-v2`, and MTP v2 no longer supports the
  VSTest bridge on the .NET 10 SDK, so `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }` and the
  test projects reference no VSTest packages. Coverage uses `Microsoft.Testing.Extensions.CodeCoverage` (Cobertura
  output) and ReportGenerator; the ≥95% gate for `FlagForge.Evaluation` is a small script because ReportGenerator has
  no threshold option.
- **`dotnet-tools.json` at the repository root.** That is where `dotnet new tool-manifest` puts it on .NET 10
  (pins `dotnet-ef` and `reportgenerator`).
- **`-chiseled-extra` runtime images instead of plain `-chiseled`.** `Microsoft.Data.SqlClient` throws
  `NotSupportedException: Globalization Invariant Mode is not supported` when opening a connection, and the plain
  chiseled images run in invariant mode because they ship without ICU. The `-extra` variants
  (`aspnet:10.0-noble-chiseled-extra`, `runtime:10.0-noble-chiseled-extra`) are still distroless and non-root, with no
  shell or package manager, and add ICU and tzdata. Found in Phase 2 by running locally with invariant mode on.
- **The demo consumes the SDK's built `dist/`** through its `exports` map, exactly like an external consumer. Root
  `lint`, `typecheck`, and `test` scripts build the SDK first; the demo's `predev` builds it for `npm run dev`.
- **`npm run lint` also runs `prettier --check`**, so one command covers both linters in CI.

- **Evaluation model is attribute-free.** The targeting records (`TargetingConfig`, `Rule`, `Clause`, `Serve`, ...)
  live in `FlagForge.Evaluation` with no serialization attributes; the JSON contract (camelCase names, camelCase enum
  strings, `SCREAMING_SNAKE` reason kinds) is configured once in the application's JSON options.
- **Context details not fixed by the spec:** a `null` attribute is accepted and treated as missing; arrays may not
  contain `null`; duplicate attribute names are a 400; unknown top-level context properties are ignored.
- **Numeric parsing** uses `NumberStyles.Float` with the invariant culture (sign, decimal point, exponent; no
  thousands separators, so `"1,000"` is not a number). Number bucket values drop trailing zeros so `31` and `31.0`
  hash identically.
- **Normalization on save** also removes duplicate keys inside a target list and drops empty target lists, in addition
  to ordering rollout weights by variation.
- **Extra bounds** not in the spec: rule ids at most 64 characters, rule descriptions at most 200.

- **Entity `ProjectEnvironment`** (table `Environments`): a class named `Environment` would clash with
  `System.Environment` in every file that imports the domain namespace.
- **JSON columns:** documents (variations, targets, rules, serves, schedule payloads, audit before/after) are mapped with
  one System.Text.Json value converter using the API's JSON options, so stored JSON has exactly the API shape. EF Core's
  structural JSON mapping (owned/complex types) cannot represent variation values, which are arbitrary JSON. `Tags`
  use EF Core's built-in primitive-collection JSON mapping so the tag filter translates to `OPENJSON` in SQL. All are
  `nvarchar(max)`; `UseCompatibilityLevel(160)` keeps that true on Azure SQL too.
- **UUIDv7 keys:** `Guid.CreateVersion7(timeProvider.GetUtcNow())` as specified. Caveat for interviews: SQL Server
  sorts `uniqueidentifier` by its last six bytes first, so v7's time ordering does not reduce page splits there; at
  scale, prefer server-side `NEWSEQUENTIALID()` or EF Core's sequential GUID generator.
- **No foreign keys on `AuditEntries` and `FlagUsageHourly`:** audit history must survive deletions, and a usage flush
  must never fail because a flag was deleted between evaluation and flush.
- **Cascade paths:** configs and scheduled changes cascade from flags; their environment FKs are `NO ACTION` because
  SQL Server allows only one cascade path. Environment deletion removes those rows explicitly.
- **`IncrementConfigVersionAsync` lives on the DbContext abstraction** because it must run inside the caller's
  transaction; other raw SQL (schedule claiming, usage MERGE, stale detection) sits behind separate interfaces.
- **JSON flag values must be an object or an array;** strings, numbers, and booleans have their own flag types.
- **Seed data:** the six demo flags are 14 days old (not yet stale-eligible); `dark-mode-beta` is 60 days old with no
  usage (stale); `legacy-search` is archived; `max-cart-items` is permanent. Development has persona-sensitive rules
  (plan, email domain, beta) so the demo's "Shopping as" switcher visibly changes the store. Seeded variation and rule
  ids are fixed, readable values in the `v_`/`r_` + 6 character format.
- **Migrator content root** is the app directory, so `dotnet run --project` works from any folder.

- **Contracts and validators live in `FlagForge.Application/<Feature>/`** (`*Contracts.cs`, `*Validators.cs`, next
  to the service), not in the API feature folders. The worker applies scheduled changes through the same services, so
  one contract and one validation path serve both entry points; `ManagementApi/Features/<Feature>/` holds only the
  endpoint group. FluentValidation checks request shape; `TargetingValidator` checks targeting semantics.
- **Errors are exceptions mapped once** (`AppExceptionHandler` in ServiceDefaults): typed application exceptions
  become 400/401/403/404/409 ProblemDetails; JSON binding failures (`ThrowOnBadRequest`) become 400 with the field path
  (for example `config.rules[0].clauses[0].operator`); SQL duplicate-key races become 409.
- **JSON enum values are camelCase** everywhere (`"type": "boolean"`, `"role": "admin"`, `"action": "turnOn"`,
  operators `"in"`/`"endsWith"`); only evaluation reason kinds use the SDK format (`"RULE_MATCH"`).
- **PATCH semantics:** null or missing fields are unchanged; an empty description clears it.
- **Extra read endpoints** so every `201 Created` `Location` resolves: `GET /users/{id}`,
  `GET /projects/{p}/environments/{e}`, `GET .../sdk-keys/{id}`, `GET .../scheduled-changes/{id}`. A release plan's
  `Location` is the scheduled-changes collection.
- **Creating a flag bumps and publishes every environment** of the project: the new key appears in evaluation output.
- **Archiving a flag cancels its pending scheduled changes** (audited as `schedule.cancelled`); archived flags reject
  targeting changes and new schedules with 409 until restored.
- **Scheduled-change execution is exactly-once and ordered:** the executing transaction first flips
  `Processing → Completed` for its own claim (the row lock blocks competing workers; a failure rolls it back), and a
  change waits while an earlier open change exists for the same flag and environment, so release-plan steps apply in
  order even when several are due at once.
- **Auth details:** one generic 401 message for unknown email, wrong password, locked, and inactive accounts (with a
  dummy hash check for unknown emails); refresh rotation is a conditional update, so of two concurrent refreshes only
  one wins; a failed refresh also deletes the cookie; changing a password ends every other session; logout is
  anonymous because it only needs the cookie; JWT lifetimes are validated against the injected `TimeProvider`.
- **Login rate limit** is configurable (`RateLimiting__LoginPermitsPerMinute`, default 10).
- **`lastEvaluatedAt` has hourly precision** (start of the latest usage hour). **FullyRolledOut** requires at least one
  evaluation in the last 14 days; a flag evaluated 15–30 days ago and not since is not stale.
- **Audit filters** resolve keys to ids (matching the indexes); a key that no longer exists matches nothing.
- **ServiceDefaults adds `app.UseServiceDefaults()`** (forwarded headers, exception handler, status-code pages) because
  middleware order matters; `MapDefaultEndpoints()` maps only the health endpoints.

- **Snapshot cache:** single-flight via `Lazy<Task>` per environment; loads use no caller's cancellation token (they
  are shared); failed loads and missing environments are not cached; eviction removes the entry itself, so a load that
  was in flight when a change arrived is never stored.
- **Redis reconnect heals clients too:** besides evicting all snapshots (spec), the subscriber clears the SDK-key
  cache (a revocation may have been missed) and sends `FlagsChanged` with the fresh version to every environment that
  has connections on this pod; without that, streaming clients would keep stale values until the next change.
- **SDK key checks:** anything with the `ffk_` prefix up to 128 characters is looked up (the seeded development key is
  not 43 characters); unknown keys are not cached, so a new key works immediately.
- **Hub transport is WebSockets only** (no negotiate, no long polling), matching the SDK and needing no sticky sessions.
- **Final usage flush runs in `UsageFlushService.StopAsync`**, which the host calls after the web server has drained
  in-flight requests, instead of an `ApplicationStopping` callback that fires before draining. `ShutdownTimeout` is
  25 s. A flush cancelled mid-write puts its counts back for that final flush.
- **Usage upserts** run all 300-row `MERGE ... WITH (HOLDLOCK)` statements in one transaction (a retried flush cannot
  double count) with rows sorted so concurrent pods lock in the same order.
- **No SDK keys in logs:** `Microsoft.AspNetCore.Hosting.Diagnostics` is capped at Warning in code, because
  request-start logs print full URLs and hub URLs carry `access_token`. A test captures all logs at Trace and asserts
  the key never appears (a mutation check confirmed the test fails without the filter). OpenTelemetry's ASP.NET Core
  instrumentation redacts query values by default.
- **Body-limit test uses real Kestrel** (.NET 10 `WebApplicationFactory.UseKestrel()`), because the in-memory
  TestServer does not apply `MaxRequestBodySize`.

- **Worker jobs** share a `PeriodicJob` base: run once at startup, then on every `PeriodicTimer` tick of the injected
  clock, with a fresh DI scope per run; exceptions are logged and the loop continues. Each claimed change executes in
  its own scope, and a failure is recorded from yet another scope so half-applied tracked entities never leak.
  Claims last 5 minutes (`ClaimedUntil`), after which another replica may reclaim the change.
- **Worker tests drive processing directly:** the test host removes the periodic jobs so "two processors in parallel"
  is deterministic; one test keeps them and advances the fake clock to prove the real timer path executes changes.

- **SDK internals:** results are stored per flag and the previous object is kept when a flag's evaluation is
  unchanged, so React hooks (built on `useSyncExternalStore`) re-render only for their own flag and JSON values keep
  their identity. `change` compares values only (per spec); a separate `subscribe` hook notifies on any state change
  (evaluations, readiness, connection) for UI bindings. After every successful (re)connection the client re-evaluates
  once to catch changes made while it was not listening. `ready()` resolving on timeout does not set `isReady`.
- **Connection states:** `offline` means closed, or polling while the last request failed; a later successful poll
  returns to `polling`. A background reconnect is attempted every 30 s while polling.
- **Default SDK logger** writes warnings and errors to the console (`[flagforge]` prefix); pass `logger: {}` to
  silence it. Invalid options (missing `baseUrl`, `sdkKey`, or `context.key`) throw at `createClient`, since they
  are programming errors rather than flag unavailability.

- **Dashboard structure:** each feature folder holds its API hooks and components; the project settings page
  (details, environments, SDK keys, danger zone) lives in `features/projects/`, and there is no `features/account/`
  because §12.1 puts `ChangePasswordPage` in `auth/`. Helpers such as the draft reducer, operator mapping, rollout
  math, and validation are plain `.ts` modules so React Fast Refresh keeps working; test helpers live in `src/test/`
  (the only place the react-refresh lint rule is off).
- **Audit details open in a side panel.** The free MUI X Data Grid has no expandable detail rows (a Pro feature), so a
  row click (or its view button) opens the entry's `JsonDiff` in a drawer. The flag History tab uses expandable
  accordion rows.
- **Targeting draft and server paths:** the editor sends every variation's target list (the server drops empty ones),
  so `targets[i]` error paths match the draft's rows. Rollouts send only non-zero weights, because the server counts
  any listed variation as in use and would block removing it; a server error on one weight is shown on the rollout.
  Client validation mirrors `TargetingValidator` with the same paths. Rollout problems show while typing; other
  problems appear after the first "Review changes".
- **Concurrency in the editor:** saves send `expectedVersion`; a 409 opens the conflict dialog. While the draft has no
  unsaved changes it adopts newer saved configs automatically (for example after a toggle). Quick toggles from the
  list and the header send no `expectedVersion`, because turning a flag on or off expresses intent regardless of
  other edits; they update the list and detail caches optimistically and roll back on error.
- **Flag list state lives in the URL** (`q`, `tag`, `archived`, `page`, `pageSize`). The API has no tag endpoint, so
  the tag chips come from the current page plus the active tag; clicking a tag in a row also filters.
- **Additions the API supports:** "Add environment" and project name and description editing on the settings page.
  The audit "Who" filter lists every user for admins; for everyone it includes the actors on the current page
  (non-admins cannot list users).
- **Scheduled changes poll every 15 s** while any change is pending or running, matching the worker's interval.
- **Bundles:** React/TanStack and MUI core are split into their own long-cached chunks (Rolldown `codeSplitting`
  groups); the Data Grid, charts, and date pickers load with the pages that use them.
- **Dialog focus in development:** React StrictMode runs MUI's focus-trap effect twice in `npm run dev`, which moves
  focus from an `autoFocus` field to the dialog. Production builds focus the field (checked with `vite preview`).

- **Demo SDK key:** a key pasted in settings (localStorage `acme-coffee-sdk-key`) wins, then `/demo/config.json`
  (nginx fills it from `DEMO_SDK_KEY`), then the dev server's fallback: `VITE_DEMO_SDK_KEY`, else the root `.env`'s
  `FF_SEED_DEMO_SDK_KEY`, else the seeded development key. The fallback is injected with `define` only when serving,
  so production builds contain no key (checked). `config.json` is read from `${BASE_URL}config.json`, and non-JSON
  answers (Vite's HTML fallback) count as no key.
- **One SDK client per key.** Switching shoppers calls `identify` (no reconnect); changing the key closes the client
  and creates another, and the provider is keyed by connection so hooks and the inspector start fresh.
- **The store checks flag shapes beyond the SDK's type check** (theme accent must be a hex color and `rounded` a
  boolean, checkout colors must be known, cart limits 1–100) and falls back to its defaults otherwise.
- **Lowering `max-cart-items` below the cart's size** keeps the items, blocks adding and checkout, and asks the shopper
  to remove the excess, which makes the live change visible.
- **Flag inspector** is a persistent drawer beside the store on large screens and a temporary drawer on smaller ones.
- **Code font utility:** `.mono.mono` in both themes, because MUI component styles are injected after global styles
  and a single class lost to them.

- **.NET images publish for the target architecture** (`-a $TARGETARCH`, with the SDK stage on `$BUILDPLATFORM`): the
  app layer is 23 MB instead of 91 MB because no Windows or macOS native libraries are included, and amd64 images
  build natively on Apple Silicon. The Dockerfiles also copy `.editorconfig`, which sets analyzer severities (warnings
  are errors).
- **Gateway** (`deploy/compose/gateway.nginx.conf`) resolves service names per request through Docker's DNS, so it can
  start before the services and survives their restarts. Every nginx config uses relative redirects
  (`absolute_redirect off`), so redirects keep the gateway's port (8080 in Compose, 8090 in kind). `/demo` redirects
  to `/demo/`.
- **Frontend images** share one security-headers snippet (`src/frontend/nginx/security-headers.conf`), included in
  every location that adds headers, because `add_header` in a location drops the inherited ones.
- **Compose healthchecks use 127.0.0.1**: in Alpine `localhost` resolves to `::1` first, and nginx listens on IPv4 only.
  Adding an IPv6 `listen` would stop nginx starting on hosts without IPv6.
- **`DEMO_SDK_KEY` defaults to `FF_SEED_DEMO_SDK_KEY`** through nested Compose defaults. The Aspire dashboard image is
  pinned to 13.5.2, allows anonymous access (local only), and binds its UI to 127.0.0.1.
- **Quiet logs:** the APIs log `Microsoft.AspNetCore.DataProtection` at Error only, because `AddAuthentication`
  registers Data Protection (unused here) and it warns at startup about unpersisted keys. Retention deletes each batch
  oldest first by the indexed cutoff column, which also removes EF Core's "Take without OrderBy" warning.
- **Smoke test** registers for `FlagsChanged` before it toggles the flag, and archives its flag in a `finally` block
  whenever creation succeeded, so failed runs clean up too.
- **`scripts/gen-dev-secrets.sh`** writes letters and digits only (no quoting needed), guarantees SQL Server's password
  complexity with fixed parts, refuses to overwrite `.env` without `--force`, and sets mode 600. The Makefile installs
  node modules only when they are missing.

## Unverified
