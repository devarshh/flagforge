import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createClient,
  type ClientOptions,
  type ConnectionState,
  type FlagForgeClient,
} from '../src/index.js';
import { createFakeFetch, createFakeHubFactory, served, silentLogger } from './fakes.js';

const baseUrl = 'https://flags.example.test/';
const context = { key: 'user-1', attributes: { plan: 'premium' } };

let clients: FlagForgeClient[] = [];

function create(options: Partial<ClientOptions> & Pick<ClientOptions, 'fetch'>): FlagForgeClient {
  const client = createClient({
    baseUrl,
    sdkKey: 'ffk_test',
    context,
    logger: silentLogger,
    streaming: false,
    ...options,
  });
  clients.push(client);
  return client;
}

/** Lets pending promise callbacks run without moving fake time forward. */
const flush = () => vi.advanceTimersByTimeAsync(0);

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(async () => {
  await Promise.all(clients.map((client) => client.close()));
  clients = [];
  vi.useRealTimers();
});

describe('evaluation requests', () => {
  it('posts the context with the SDK key to the evaluate endpoint', async () => {
    const { fetch, calls } = createFakeFetch();
    create({ fetch });
    await flush();

    expect(calls).toHaveLength(1);
    expect(calls[0]!.url).toBe('https://flags.example.test/sdk/v1/evaluate');
    expect(calls[0]!.headers.Authorization).toBe('Bearer ffk_test');
    expect(calls[0]!.context).toEqual(context);
  });

  it('rejects missing options early', () => {
    const { fetch } = createFakeFetch();
    expect(() => createClient({ baseUrl: '', sdkKey: 'ffk_x', context, fetch })).toThrow(/baseUrl/);
    expect(() => createClient({ baseUrl, sdkKey: '', context, fetch })).toThrow(/sdkKey/);
    expect(() => createClient({ baseUrl, sdkKey: 'ffk_x', context: { key: '' }, fetch })).toThrow(
      /context.key/,
    );
  });
});

describe('typed getters', () => {
  it('return defaults until values arrive, then the served values', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch });
    expect(client.getBoolean('enabled', true)).toBe(true);
    expect(client.isReady).toBe(false);

    calls[0]!.respond({
      enabled: served(false, 'false'),
      text: served('Hello'),
      limit: served(5),
      theme: served({ accent: '#5E7F4F', rounded: true }),
    });
    await client.ready();

    expect(client.isReady).toBe(true);
    expect(client.getBoolean('enabled', true)).toBe(false);
    expect(client.getString('text', 'fallback')).toBe('Hello');
    expect(client.getNumber('limit', 3)).toBe(5);
    expect(client.getJson('theme', { accent: '#000', rounded: false })).toEqual({
      accent: '#5E7F4F',
      rounded: true,
    });
  });

  it('return the default for missing flags, wrong types, and server errors', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch });
    calls[0]!.respond({
      text: served('Hello'),
      broken: { value: null, reason: { kind: 'ERROR' } },
    });
    await client.ready();

    expect(client.getBoolean('text', true)).toBe(true);
    expect(client.getNumber('text', 7)).toBe(7);
    expect(client.getJson('text', { a: 1 })).toEqual({ a: 1 });
    expect(client.getString('missing', 'fallback')).toBe('fallback');
    expect(client.getDetail('missing', 'fallback')).toEqual({
      value: 'fallback',
      reason: { kind: 'FLAG_NOT_FOUND' },
    });
    expect(client.getDetail('text', 0)).toEqual({ value: 0, reason: { kind: 'ERROR' } });
    expect(client.getDetail('broken', false)).toEqual({ value: false, reason: { kind: 'ERROR' } });
  });

  it('expose the variation and reason in getDetail and getAll', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch });
    const reason = { kind: 'RULE_MATCH', ruleId: 'r_paid', ruleIndex: 1, inRollout: true };
    calls[0]!.respond({ layout: { value: true, variationId: 'true', reason } });
    await client.ready();

    expect(client.getDetail('layout', false)).toEqual({ value: true, variationId: 'true', reason });
    expect(Object.keys(client.getAll())).toEqual(['layout']);
  });
});

describe('ready', () => {
  it('resolves after the timeout with defaults, emits error, and never rejects', async () => {
    const { fetch } = createFakeFetch();
    const client = create({ fetch, readyTimeoutMs: 1000 });
    const errors: Error[] = [];
    client.on('error', (error) => errors.push(error));
    let resolved = false;
    void client.ready().then(() => {
      resolved = true;
    });

    await vi.advanceTimersByTimeAsync(999);
    expect(resolved).toBe(false);
    await vi.advanceTimersByTimeAsync(1);

    expect(resolved).toBe(true);
    expect(client.isReady).toBe(false);
    expect(errors[0]?.message).toMatch(/did not respond within 1000 ms/);
    expect(client.getBoolean('anything', true)).toBe(true);
  });

  it('emits ready once, on the first evaluation', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    const ready = vi.fn();
    client.on('ready', ready);

    calls[0]!.respond({ a: served(true) });
    await flush();
    await vi.advanceTimersByTimeAsync(1000);
    calls[1]!.respond({ a: served(false) });
    await flush();

    expect(ready).toHaveBeenCalledTimes(1);
  });
});

describe('change events', () => {
  it('report exactly the keys whose values changed, comparing JSON deeply', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    const changes: string[][] = [];
    client.on('change', (keys) => changes.push(keys));

    calls[0]!.respond({
      toggle: served(false),
      theme: served({ accent: 'green', sizes: [1, 2] }),
      gone: served('x'),
    });
    await flush();
    await vi.advanceTimersByTimeAsync(1000);
    calls[1]!.respond({ toggle: served(true), theme: served({ sizes: [1, 2], accent: 'green' }) });
    await flush();

    expect(changes).toEqual([
      ['toggle', 'theme', 'gone'],
      ['toggle', 'gone'],
    ]);
  });

  it('keep the same object for unchanged flags', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    calls[0]!.respond({ theme: served({ accent: 'green' }) });
    await flush();
    const before = client.getEvaluation('theme');

    await vi.advanceTimersByTimeAsync(1000);
    calls[1]!.respond({ theme: served({ accent: 'green' }) });
    await flush();

    expect(client.getEvaluation('theme')).toBe(before);
  });

  it('keep previous values and report an error when a request fails', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    const errors: Error[] = [];
    client.on('error', (error) => errors.push(error));
    calls[0]!.respond({ toggle: served(true) });
    await flush();

    await vi.advanceTimersByTimeAsync(1000);
    calls[1]!.fail(503);
    await flush();

    expect(errors[0]?.message).toMatch(/HTTP 503/);
    expect(client.getBoolean('toggle', false)).toBe(true);
    expect(client.connectionState).toBe('offline');
  });
});

describe('ordering', () => {
  it('ignores a response that arrives after a newer one', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    calls[0]!.respond({ banner: served('first') });
    await flush();
    await vi.advanceTimersByTimeAsync(1000);
    await vi.advanceTimersByTimeAsync(1000);
    expect(calls).toHaveLength(3);

    calls[2]!.respond({ banner: served('newest') });
    await flush();
    calls[1]!.respond({ banner: served('stale') });
    await flush();

    expect(client.getString('banner', '')).toBe('newest');
  });

  it('identify aborts the in-flight request and evaluates the new context', async () => {
    const { fetch, calls } = createFakeFetch();
    const client = create({ fetch, pollIntervalMs: 1000 });
    calls[0]!.respond({ plan: served('premium') });
    await flush();
    await vi.advanceTimersByTimeAsync(1000);
    const inFlight = calls[1]!;

    const identified = client.identify({ key: 'user-2', attributes: { plan: 'free' } });
    await flush();

    expect(inFlight.signal?.aborted).toBe(true);
    expect(calls[2]!.context).toEqual({ key: 'user-2', attributes: { plan: 'free' } });
    calls[2]!.respond({ plan: served('free') });
    await identified;
    expect(client.getString('plan', '')).toBe('free');
    expect(client.context.key).toBe('user-2');

    inFlight.respond({ plan: served('premium') });
    await flush();
    expect(client.getString('plan', '')).toBe('free');
  });
});

describe('streaming', () => {
  it('connects with WebSockets to the hub and debounces change notifications', async () => {
    const { fetch, calls } = createFakeFetch();
    const { factory, hubs } = createFakeHubFactory();
    const client = create({ fetch, streaming: true, hubConnectionFactory: factory });
    await flush();

    const hub = hubs[0]!;
    expect(hub.url).toBe('https://flags.example.test/sdk/hubs/flags');
    expect(hub.accessTokenFactory()).toBe('ffk_test');
    expect(client.connectionState).toBe('live');
    const settled = calls.length;

    hub.send('FlagsChanged', { environmentVersion: 2 });
    await vi.advanceTimersByTimeAsync(100);
    hub.send('FlagsChanged', { environmentVersion: 3 });
    await vi.advanceTimersByTimeAsync(100);
    hub.send('FlagsChanged', { environmentVersion: 4 });
    await vi.advanceTimersByTimeAsync(249);
    expect(calls).toHaveLength(settled);

    await vi.advanceTimersByTimeAsync(1);
    expect(calls).toHaveLength(settled + 1);
  });

  it('refetches after reconnecting and reports connection states', async () => {
    const { fetch, calls } = createFakeFetch();
    const { factory, hubs } = createFakeHubFactory();
    const client = create({ fetch, streaming: true, hubConnectionFactory: factory });
    const states: ConnectionState[] = [];
    client.on('connection', (state) => states.push(state));
    await flush();
    const settled = calls.length;

    hubs[0]!.simulateReconnecting();
    hubs[0]!.simulateReconnected();
    await flush();

    expect(states).toEqual(['live', 'connecting', 'live']);
    expect(calls).toHaveLength(settled + 1);
  });

  it('falls back to polling when the connection cannot open, then reconnects in the background', async () => {
    const { fetch, calls } = createFakeFetch();
    const { factory, hubs } = createFakeHubFactory((hub) => {
      hub.failNextStarts = 5;
    });
    const client = create({
      fetch,
      streaming: true,
      pollIntervalMs: 1000,
      hubConnectionFactory: factory,
    });
    const states: ConnectionState[] = [];
    client.on('connection', (state) => states.push(state));

    // The retry schedule is 0, 2 s, 5 s, 10 s, and 30 s before giving up.
    await vi.advanceTimersByTimeAsync(2000 + 5000 + 10000 + 30000);
    expect(hubs[0]!.starts).toBe(5);
    expect(client.connectionState).toBe('polling');
    expect(states).toEqual(['polling']);

    const beforePoll = calls.length;
    await vi.advanceTimersByTimeAsync(1000);
    expect(calls.length).toBe(beforePoll + 1);

    // The background retry succeeds 30 s after falling back; polling then stops.
    await vi.advanceTimersByTimeAsync(29_000);
    expect(client.connectionState).toBe('live');
    const afterReconnect = calls.length;
    await vi.advanceTimersByTimeAsync(5000);
    expect(calls.length).toBe(afterReconnect);
  });

  it('polls when the live connection closes for good', async () => {
    const { fetch } = createFakeFetch();
    const { factory, hubs } = createFakeHubFactory();
    const client = create({ fetch, streaming: true, hubConnectionFactory: factory });
    await flush();

    hubs[0]!.simulateClose();

    expect(client.connectionState).toBe('polling');
  });

  it('close stops the connection, timers, and requests', async () => {
    const { fetch, calls } = createFakeFetch();
    const { factory, hubs } = createFakeHubFactory();
    const client = create({ fetch, streaming: true, hubConnectionFactory: factory });
    await flush();

    await client.close();

    expect(hubs[0]!.stops).toBe(1);
    expect(client.connectionState).toBe('offline');
    expect(calls.every((call) => call.signal?.aborted)).toBe(true);
    const settled = calls.length;
    hubs[0]!.send('FlagsChanged', { environmentVersion: 9 });
    await vi.advanceTimersByTimeAsync(60_000);
    expect(calls).toHaveLength(settled);
  });
});
