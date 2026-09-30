/** Readable names for audit action codes, in the dashboard's wording. */
export const auditActionLabels: Record<string, string> = {
  'project.created': 'Project created',
  'project.updated': 'Project updated',
  'project.deleted': 'Project deleted',
  'environment.created': 'Environment created',
  'environment.updated': 'Environment updated',
  'environment.deleted': 'Environment deleted',
  'sdkkey.created': 'SDK key created',
  'sdkkey.revoked': 'SDK key revoked',
  'flag.created': 'Flag created',
  'flag.updated': 'Flag details updated',
  'flag.variations.updated': 'Variations updated',
  'flag.archived': 'Flag archived',
  'flag.restored': 'Flag restored',
  'flag.deleted': 'Flag deleted',
  'flag.targeting.updated': 'Targeting updated',
  'flag.toggled': 'Flag turned on or off',
  'schedule.created': 'Change scheduled',
  'schedule.cancelled': 'Scheduled change cancelled',
  'schedule.executed': 'Scheduled change applied',
  'schedule.failed': 'Scheduled change failed',
  'user.created': 'User created',
  'user.updated': 'User updated',
  'user.deactivated': 'User deactivated',
  'user.password_reset': 'Password reset',
};

export function auditActionLabel(action: string): string {
  return auditActionLabels[action] ?? action;
}
