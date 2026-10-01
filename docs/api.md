# API

FlagForge has two HTTP APIs behind one origin. The **management API** (`/api/v1`) backs the dashboard. The
**evaluation API** (`/sdk`) serves SDKs. Both speak JSON with camelCase properties and enum values as camelCase
strings (evaluation reason kinds use the SDK's `RULE_MATCH` style).

## Interactive reference

The management API publishes an OpenAPI 3.1 document at `/openapi/v1.json`. In development it also serves
[Scalar](https://scalar.com), an interactive reference with every endpoint, schema, and example request:

- Running the API with `dotnet run` (see the README): <http://localhost:5101/scalar>
- The document itself: <http://localhost:5101/openapi/v1.json>

The gateway does not route `/scalar` or `/openapi`, so use the management API's own port.

## Conventions

- **Authentication:** `POST /api/v1/auth/login` returns a short-lived JWT access token (15 minutes) and sets an
  httpOnly refresh cookie scoped to `/api/v1/auth`. Send the token as `Authorization: Bearer <token>`.
  `POST /api/v1/auth/refresh` rotates the cookie and returns a new token.
- **Roles:** Viewers read everything and can use the targeting preview. Editors also change flags, variations,
  targeting, toggles, and schedules outside protected environments. Admins can do everything, including protected
  environments, SDK keys, environments, projects, and users. Changes to a protected environment need an admin and a
  comment.
- **Errors** are RFC 9457 ProblemDetails. Validation errors (400) carry an `errors` map keyed by field path, such as
  `config.rules[0].clauses[1].values`. 409 means a concurrency conflict (targeting saves send `expectedVersion`, and
  the 409 carries `currentVersion`) or a business conflict; 429 means rate limited.
- **Pagination:** `?page=1&pageSize=25` (at most 100) returns `{ items, page, pageSize, totalCount }`.
- **Creates** return `201 Created` with a `Location` header.
- **Audit:** every change writes an audit entry in the same database transaction.

## Management API endpoints

`{p}` is a project key, `{e}` an environment key, `{f}` a flag key. All paths start with `/api/v1`.

| Area | Endpoints | Role |
|---|---|---|
| Meta | `GET /meta` (version and commit) | anyone |
| Auth | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`, `POST /auth/change-password` | anyone (signed in for `me` and `change-password`) |
| Users | `GET /users`, `GET /users/{id}`, `POST /users` (returns a temporary password once), `PATCH /users/{id}`, `POST /users/{id}/reset-password` | Admin |
| Projects | `GET /projects`, `GET /projects/{p}`; `POST /projects`, `PATCH /projects/{p}`, `DELETE /projects/{p}` (body `{ confirmKey }`) | Viewer; Admin to change |
| Environments | `GET /projects/{p}/environments`, `GET .../environments/{e}`; `POST`, `PATCH`, `DELETE` (body `{ confirmKey }`) | Viewer; Admin to change |
| SDK keys | `GET .../environments/{e}/sdk-keys`, `GET .../sdk-keys/{id}`, `POST` (returns the key once), `DELETE .../sdk-keys/{id}` (revokes) | Admin |
| Flags | `GET /projects/{p}/flags?search=&tag=&includeArchived=&page=&pageSize=`, `GET .../flags/{f}`; `POST`, `PATCH .../flags/{f}`, `PUT .../flags/{f}/variations`, `POST .../flags/{f}/archive`, `POST .../flags/{f}/restore` | Viewer; Editor to change |
| Flags | `DELETE /projects/{p}/flags/{f}` (archived flags only, body `{ confirmKey }`) | Admin |
| Targeting | `GET .../flags/{f}/environments/{e}`; `PUT` (body `{ config, expectedVersion, comment? }`); `POST .../toggle` (body `{ enabled, expectedVersion?, comment? }`) | Viewer; Editor to change (Admin in protected environments) |
| Preview | `POST .../flags/{f}/environments/{e}/evaluate-preview` (body `{ context, draftConfig? }`, not counted as usage) | Viewer |
| Schedules | `GET .../environments/{e}/scheduled-changes`, `GET .../scheduled-changes/{id}`; `POST` (body `{ executeAt, action, payload? }`), `POST .../release-plan` (body `{ steps: [{ executeAt, weights }], bucketBy? }`), `DELETE .../scheduled-changes/{id}` (cancels) | Viewer; Editor to change (Admin in protected environments) |
| Usage | `GET .../flags/{f}/environments/{e}/usage?from=&to=&granularity=hour\|day` (at most 90 days) | Viewer |
| Stale flags | `GET /projects/{p}/stale-flags` | Viewer |
| Audit | `GET /audit?projectKey=&flagKey=&environmentKey=&actorId=&action=&from=&to=&page=&pageSize=` (newest first) | Viewer |

## Evaluation API

SDKs authenticate with an SDK key (`ffk_...`) as `Authorization: Bearer <key>`. An SDK key identifies one
environment. Keys are public identifiers (browsers ship them), so responses never contain targeting rules, target
lists, or any other configuration, only served values.

| Method | Path | Returns |
|---|---|---|
| POST | `/sdk/v1/evaluate` | Body `{ context }`. `{ environmentVersion, flags: { "<flagKey>": { value, variationId, reason } } }` for every non-archived flag. |
| POST | `/sdk/v1/evaluate/{flagKey}` | Body `{ context }`. One result, or 404 when the flag does not exist or is archived. |
| WebSocket | `/sdk/hubs/flags` | A SignalR hub (WebSockets only, no negotiation request; pass the key as `access_token`). The server sends `FlagsChanged { environmentVersion }` when the environment's flags change. |

Limits: request bodies up to 32 KB; a token bucket of 20 requests per second per SDK key with a burst of 100 (429 with
`Retry-After` beyond that). Cross-origin browser access is off unless `Cors__AllowedOrigins` lists origins (same-origin
apps behind the gateway need none).

Example:

```sh
curl -s http://localhost:8080/sdk/v1/evaluate \
  -H 'Authorization: Bearer ffk_local_demo_key_for_development_only_000' \
  -H 'Content-Type: application/json' \
  -d '{"context":{"key":"bob","attributes":{"plan":"free"}}}'
```

The [JavaScript SDK](../src/frontend/packages/sdk/README.md) wraps all of this, including the live connection and its
polling fallback. [Evaluation semantics](evaluation.md) explains how a value is chosen.
