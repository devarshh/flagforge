import type { FlagEvaluation, FlagForgeClient } from '@flagforge/sdk';
import { useEffect, useState } from 'react';

const highlightMs = 1800;

/** Every flag as served, plus the keys that changed in the last couple of seconds (for a brief highlight). */
export function useFlagChanges(client: FlagForgeClient): {
  flags: Readonly<Record<string, FlagEvaluation>>;
  changed: ReadonlySet<string>;
} {
  const [flags, setFlags] = useState(() => client.getAll());
  const [changed, setChanged] = useState<ReadonlySet<string>>(() => new Set());

  useEffect(() => {
    const timers = new Set<ReturnType<typeof setTimeout>>();
    const unsubscribe = client.subscribe(() => setFlags(client.getAll()));
    const offChange = client.on('change', (keys) => {
      setChanged((current) => new Set([...current, ...keys]));
      const timer = setTimeout(() => {
        timers.delete(timer);
        setChanged((current) => new Set([...current].filter((key) => !keys.includes(key))));
      }, highlightMs);
      timers.add(timer);
    });
    return () => {
      unsubscribe();
      offChange();
      timers.forEach(clearTimeout);
    };
  }, [client]);

  return { flags, changed };
}
