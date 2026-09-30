/** Where the store keeps an SDK key someone pasted into the settings dialog. */
export const sdkKeyStorageKey = 'acme-coffee-sdk-key';

const maxKeyLength = 128;

export function isSdkKey(value: string): boolean {
  return (
    value.startsWith('ffk_') &&
    value.length > 4 &&
    value.length <= maxKeyLength &&
    !/\s/.test(value)
  );
}

export function readStoredKey(
  storage: Pick<Storage, 'getItem'> | undefined = safeStorage(),
): string | null {
  try {
    const value = storage?.getItem(sdkKeyStorageKey)?.trim();
    return value && isSdkKey(value) ? value : null;
  } catch {
    return null;
  }
}

export function storeKey(
  key: string | null,
  storage: Pick<Storage, 'setItem' | 'removeItem'> | undefined = safeStorage(),
): void {
  try {
    if (key === null) {
      storage?.removeItem(sdkKeyStorageKey);
    } else {
      storage?.setItem(sdkKeyStorageKey, key);
    }
  } catch {
    // Private windows can refuse storage; the key still works for this visit.
  }
}

/** The key the deployment configured: `/demo/config.json`, which nginx fills from `DEMO_SDK_KEY`. */
export async function readConfiguredKey(
  fetchImpl: typeof fetch = fetch,
  baseUrl = import.meta.env.BASE_URL,
): Promise<string | null> {
  try {
    const response = await fetchImpl(`${baseUrl}config.json`, {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });
    // The Vite dev server answers unknown paths with index.html, so check the content type as well.
    if (!response.ok || !response.headers.get('content-type')?.includes('json')) {
      return null;
    }

    const body = (await response.json()) as { sdkKey?: unknown };
    const key = typeof body.sdkKey === 'string' ? body.sdkKey.trim() : '';
    return isSdkKey(key) ? key : null;
  } catch {
    return null;
  }
}

export interface KeySources {
  stored: string | null;
  configured: string | null;
  development: string | undefined;
}

/** A key pasted in settings wins, then the deployment's key, then the dev server's fallback. */
export function chooseSdkKey({ stored, configured, development }: KeySources): string | null {
  const fallback = development?.trim();
  return stored ?? configured ?? (fallback && isSdkKey(fallback) ? fallback : null);
}

function safeStorage(): Storage | undefined {
  try {
    return window.localStorage;
  } catch {
    return undefined;
  }
}
