import { Box, Stack, Typography } from '@mui/material';
import type { AuditEntry } from '../../api/types';
import { JsonDiff } from '../../components/JsonDiff';
import { formatDateTime } from '../../components/time';
import { auditActionLabel } from './actions';

/** What changed in one audit entry: who, when, the comment, and a diff of before and after. */
export function AuditEntryDetails({ entry }: { entry: AuditEntry }) {
  return (
    <Stack spacing={1.5}>
      <Box>
        <Typography variant="body2" color="text.secondary">
          {auditActionLabel(entry.action)} by {entry.actorName}
          {entry.actorType === 'system' ? ' (system)' : ''} on {formatDateTime(entry.occurredAt)}
        </Typography>
        <Typography variant="body2" className="mono" color="text.secondary">
          {entry.resourceKey}
        </Typography>
      </Box>
      {entry.comment && (
        <Box sx={{ pl: 1.5, borderLeft: 3, borderColor: 'divider' }}>
          <Typography variant="body2">{entry.comment}</Typography>
        </Box>
      )}
      <JsonDiff before={entry.before} after={entry.after} />
    </Stack>
  );
}
