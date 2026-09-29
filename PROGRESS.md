# FlagForge — build progress

This file tracks the build against `PROJECT_SPEC.md` §27. A fresh session should read this file,
verify the current state (build + tests), and continue from the first unchecked item.

## Phases

| Phase | Work | Checkpoint | Status |
|---|---|---|---|
| 0 | Repository scaffolding, tooling configs, solution and empty projects, npm workspace, `.env.example`, `PROGRESS.md`, LICENSE, `git init` | `dotnet build` succeeds; `npm ci && npm run build` succeeds in `src/frontend` | [x] |
| 1 | Evaluation engine and validator with full tests (§7) | Tests pass; coverage ≥95% | [ ] |
| 2 | Domain, Infrastructure, EF Core model and initial migration, Migrator with seed data | Against the Compose SQL Server: the migrator runs twice and the second run changes nothing | [ ] |
| 3 | ServiceDefaults and Management API with integration tests | Tests pass; `/scalar` lists every endpoint | [ ] |
| 4 | Evaluation API with integration tests | Tests pass | [ ] |
| 5 | Worker with integration tests | Tests pass | [ ] |
| 6 | SDK and React bindings with tests | Tests pass; package builds with types | [ ] |
| 7 | Dashboard with tests | Lint, typecheck, tests, and build pass; against locally running APIs you can sign in, create a flag, edit targeting, and save | [ ] |
| 8 | Demo app | Builds; connects to the local evaluation API and updates live when a flag changes | [ ] |
| 9 | Dockerfiles, Compose, gateway, smoke test, Makefile | `docker compose up --build -d` on a clean checkout, then the smoke test passes in full mode | [ ] |
| 10 | Kubernetes base, component, overlays, migrator Job, platform, kind scripts | Every kustomization validates (§20.6); if kind and helm are available, `scripts/k8s-local-up.sh --smoke` passes | [ ] |
| 11 | Bicep, all GitHub workflows, Dependabot, PR template | `az bicep build` (if `az` is available) and `actionlint` pass | [ ] |
| 12 | Documentation (§25), polish, and final verification | Run everything again from a clean state (backend tests, frontend tests, Compose smoke test, manifest validation); `PROGRESS.md` complete with Decisions and Unverified sections | [ ] |

## Current state

Phase 0 complete. Next: Phase 1 (evaluation engine).

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
- **`InvariantGlobalization` for every .NET project** (Directory.Build.props), so local runs behave like the chiseled
  images, which ship without ICU.
- **The demo consumes the SDK's built `dist/`** through its `exports` map, exactly like an external consumer. Root
  `lint`, `typecheck`, and `test` scripts build the SDK first; the demo's `predev` builds it for `npm run dev`.
- **`npm run lint` also runs `prettier --check`**, so one command covers both linters in CI.

## Unverified
