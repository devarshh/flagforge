import type { Environment, Role } from '../../api/types';

/**
 * Why a person cannot change a flag in an environment, or null when they can. Mirrors the server's rules: Viewers
 * change nothing, Editors change non-protected environments, Admins change everything; archived flags are frozen.
 */
export function changeBlockedReason(
  role: Role,
  environment: Pick<Environment, 'name' | 'isProtected'>,
  archived = false,
): string | null {
  if (role === 'viewer') {
    return 'Viewers cannot change flags. Ask an admin for the Editor role.';
  }

  if (archived) {
    return 'This flag is archived. Restore it before changing it.';
  }

  if (environment.isProtected && role !== 'admin') {
    return `${environment.name} is protected. Only admins can change it.`;
  }

  return null;
}

/** Whether a person can create and edit flags at all (Editor or Admin). */
export function canEditFlags(role: Role): boolean {
  return role !== 'viewer';
}
