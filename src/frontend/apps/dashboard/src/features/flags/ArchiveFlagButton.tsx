import { Button } from '@mui/material';
import ArchiveOutlined from '@mui/icons-material/ArchiveOutlined';
import UnarchiveOutlined from '@mui/icons-material/UnarchiveOutlined';
import { useState } from 'react';
import { errorMessage } from '../../api/errors';
import type { Flag } from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { useNotify } from '../../components/notify';
import { useSetArchived } from './flagsApi';

/** Archive (after a confirmation) or restore a flag. */
export function ArchiveFlagButton({
  projectKey,
  flag,
}: {
  projectKey: string;
  flag: Pick<Flag, 'key' | 'name' | 'isArchived'>;
}) {
  const notify = useNotify();
  const setArchived = useSetArchived(projectKey);
  const [confirming, setConfirming] = useState(false);

  if (flag.isArchived) {
    return (
      <Button
        variant="outlined"
        startIcon={<UnarchiveOutlined />}
        loading={setArchived.isPending}
        onClick={() =>
          setArchived.mutate(
            { flagKey: flag.key, archive: false },
            {
              onSuccess: () => notify('Flag restored'),
              onError: (error) => notify(errorMessage(error), 'error'),
            },
          )
        }
      >
        Restore flag
      </Button>
    );
  }

  return (
    <>
      <Button
        variant="outlined"
        startIcon={<ArchiveOutlined />}
        onClick={() => setConfirming(true)}
      >
        Archive flag
      </Button>
      <ConfirmDialog
        open={confirming}
        title={`Archive ${flag.name}?`}
        description="SDKs stop receiving this flag and fall back to their default values. Pending scheduled changes are cancelled. You can restore it later."
        confirmLabel="Archive flag"
        destructive
        pending={setArchived.isPending}
        onConfirm={() =>
          setArchived.mutate(
            { flagKey: flag.key, archive: true },
            {
              onSuccess: () => {
                setConfirming(false);
                notify('Flag archived');
              },
              onError: (error) => notify(errorMessage(error), 'error'),
            },
          )
        }
        onClose={() => setConfirming(false)}
      />
    </>
  );
}
