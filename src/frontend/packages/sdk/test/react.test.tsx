// @vitest-environment jsdom
import { act, cleanup, render, renderHook, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createClient, type FlagForgeClient } from '../src/index.js';
import {
  FlagForgeProvider,
  useConnectionState,
  useFlag,
  useFlagDetail,
  useFlagForgeClient,
  useFlagsReady,
} from '../src/react/index.js';
import { createFakeFetch, served, silentLogger } from './fakes.js';

let client: FlagForgeClient;
let calls: ReturnType<typeof createFakeFetch>['calls'];

beforeEach(() => {
  vi.useFakeTimers();
  const fake = createFakeFetch();
  calls = fake.calls;
  client = createClient({
    baseUrl: 'https://flags.example.test',
    sdkKey: 'ffk_test',
    context: { key: 'user-1' },
    streaming: false,
    pollIntervalMs: 1000,
    fetch: fake.fetch,
    logger: silentLogger,
  });
});

afterEach(async () => {
  cleanup();
  await client.close();
  vi.useRealTimers();
});

function wrapper({ children }: { children?: ReactNode }) {
  return <FlagForgeProvider client={client}>{children}</FlagForgeProvider>;
}

async function respond(index: number, flags: Record<string, unknown>) {
  await act(async () => {
    calls[index]!.respond(flags);
    await vi.advanceTimersByTimeAsync(0);
  });
}

async function poll() {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(1000);
  });
}

describe('useFlag', () => {
  it('renders the default, then the served value, and re-renders only for its own flag', async () => {
    const renders = { layout: 0, banner: 0 };
    function Layout() {
      renders.layout++;
      return <span>layout:{String(useFlag('new-layout', false))}</span>;
    }
    function Banner() {
      renders.banner++;
      return <span>banner:{useFlag('banner-text', 'Welcome')}</span>;
    }
    render(
      <>
        <Layout />
        <Banner />
      </>,
      { wrapper },
    );
    expect(screen.getByText('layout:false')).toBeTruthy();
    expect(screen.getByText('banner:Welcome')).toBeTruthy();

    await respond(0, { 'new-layout': served(true), 'banner-text': served('Autumn sale') });
    expect(screen.getByText('layout:true')).toBeTruthy();
    expect(screen.getByText('banner:Autumn sale')).toBeTruthy();
    const afterFirst = { ...renders };

    await poll();
    await respond(1, {
      'new-layout': served(false, 'false'),
      'banner-text': served('Autumn sale'),
    });

    expect(screen.getByText('layout:false')).toBeTruthy();
    expect(renders.layout).toBe(afterFirst.layout + 1);
    expect(renders.banner).toBe(afterFirst.banner);
  });

  it('keeps JSON values referentially stable while unchanged', async () => {
    const defaultTheme = { accent: '#000000' };
    const { result } = renderHook(() => useFlag('theme', defaultTheme), { wrapper });
    await respond(0, { theme: served({ accent: '#5E7F4F' }) });
    const first = result.current;

    await poll();
    await respond(1, { theme: served({ accent: '#5E7F4F' }) });

    expect(result.current).toBe(first);
    expect(first).toEqual({ accent: '#5E7F4F' });
  });
});

describe('other hooks', () => {
  it('useFlagDetail exposes the reason', async () => {
    const { result } = renderHook(() => useFlagDetail('limit', 3), { wrapper });
    expect(result.current.reason.kind).toBe('FLAG_NOT_FOUND');

    await respond(0, {
      limit: {
        value: 10,
        variationId: 'v_ten',
        reason: { kind: 'RULE_MATCH', ruleId: 'r1', ruleIndex: 0, inRollout: false },
      },
    });

    expect(result.current).toEqual({
      value: 10,
      variationId: 'v_ten',
      reason: { kind: 'RULE_MATCH', ruleId: 'r1', ruleIndex: 0, inRollout: false },
    });
  });

  it('useFlagsReady and useConnectionState follow the client', async () => {
    const { result } = renderHook(() => ({ ready: useFlagsReady(), state: useConnectionState() }), {
      wrapper,
    });
    expect(result.current).toEqual({ ready: false, state: 'polling' });

    await respond(0, {});
    expect(result.current.ready).toBe(true);

    await act(async () => {
      await client.close();
    });
    expect(result.current.state).toBe('offline');
  });

  it('useFlagForgeClient requires a provider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    expect(() => renderHook(() => useFlagForgeClient())).toThrow(/FlagForgeProvider/);
    consoleError.mockRestore();
  });
});
