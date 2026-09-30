import type { Environment } from '../../api/types';

/** Environments in their configured order (development, staging, production for new projects). */
export function sortEnvironments(environments: readonly Environment[]): Environment[] {
  return [...environments].sort(
    (left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name),
  );
}
