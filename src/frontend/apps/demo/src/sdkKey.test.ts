import { describe, expect, it, vi } from 'vitest';
import { chooseSdkKey, readConfiguredKey, readStoredKey, sdkKeyStorageKey } from './sdkKey';

function response(body: string, contentType: string, status = 200): Response {
  return new Response(body, { status, headers: { 'Content-Type': contentType } });
}

describe('chooseSdkKey', () => {
  it('prefers a pasted key, then the deployment key, then the dev server fallback', () => {
    expect(
      chooseSdkKey({ stored: 'ffk_pasted', configured: 'ffk_deployed', development: 'ffk_dev' }),
    ).toBe('ffk_pasted');
    expect(chooseSdkKey({ stored: null, configured: 'ffk_deployed', development: 'ffk_dev' })).toBe(
      'ffk_deployed',
    );
    expect(chooseSdkKey({ stored: null, configured: null, development: ' ffk_dev ' })).toBe(
      'ffk_dev',
    );
    expect(chooseSdkKey({ stored: null, configured: null, development: '' })).toBeNull();
    expect(chooseSdkKey({ stored: null, configured: null, development: 'not-a-key' })).toBeNull();
  });
});

describe('readConfiguredKey', () => {
  it('reads the key nginx serves at /demo/config.json', async () => {
    const fetch = vi.fn(() =>
      Promise.resolve(response('{"sdkKey":"ffk_from_nginx"}', 'application/json')),
    );

    await expect(readConfiguredKey(fetch, '/demo/')).resolves.toBe('ffk_from_nginx');
    expect(fetch).toHaveBeenCalledWith(
      '/demo/config.json',
      expect.objectContaining({ cache: 'no-store' }),
    );
  });

  it('ignores an empty key, an HTML fallback page, errors, and invalid keys', async () => {
    const answers = [
      response('{"sdkKey":""}', 'application/json'),
      response('<!doctype html><html></html>', 'text/html'),
      response('{}', 'application/json', 404),
      response('{"sdkKey":"not a key"}', 'application/json'),
    ];
    for (const answer of answers) {
      await expect(readConfiguredKey(() => Promise.resolve(answer), '/demo/')).resolves.toBeNull();
    }

    await expect(
      readConfiguredKey(() => Promise.reject(new TypeError('Failed to fetch')), '/demo/'),
    ).resolves.toBeNull();
  });
});

describe('readStoredKey', () => {
  it('returns a stored key only when it looks like an SDK key', () => {
    const storage = (value: string | null) => ({
      getItem: (name: string) => (name === sdkKeyStorageKey ? value : null),
    });
    expect(readStoredKey(storage('ffk_pasted'))).toBe('ffk_pasted');
    expect(readStoredKey(storage('garbage'))).toBeNull();
    expect(readStoredKey(storage(null))).toBeNull();
    expect(
      readStoredKey({
        getItem: () => {
          throw new Error('SecurityError');
        },
      }),
    ).toBeNull();
  });
});
