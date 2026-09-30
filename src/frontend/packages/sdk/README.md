# @flagforge/sdk

JavaScript SDK for [FlagForge](../../../../README.md) feature flags, with optional React bindings. It evaluates every
flag for one context in a single request and keeps the values current over a WebSocket, so a flag change reaches the
page in about a second without a reload.

The guiding rule: **an application must never break because flags are unavailable.** Every getter takes a default and
returns it until values arrive, when the flag does not exist, or when the served value has the wrong type.

## Install

```sh
npm install @flagforge/sdk
```

React is an optional peer dependency (18 or 19); install it only if you use `@flagforge/sdk/react`.

## Quick start

```ts
import { createClient } from '@flagforge/sdk';

const client = createClient({
  baseUrl: 'https://flags.example.com', // the origin that serves /sdk
  sdkKey: 'ffk_...', // from the environment's SDK keys page
  context: { key: 'user-123', attributes: { plan: 'premium', email: 'sam@example.com' } },
});

await client.ready(); // never rejects; resolves with defaults after readyTimeoutMs

if (client.getBoolean('new-checkout', false)) {
  showNewCheckout();
}

const unsubscribe = client.on('change', (changedKeys) => {
  if (changedKeys.includes('new-checkout')) rerender();
});
```

## Options

| Option                 | Default    | Description                                                                                  |
| ---------------------- | ---------- | -------------------------------------------------------------------------------------------- |
| `baseUrl`              | (required) | Origin serving `/sdk`, for example `window.location.origin`.                                 |
| `sdkKey`               | (required) | An `ffk_` key. SDK keys identify an environment and are public, not secret.                  |
| `context`              | (required) | `{ key, attributes? }`. Attribute values are strings, numbers, booleans, or arrays of those. |
| `streaming`            | `true`     | Receive changes over a WebSocket. When `false`, the client polls.                            |
| `pollIntervalMs`       | `30000`    | Poll interval while streaming is unavailable or disabled.                                    |
| `readyTimeoutMs`       | `5000`     | `ready()` resolves after this long even without a response.                                  |
| `logger`               | console    | `{ debug?, warn?, error? }`. Pass `{}` to silence the SDK.                                   |
| `fetch`                | `fetch`    | A custom fetch implementation (tests, proxies).                                              |
| `hubConnectionFactory` | SignalR    | Replaces the WebSocket connection (tests).                                                   |

## API

| Member                           | Description                                                                            |
| -------------------------------- | -------------------------------------------------------------------------------------- |
| `ready()`                        | Resolves after the first evaluation or after `readyTimeoutMs`. Never rejects.          |
| `getBoolean/getString/getNumber` | Typed values with a default.                                                           |
| `getJson<T>(key, default)`       | A JSON object or array flag.                                                           |
| `getDetail(key, default)`        | `{ value, variationId?, reason }`, for example `{ kind: 'RULE_MATCH', ruleIndex: 1 }`. |
| `getAll()`                       | Every flag as last served.                                                             |
| `identify(context)`              | Switches context (for example after sign-in); cancels any in-flight request.           |
| `on(event, listener)`            | Events below; returns an unsubscribe function.                                         |
| `isReady`, `connectionState`     | Current state.                                                                         |
| `close()`                        | Closes the connection and stops all timers.                                            |

Events: `change` (the keys whose values changed, compared deeply), `ready`, `error`, and `connection` with one of
`connecting`, `live`, `polling`, or `offline`.

Reason kinds: `OFF`, `TARGET_MATCH`, `RULE_MATCH`, `FALLTHROUGH`, `FLAG_NOT_FOUND`, `ERROR`.

## React

```tsx
import { createClient } from '@flagforge/sdk';
import { FlagForgeProvider, useFlag, useConnectionState } from '@flagforge/sdk/react';

const client = createClient({ baseUrl: window.location.origin, sdkKey, context: { key: userId } });

export function App() {
  return (
    <FlagForgeProvider client={client}>
      <Checkout />
    </FlagForgeProvider>
  );
}

function Checkout() {
  const newCheckout = useFlag('new-checkout', false);
  const connection = useConnectionState(); // 'live', 'polling', ...
  return newCheckout ? <NewCheckout /> : <ClassicCheckout />;
}
```

Hooks: `useFlag(key, default)`, `useFlagDetail(key, default)`, `useFlagsReady()`, `useConnectionState()`, and
`useFlagForgeClient()`. They are built on `useSyncExternalStore`; a component re-renders only when its own flag's
evaluation changes, and JSON values keep the same object identity while unchanged.

## How updates arrive

1. The client posts the context to `/sdk/v1/evaluate` and stores every result.
2. It opens a SignalR WebSocket to `/sdk/hubs/flags` (no negotiation request, so any server instance can take it).
3. When a flag changes, the server sends `FlagsChanged`. The client waits 250 ms for more notifications, re-evaluates
   once, and emits `change` with the keys whose values changed. Responses that arrive out of order are ignored.
4. If the WebSocket cannot open after retrying at 0 s, 2 s, 5 s, 10 s, and 30 s, the client polls every
   `pollIntervalMs` and keeps trying to reconnect in the background.

## Build and test

```sh
npm run build -w packages/sdk   # tsc: ESM and .d.ts in dist/
npm test -w packages/sdk        # Vitest
```
