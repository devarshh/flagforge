import { createContext, useContext, useMemo, useSyncExternalStore, type ReactNode } from 'react';
import { resolveDetail } from '../detail.js';
import type { ConnectionState, FlagDetail, FlagForgeClient } from '../types.js';

const FlagForgeContext = createContext<FlagForgeClient | null>(null);

export interface FlagForgeProviderProps {
  readonly client: FlagForgeClient;
  readonly children?: ReactNode;
}

/** Makes a client available to the hooks below. Create the client once, outside render. */
export function FlagForgeProvider({ client, children }: FlagForgeProviderProps) {
  return <FlagForgeContext.Provider value={client}>{children}</FlagForgeContext.Provider>;
}

export function useFlagForgeClient(): FlagForgeClient {
  const client = useContext(FlagForgeContext);
  if (!client) {
    throw new Error('FlagForge hooks need a <FlagForgeProvider client={client}> above them.');
  }

  return client;
}

/**
 * A flag's value and reason. Re-renders only when this flag's evaluation changes: the client keeps the same object
 * for unchanged flags, which is what `useSyncExternalStore` compares.
 */
export function useFlagDetail<T>(key: string, defaultValue: T): FlagDetail<T> {
  const client = useFlagForgeClient();
  const evaluation = useSyncExternalStore(
    client.subscribe,
    () => client.getEvaluation(key),
    () => client.getEvaluation(key),
  );
  return useMemo(() => resolveDetail(evaluation, defaultValue), [evaluation, defaultValue]);
}

/** A flag's value, or `defaultValue` while unavailable. */
export function useFlag<T>(key: string, defaultValue: T): T {
  return useFlagDetail(key, defaultValue).value;
}

/** True once the first evaluation has arrived. */
export function useFlagsReady(): boolean {
  const client = useFlagForgeClient();
  return useSyncExternalStore(
    client.subscribe,
    () => client.isReady,
    () => client.isReady,
  );
}

export function useConnectionState(): ConnectionState {
  const client = useFlagForgeClient();
  return useSyncExternalStore(
    client.subscribe,
    () => client.connectionState,
    () => client.connectionState,
  );
}
