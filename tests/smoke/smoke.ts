/**
 * FlagForge smoke test. Checks a running deployment end to end through its gateway.
 *
 *   readonly: the gateway, dashboard, and demo answer, and the demo SDK key evaluates the seeded flags.
 *   full:     also signs in, creates a flag, watches for FlagsChanged over SignalR while turning the flag on, checks the
 *             new value and the audit log, and archives the flag.
 *
 * Settings (environment variables): BASE_URL, ADMIN_EMAIL, ADMIN_PASSWORD, DEMO_SDK_KEY, SMOKE_MODE (full | readonly).
 * The defaults match the development values in .env.example. Exits non-zero with a message on the first failure.
 */
import { HttpTransportType, HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

const settings = {
  baseUrl: (process.env.BASE_URL ?? 'http://localhost:8080').replace(/\/+$/, ''),
  adminEmail: process.env.ADMIN_EMAIL ?? 'admin@flagforge.local',
  adminPassword: process.env.ADMIN_PASSWORD ?? 'FlagForge!2026',
  demoSdkKey: process.env.DEMO_SDK_KEY ?? 'ffk_local_demo_key_for_development_only_000',
  mode: process.env.SMOKE_MODE ?? 'full',
};

const projectKey = 'acme-coffee';
const environmentKey = 'development';
const seededFlags = ['promo-banner', 'promo-banner-text', 'new-product-layout', 'checkout-button-color', 'max-cart-items', 'store-theme'];
const gatewayTimeoutMs = 180_000;
const flagsChangedTimeoutMs = 5_000;

interface ServedFlag {
  value: unknown;
  variationId?: string;
  reason: { kind: string };
}

interface EvaluateResponse {
  environmentVersion: number;
  flags: Record<string, ServedFlag>;
}

class SmokeFailure extends Error {}

function fail(message: string): never {
  throw new SmokeFailure(message);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function step<T>(name: string, run: () => Promise<T>): Promise<T> {
  const started = Date.now();
  try {
    const result = await run();
    console.log(`ok    ${name} (${Date.now() - started} ms)`);
    return result;
  } catch (error) {
    console.error(`FAIL  ${name}`);
    throw error;
  }
}

async function send(method: string, path: string, options: { bearer?: string; body?: unknown } = {}): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json, text/html' };
  if (options.bearer) {
    headers.Authorization = `Bearer ${options.bearer}`;
  }

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  return fetch(`${settings.baseUrl}${path}`, {
    method,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    redirect: 'manual',
    signal: AbortSignal.timeout(15_000),
  });
}

/** Sends a request and returns the JSON body, failing with the status and the start of the body otherwise. */
async function sendJson<T>(method: string, path: string, options: { bearer?: string; body?: unknown } = {}): Promise<T> {
  const response = await send(method, path, options);
  const text = await response.text();
  if (!response.ok) {
    fail(`${method} ${path} returned HTTP ${response.status}: ${text.slice(0, 300)}`);
  }

  return (text ? JSON.parse(text) : undefined) as T;
}

async function waitForGateway(): Promise<string> {
  const deadline = Date.now() + gatewayTimeoutMs;
  let lastProblem = 'no response yet';
  while (Date.now() < deadline) {
    try {
      const response = await send('GET', '/api/v1/meta');
      if (response.ok) {
        const meta = (await response.json()) as { version?: string; commit?: string };
        return `${meta.version ?? '?'} (${meta.commit ?? '?'})`;
      }

      lastProblem = `HTTP ${response.status}`;
    } catch (error) {
      lastProblem = error instanceof Error ? error.message : String(error);
    }

    await sleep(2_000);
  }

  return fail(`The gateway did not answer /api/v1/meta within ${gatewayTimeoutMs / 1000} s (last: ${lastProblem}).`);
}

async function expectHtml(path: string): Promise<void> {
  const response = await send('GET', path);
  const body = await response.text();
  if (response.status !== 200 || !response.headers.get('content-type')?.includes('text/html') || !body.includes('<div id="root">')) {
    fail(`GET ${path} should return the app's HTML, but returned HTTP ${response.status} (${response.headers.get('content-type') ?? 'no content type'}).`);
  }
}

function evaluate(contextKey: string): Promise<EvaluateResponse> {
  return sendJson<EvaluateResponse>('POST', '/sdk/v1/evaluate', {
    bearer: settings.demoSdkKey,
    body: { context: { key: contextKey, attributes: { plan: 'free' } } },
  });
}

/** Resolves on the next FlagsChanged message, or fails after the timeout. */
function nextFlagsChanged(hub: HubConnection, timeoutMs: number): Promise<number> {
  return new Promise((resolve, reject) => {
    const started = Date.now();
    const timer = setTimeout(() => {
      hub.off('FlagsChanged', handler);
      reject(new SmokeFailure(`No FlagsChanged message arrived within ${timeoutMs / 1000} s of turning the flag on.`));
    }, timeoutMs);
    const handler = () => {
      clearTimeout(timer);
      hub.off('FlagsChanged', handler);
      resolve(Date.now() - started);
    };
    hub.on('FlagsChanged', handler);
  });
}

async function readonlyChecks(): Promise<void> {
  const version = await step(`gateway answers at ${settings.baseUrl}`, waitForGateway);
  console.log(`      FlagForge ${version}`);
  await step('dashboard serves its app at /', () => expectHtml('/'));
  await step('demo serves its app at /demo/', () => expectHtml('/demo/'));
  await step('demo SDK key evaluates the seeded flags', async () => {
    const result = await evaluate('smoke-reader');
    const missing = seededFlags.filter((key) => !(key in result.flags));
    if (missing.length > 0) {
      fail(`Evaluation is missing seeded flags: ${missing.join(', ')}.`);
    }
  });
}

async function fullChecks(): Promise<void> {
  const token = await step('admin signs in', async () => {
    const session = await sendJson<{ accessToken: string }>('POST', '/api/v1/auth/login', {
      body: { email: settings.adminEmail, password: settings.adminPassword },
    });
    return session.accessToken;
  });

  const flagKey = `smoke-${Date.now()}`;
  const flagPath = `/api/v1/projects/${projectKey}/flags/${flagKey}`;
  let created = false;
  const hub = new HubConnectionBuilder()
    .withUrl(`${settings.baseUrl}/sdk/hubs/flags`, {
      accessTokenFactory: () => settings.demoSdkKey,
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    .configureLogging(LogLevel.Warning)
    .build();

  try {
    await step(`create flag ${flagKey}`, async () => {
      await sendJson('POST', `/api/v1/projects/${projectKey}/flags`, {
        bearer: token,
        body: { key: flagKey, name: `Smoke test ${new Date().toISOString()}`, type: 'boolean', tags: ['smoke'], isPermanent: false },
      });
      created = true;
    });

    await step('SignalR connects with the demo SDK key', () => hub.start());

    await step('new flag evaluates to false (off)', async () => {
      const served = (await evaluate('smoke-user')).flags[flagKey];
      if (served?.value !== false) {
        fail(`Expected ${flagKey} to be false while off, got ${JSON.stringify(served)}.`);
      }
    });

    await step(`turning the flag on sends FlagsChanged within ${flagsChangedTimeoutMs / 1000} s`, async () => {
      const changed = nextFlagsChanged(hub, flagsChangedTimeoutMs);
      await sendJson('POST', `${flagPath}/environments/${environmentKey}/toggle`, { bearer: token, body: { enabled: true } });
      const elapsed = await changed;
      console.log(`      FlagsChanged after ${elapsed} ms`);
    });

    await step('flag now evaluates to true', async () => {
      const served = (await evaluate('smoke-user')).flags[flagKey];
      if (served?.value !== true) {
        fail(`Expected ${flagKey} to be true after turning it on, got ${JSON.stringify(served)}.`);
      }
    });

    await step('audit log records the change', async () => {
      const audit = await sendJson<{ items: { action: string; resourceKey: string }[] }>(
        'GET',
        `/api/v1/audit?projectKey=${projectKey}&flagKey=${flagKey}&action=flag.toggled`,
        { bearer: token },
      );
      if (!audit.items.some((entry) => entry.resourceKey === `${projectKey}/${flagKey}@${environmentKey}`)) {
        fail(`The audit log has no flag.toggled entry for ${flagKey} in ${environmentKey}.`);
      }
    });
  } finally {
    await hub.stop().catch(() => undefined);
    if (created) {
      await step(`archive ${flagKey} (clean up)`, () => sendJson('POST', `${flagPath}/archive`, { bearer: token }));
    }
  }
}

async function main(): Promise<void> {
  if (settings.mode !== 'full' && settings.mode !== 'readonly') {
    fail(`SMOKE_MODE must be "full" or "readonly", not "${settings.mode}".`);
  }

  console.log(`FlagForge smoke test (${settings.mode}) against ${settings.baseUrl}`);
  await readonlyChecks();
  if (settings.mode === 'full') {
    await fullChecks();
  }

  console.log('Smoke test passed.');
}

main().catch((error: unknown) => {
  console.error(error instanceof SmokeFailure ? error.message : error);
  console.error('Smoke test failed.');
  process.exitCode = 1;
});
