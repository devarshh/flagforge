import { useState } from 'react';
import type { Environment } from '../../api/types';
import { errorMessage } from '../../api/errors';
import { useCurrentUser } from '../../auth/authContext';
import { SignalLamp } from '../../components/SignalLamp';
import { TypeToConfirmDialog } from '../../components/TypeToConfirmDialog';
import { useNotify } from '../../components/notify';
import { useToggleFlag } from './flagsApi';
import { changeBlockedReason } from './permissions';

export interface FlagToggleProps {
  projectKey: string;
  flagKey: string;
  environment: Environment;
  enabled: boolean;
  archived?: boolean;
}

/**
 * A flag's on/off lamp in one environment. Non-protected environments change at once; protected ones ask the
 * person to type the flag key and give a reason, which the audit log records.
 */
export function FlagToggle({
  projectKey,
  flagKey,
  environment,
  enabled,
  archived = false,
}: FlagToggleProps) {
  const user = useCurrentUser();
  const notify = useNotify();
  const toggle = useToggleFlag(projectKey);
  const [pendingValue, setPendingValue] = useState<boolean | null>(null);
  const blockedReason = changeBlockedReason(user.role, environment, archived);

  const apply = (next: boolean, comment?: string) =>
    toggle.mutate(
      { flagKey, environmentKey: environment.key, enabled: next, comment },
      {
        onSuccess: () => {
          setPendingValue(null);
          notify(`Flag turned ${next ? 'on' : 'off'} in ${environment.name}`);
        },
        onError: (error) => {
          if (pendingValue === null) {
            notify(errorMessage(error), 'error');
          }
        },
      },
    );

  return (
    <>
      <SignalLamp
        on={enabled}
        label={`${flagKey} in ${environment.name}`}
        disabled={blockedReason !== null}
        disabledReason={blockedReason ?? undefined}
        onToggle={(next) => {
          if (environment.isProtected) {
            toggle.reset();
            setPendingValue(next);
          } else {
            apply(next);
          }
        }}
      />
      <TypeToConfirmDialog
        open={pendingValue !== null}
        title={`Turn ${pendingValue ? 'on' : 'off'} ${flagKey} in ${environment.name}?`}
        description={`${environment.name} is protected. Type the flag key and say why you are making this change.`}
        confirmText={flagKey}
        confirmLabel={pendingValue ? 'Turn on' : 'Turn off'}
        destructive={pendingValue === false}
        requireComment
        pending={toggle.isPending}
        error={toggle.isError ? errorMessage(toggle.error) : null}
        onConfirm={(comment) => pendingValue !== null && apply(pendingValue, comment)}
        onClose={() => setPendingValue(null)}
      />
    </>
  );
}
