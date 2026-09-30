import type { Role } from '../../api/types';

export const roleOptions: { role: Role; label: string; description: string }[] = [
  { role: 'viewer', label: 'Viewer', description: 'Reads everything and can use the test panel.' },
  {
    role: 'editor',
    label: 'Editor',
    description: 'Changes flags, targeting, and schedules outside protected environments.',
  },
  {
    role: 'admin',
    label: 'Admin',
    description: 'Everything, including protected environments, SDK keys, projects, and users.',
  },
];

export function roleLabel(role: Role): string {
  return roleOptions.find((option) => option.role === role)?.label ?? role;
}
