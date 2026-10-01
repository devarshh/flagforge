# FlagForge — build log

FlagForge was built in thirteen phases, each with a checkpoint that had to pass before the next one started. This log
records the phases, the final state and how it was verified, the tools used, the decisions made along the way, and
what remains unverified.

## Phases

| Phase | Work | Checkpoint | Status |
|---|---|---|---|
| 0 | Repository scaffolding, tooling configs, solution and empty projects, npm workspace, `.env.example`, `PROGRESS.md`, LICENSE, `git init` | `dotnet build` succeeds; `npm ci && npm run build` succeeds in `src/frontend` | [x] |
| 1 | Evaluation engine and validator with full tests | Tests pass; coverage ≥95% | [x] |
| 2 | Domain, Infrastructure, EF Core model and initial migration, Migrator with seed data | Against the Compose SQL Server: the migrator runs twice and the second run changes nothing | [x] |
| 3 | ServiceDefaults and Management API with integration tests | Tests pass; `/scalar` lists every endpoint | [x] |
| 4 | Evaluation API with integration tests | Tests pass | [x] |
| 5 | Worker with integration tests | Tests pass | [x] |
| 6 | SDK and React bindings with tests | Tests pass; package builds with types | [x] |
| 7 | Dashboard with tests | Lint, typecheck, tests, and build pass; against locally running APIs you can sign in, create a flag, edit targeting, and save | [x] |
| 8 | Demo app | Builds; connects to the local evaluation API and updates live when a flag changes | [x] |
| 9 | Dockerfiles, Compose, gateway, smoke test, Makefile | `docker compose up --build -d` on a clean checkout, then the smoke test passes in full mode | [x] |
| 10 | Kubernetes base, component, overlays, migrator Job, platform, kind scripts | Every kustomization validates; if kind and helm are available, `scripts/k8s-local-up.sh --smoke` passes | [x] |
| 11 | Bicep, all GitHub workflows, Dependabot, PR template | `az bicep build` (if `az` is available) and `actionlint` pass | [x] |
| 12 | Documentation, polish, and final verification | Run everything again from a clean state (backend tests, frontend tests, Compose smoke test, manifest validation); `PROGRESS.md` complete with Decisions and Unverified sections | [x] |

## Current state

All phases (0–12) are complete. Backend: 368 tests (245 for the evaluation engine, at 99.6% line coverage). Frontend:
132 Vitest tests (SDK 22, dashboard 95, demo 15). Documentation: README (with real screenshots), architecture,
evaluation, API, local Kubernetes, Azure setup, and ten ADRs.

Phase 12 checkpoint (2026-09-30 and 2026-10-01), on a fresh clone of the final commit with no `.env`:

- **Backend:** `dotnet format --verify-no-changes` is clean, the Release build has 0 warnings, all 368 tests pass, and
  `scripts/check-coverage.sh` reports 99.6% for `FlagForge.Evaluation`.
- **Frontend:** `npm ci`, `lint` (ESLint and Prettier), `typecheck`, `test`, and `build` pass with no warnings (run at
  `a374ec2`; the two later commits change only backend files).
- **Static checks:** `scripts/k8s-validate.sh` (every kustomization valid, nothing skipped), `actionlint` with
  shellcheck, `shellcheck scripts/*.sh`, and `az bicep build`, `lint`, and `build-params`.
- **Compose:** `docker compose up --build -d` on wiped volumes in one pass; the smoke test passed in full mode
  (FlagsChanged 68 ms after the toggle) and in readonly mode; no warnings or errors in any service log.
- **kind:** `scripts/k8s-local-up.sh --smoke` from a new cluster passed (migrator Job, Envoy Gateway, full smoke test,
  FlagsChanged after 58 ms).
- **Definition of done in the UI** (headless Chrome against the clean Compose stack, at the commit before the last two
  backend fixes): signed in as the admin, created
  a flag, added a rule (email ends with `@acme.com`) and a 25/75 rollout, reviewed the diff, and saved; History shows
  the change with its diff. Two editors saving the same targeting got the 409 conflict dialog, and "Load latest
  version" loaded the other save. A scheduled turn-on and a three-step release plan completed through the worker,
  in order, within 35 s. Insights charts the seeded usage; Stale flags lists `dark-mode-beta`. A Viewer sees every
  switch disabled and no Create flag button; an Editor can switch Development and Staging but not Production, and
  cannot edit Production targeting.
- **Fixed during the final pass**, each with tests: the evaluation API never received changes when it started before
  Redis, and missed RESP3 reconnects (found by the kind smoke test); the Insights chart merged the oldest and the
  current hour; "Last evaluated" claimed minute precision for hourly data; targeting JSON carried the unused serve
  alternative as null; environment header chips were truncated; and the demo's inspector covered the store on phones.

Phase 9 checkpoint: a copy of exactly the files git tracks (no `.env`, no build output) ran as its own Compose project
with fresh volumes. `docker compose up --build -d` succeeded in one pass, and the smoke test passed in full mode
(FlagsChanged 72 ms after the toggle) and in readonly mode; bad settings exit 1 with a clear message. Through the
gateway: security headers, immutable asset caching with gzip, `no-cache` HTML (including deep links), `/demo` to
`/demo/`, the runtime `/demo/config.json`, 413 for bodies over 1 MB, and no warnings or errors in any service log. In
a browser, the demo showed Live through the gateway and the dashboard's sign-in and cookie session restore worked.

Phase 10 checkpoint: `scripts/k8s-validate.sh` validates every kustomization with kubeconform (local, preview, a
preview rendered with a sample namespace, hostname, and images, aks, the migrator Job, and both platforms; no
skipped resources). `scripts/k8s-local-up.sh --smoke` passed from scratch (new kind cluster, Envoy Gateway 1.9.2, SQL
Server under Rosetta on the arm64 node, migrator, full smoke test through Envoy with FlagsChanged after 101 ms) and
on a rerun against the existing cluster. A SignalR connection held idle for 390 s through Envoy stayed connected and
still received FlagsChanged.

Phase 11 checkpoint: `az bicep build --file infra/main.bicep` and `bicep lint` pass with no warnings (every API
version has Bicep types, so properties are validated), `bicep build-params` resolves `main.bicepparam`, and
`actionlint` passes on all six workflows with shellcheck checking their scripts. `kustomize edit set image` was
exercised on a copy of the manifests the way CD runs it.

Earlier manual checks: Phase 7 against the Compose SQL Server and Redis (sign in, create a flag, edit targeting,
save, test panel, light mode, 360 px); Phase 8 through the demo's dev server (a flag change reached the page in
702 ms; `identify` on shopper switch).

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
| shellcheck | 0.11.0 | Downloaded to `./.tools/` in Phase 11 (checksum verified); lets local actionlint check scripts like CI |
| kustomize | 5.8.2 | Downloaded to `./.tools/` in Phase 11 (checksum verified); CI pins the same version |
| Bicep CLI | 0.47.16 | Installed with `az bicep install` (in `~/.azure/bin`) in Phase 11 |
| ffmpeg | 8.0.1 | Homebrew; used in Phase 12 only to encode the README images (with headless Google Chrome) |

## Decisions

- **TypeScript 6.0.3, not 7.x.** TypeScript 7.0.2 is the latest release, but typescript-eslint 8.71 (latest) declares
  `typescript >=4.8.4 <6.1.0`. 6.0.3 is the newest version inside that range.
- **React Router 7.18.4.** The dashboard targets v7 in library mode; 8.x exists but was out of scope.
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
- **Context details decided during the build:** a `null` attribute is accepted and treated as missing; arrays may not
  contain `null`; duplicate attribute names are a 400; unknown top-level context properties are ignored.
- **Numeric parsing** uses `NumberStyles.Float` with the invariant culture (sign, decimal point, exponent; no
  thousands separators, so `"1,000"` is not a number). Number bucket values drop trailing zeros so `31` and `31.0`
  hash identically.
- **Normalization on save** also removes duplicate keys inside a target list and drops empty target lists, in addition
  to ordering rollout weights by variation.
- **Extra bounds:** rule ids at most 64 characters, rule descriptions at most 200.

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
- **A serve is written with only the alternative it uses** (`{ "variationId" }` or `{ "rollout" }`), by a type-info
  modifier in `JsonDefaults`, so the evaluation model stays attribute-free and audit diffs show no null alternative.
  JSON with explicit nulls still reads.
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
- **`lastEvaluatedAt` has hourly precision** (start of the latest usage hour), so the dashboard shows the current hour
  as "this hour" instead of a minute-precise time, with the hour's range in the tooltip. **FullyRolledOut** requires at
  least one evaluation in the last 14 days; a flag evaluated 15–30 days ago and not since is not stale.
- **Audit filters** resolve keys to ids (matching the indexes); a key that no longer exists matches nothing.
- **ServiceDefaults adds `app.UseServiceDefaults()`** (forwarded headers, exception handler, status-code pages) because
  middleware order matters; `MapDefaultEndpoints()` maps only the health endpoints.

- **Snapshot cache:** single-flight via `Lazy<Task>` per environment; loads use no caller's cancellation token (they
  are shared); failed loads and missing environments are not cached; eviction removes the entry itself, so a load that
  was in flight when a change arrived is never stored.
- **Redis reconnect heals clients too:** besides evicting all snapshots, the subscriber clears the SDK-key
  cache (a revocation may have been missed) and sends `FlagsChanged` with the fresh version to every environment that
  has connections on this pod; without that, streaming clients would keep stale values until the next change.
- **Subscriptions survive a Redis that starts late:** handlers are registered as callbacks, which StackExchange.Redis
  keeps through a failed first subscribe and subscribes whenever it connects (a `ChannelMessageQueue` from a failed
  call is orphaned: the client still feeds it, but nothing reads it). Every restored connection resynchronizes,
  whatever its type, because with RESP3 (the client's choice against Redis 7) subscriptions share the interactive
  connection. Found by the kind smoke test in Phase 12; `RedisOutageTests` starts the API before its Redis.
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
  because `ChangePasswordPage` lives in `auth/`. Helpers such as the draft reducer, operator mapping, rollout
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
- **Flag inspector** is a persistent drawer beside the store on large screens, open at first, and a temporary drawer
  on smaller ones, closed at first (open, it would cover the whole store on a phone).
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

- **Envoy Gateway's chart installs the Gateway API CRDs** (experimental channel, bundle v1.6.1) together with its own,
  so the scripts install no separate CRDs. The chart is pinned to 1.9.2.
- **Numeric users for `runAsNonRoot`:** the SQL Server image runs as the named user `mssql` and Redis's image as root,
  so their pods set the numeric IDs (10001; 999/1000). The .NET and nginx images already use numeric users.
- **SQL Server keeps `NET_BIND_SERVICE`:** `sqlservr` carries that file capability, and the kernel refuses to execute
  it ("Operation not permitted") when the capability is outside the bounding set. Its container drops ALL and adds
  back only NET_BIND_SERVICE (allowed by the restricted Pod Security Standard); with privilege escalation off, the
  process never actually gains it.
- **amd64 SQL Server on arm64 kind nodes:** the script pulls the amd64 image on the host and loads it into the node,
  where it runs under Docker Desktop's Rosetta emulation (binfmt_misc applies inside the kind node too).
- **Reruns of `k8s-local-up.sh`** restart the Deployments (images keep the tag `local`) and delete a SQL Server pod that
  is not ready once the StatefulSet has observed the new spec, because a StatefulSet never replaces a pod that never
  became ready. First runs skip both.
- **Hostnames and preview namespaces are not committed:** the workflows wrap an overlay in a generated kustomization
  under the git-ignored `deploy/k8s/overlays/.generated/`, and `scripts/k8s-validate.sh` renders a preview the same way
  with sample values. The migrator Job defaults to the `local` image tag; CD and previews set theirs.
- **Base labels:** `app.kubernetes.io/part-of: flagforge` on every resource and pod template (not on selectors), and
  `app.kubernetes.io/name` per workload. The SQL component sets the same label, because base labels do not reach
  component resources.
- **Probes:** .NET startup probes allow up to 10 minutes (120 x 5 s) on `/health/ready`, which stays unready until the
  migrator has applied the migrations; nginx uses `/healthz`; Redis and SQL Server use exec probes.
- **nginx images keep a writable root filesystem** (their entrypoint renders config templates at startup). The .NET
  containers and Redis are read-only with `emptyDir` mounts (`/tmp`, `/data`).

- **Bicep API versions** are the newest stable versions the Bicep CLI (0.47.16) has types for: AKS 2026-05-01, SQL
  2025-01-01, ACR 2025-11-01, managed identity 2024-11-30, role assignments 2022-04-01. So `bicep build` validates
  every property (a probe confirmed unknown properties raise BCP037).
- **Azure SQL free offer:** API 2025-01-01 has `useFreeLimit` and `freeLimitExhaustionBehavior`, so the database uses
  the free offer with `AutoPause`. A subscription has one free database, so `sqlUseFreeLimit` (default true) lets a
  deployment opt out. The database is serverless GP Gen5 (0.5 to 2 vCores, auto-pause 60 min, 32 GB).
- **GitHub identity roles** (each on one resource): AcrPush, AcrDelete (preview tag cleanup), and Reader on the
  registry, because `az acr login` and `az acr repository` resolve the registry through Azure Resource Manager; the
  AKS Cluster User Role covers `az aks get-credentials` (it includes the cluster read and user-credential actions).
- **`main.bicepparam` reads everything deployment-specific from the environment** (`readEnvironmentVariable`), and
  `scripts/azure-deploy-infra.sh` prints `gh` commands that reference `$SQL_ADMIN_PASSWORD` or generate the other
  secrets when run, so no secret value is ever printed.
- **Workflow tooling:** kubeconform, actionlint, and kustomize are downloaded at pinned versions and checked against
  pinned SHA-256 sums (`.github/actions/install-tools`); kubectl and Helm use `azure/setup-kubectl` and
  `azure/setup-helm` with pinned versions. No third-party actions are used, so no SHA pins are needed.
- **Shared deploy steps are scripts:** `scripts/k8s-run-migrator.sh` (local, CD, previews) and
  `scripts/k8s-generate-overlay.sh` (CD, previews, validation). The sticky preview comment is one CommonJS helper
  (`.github/scripts/preview-comment.cjs`) loaded by `actions/github-script`.
- **CI concurrency** includes `github.workflow`, which is the caller's name when CD calls CI, so a push to main does not
  cancel its own CD run; only pull request runs cancel in progress. CI requests `id-token: write` for the image build
  because the reusable workflow's job needs it when pushing; CI never pushes.
- **Coverage and results** use the xUnit v3 test platform options (`--report-xunit-trx`, `--coverage
  --coverage-output-format cobertura`); `scripts/check-coverage.sh` enforces 95% for FlagForge.Evaluation and writes
  the summary shown in the job summary.
- **The preview janitor deletes a namespace only when the pull request is CLOSED or MERGED**; an unreadable state keeps
  the preview and logs a warning.

- **README images are real captures** from a fresh Compose stack, taken with a throwaway headless-Chrome script (the
  DevTools protocol over Node's built-in WebSocket, so nothing was installed) and encoded with ffmpeg. The GIF stacks
  the flag list above the demo store so text stays readable at README width. An HTML comment in the README explains
  how to recapture them by hand.
- **Flag list environment columns are 136 px**, enough for the default environments' header chips (Production's
  includes a lock icon).
- **The Insights x axis is keyed by bucket start**, not by its label: the last 24 hours span 25 hourly buckets, and
  the first and last share an hour label, which a band axis would merge into one bar.

## Unverified

- **GitHub workflows have never run.** The repository has no remote, so CI, CD, previews, the janitor, the bootstrap
  workflow, and Dependabot were checked only statically (actionlint with shellcheck). Their building blocks were run
  locally: the Compose smoke test, `scripts/k8s-validate.sh`, `scripts/check-coverage.sh`, the xUnit report options,
  `kustomize edit set image`, and the migrator and overlay scripts (through the kind deployment).
- **Nothing was deployed to Azure.** The Bicep builds and lints cleanly, but `scripts/azure-deploy-infra.sh`, the
  role assignments (including whether Reader is needed for `az acr login` with AcrPush), the free-offer settings, and
  the AKS deployment have not run against a subscription.
