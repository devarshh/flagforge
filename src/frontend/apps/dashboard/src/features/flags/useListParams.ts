import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import type { FlagListParams } from './flagsApi';

export const pageSizeOptions = [25, 50, 100];

function positiveInteger(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

/** The flag list's search, tag, archived switch, and page live in the URL so they survive navigation and reloads. */
export function useListParams(): [FlagListParams, (changes: Partial<FlagListParams>) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const params = useMemo<FlagListParams>(() => {
    const pageSize = positiveInteger(searchParams.get('pageSize'), 25);
    return {
      search: searchParams.get('q') ?? '',
      tag: searchParams.get('tag'),
      includeArchived: searchParams.get('archived') === 'true',
      page: positiveInteger(searchParams.get('page'), 1),
      pageSize: pageSizeOptions.includes(pageSize) ? pageSize : 25,
    };
  }, [searchParams]);

  const update = useCallback(
    (changes: Partial<FlagListParams>) =>
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current);
          const set = (name: string, value: string | null, defaultValue: string) => {
            if (value === null || value === '' || value === defaultValue) {
              next.delete(name);
            } else {
              next.set(name, value);
            }
          };
          if ('search' in changes) set('q', changes.search ?? null, '');
          if ('tag' in changes) set('tag', changes.tag ?? null, '');
          if ('includeArchived' in changes)
            set('archived', String(changes.includeArchived ?? false), 'false');
          if ('pageSize' in changes) set('pageSize', String(changes.pageSize), '25');
          // Any filter change returns to the first page unless a page is given.
          set('page', 'page' in changes ? String(changes.page) : null, '1');
          return next;
        },
        { replace: true },
      ),
    [setSearchParams],
  );

  return [params, update];
}
