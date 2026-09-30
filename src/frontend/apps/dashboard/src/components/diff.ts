export interface DiffLine {
  kind: 'same' | 'added' | 'removed';
  text: string;
}

/** JSON with keys sorted recursively, so two documents built in different orders diff cleanly. */
export function formatJson(value: unknown): string {
  return value === undefined || value === null ? '' : JSON.stringify(sortKeys(value), null, 2);
}

function sortKeys(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(sortKeys);
  }

  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, item]) => [key, sortKeys(item)]),
    );
  }

  return value;
}

/** A line diff based on the longest common subsequence of lines. */
export function diffLines(before: string, after: string): DiffLine[] {
  const a = before === '' ? [] : before.split('\n');
  const b = after === '' ? [] : after.split('\n');
  const lengths = Array.from({ length: a.length + 1 }, () => new Uint32Array(b.length + 1));
  const lcs = (i: number, j: number) => lengths[i]?.[j] ?? 0;
  for (let i = a.length - 1; i >= 0; i--) {
    const row = lengths[i]!;
    for (let j = b.length - 1; j >= 0; j--) {
      row[j] = a[i] === b[j] ? lcs(i + 1, j + 1) + 1 : Math.max(lcs(i + 1, j), lcs(i, j + 1));
    }
  }

  const lines: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      lines.push({ kind: 'same', text: a[i]! });
      i++;
      j++;
    } else if (lcs(i + 1, j) >= lcs(i, j + 1)) {
      lines.push({ kind: 'removed', text: a[i]! });
      i++;
    } else {
      lines.push({ kind: 'added', text: b[j]! });
      j++;
    }
  }

  lines.push(...a.slice(i).map((text) => ({ kind: 'removed' as const, text })));
  lines.push(...b.slice(j).map((text) => ({ kind: 'added' as const, text })));
  return lines;
}
