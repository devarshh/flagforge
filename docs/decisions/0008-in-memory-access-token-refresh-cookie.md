# 0008. In-memory access token plus a rotating httpOnly refresh cookie

## Context

The dashboard is a single-page app on the same origin as the API. Tokens in `localStorage` can be read by any script
that runs in the page (an XSS bug would leak them), while a session cookie for every request needs CSRF protection.

## Decision

- Sign-in returns a JWT access token (HS256, 15 minutes) that the dashboard keeps **only in memory**.
- It also sets a refresh cookie: 32 random bytes, stored hashed, `HttpOnly`, `SameSite=Strict`, and scoped to
  `Path=/api/v1/auth`, so it is sent to the auth endpoints only. `Secure` is on unless the deployment is plain HTTP.
- Every refresh rotates the cookie. Presenting a revoked refresh token revokes all of the user's sessions (reuse
  detection). Rotation is a conditional update, so of two concurrent refreshes only one wins.
- On load, and on a 401, the dashboard refreshes once; concurrent 401s share a single refresh.

## Consequences

- Scripts cannot read the refresh cookie, and a stolen access token expires in 15 minutes.
- API calls carry the bearer token, not the cookie, so they need no CSRF token; the cookie is `SameSite=Strict` and
  only reaches the auth endpoints.
- Reloading the page keeps the session without storing a token.
- The API must be same-site with the dashboard for the cookie to flow, which the gateway provides.
