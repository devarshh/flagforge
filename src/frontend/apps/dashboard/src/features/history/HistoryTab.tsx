import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Pagination,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreRounded from '@mui/icons-material/ExpandMoreRounded';
import { useState } from 'react';
import type { Flag } from '../../api/types';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { RelativeTime } from '../../components/RelativeTime';
import { Section } from '../../components/Section';
import { auditActionLabel } from '../audit/actions';
import { AuditEntryDetails } from '../audit/AuditEntryDetails';
import { useAuditLog } from '../audit/auditApi';

const pageSize = 20;

/** This flag's audit entries, newest first; each expands to show what changed. */
export function HistoryTab({ projectKey, flag }: { projectKey: string; flag: Flag }) {
  const [page, setPage] = useState(1);
  const history = useAuditLog({ projectKey, flagKey: flag.key, page, pageSize });
  const pageCount = history.data ? Math.max(1, Math.ceil(history.data.totalCount / pageSize)) : 1;

  return (
    <Section
      title="History"
      description="Every change to this flag, newest first. Expand an entry to see what changed."
    >
      {history.error && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
      {history.isPending && <Skeleton variant="rounded" height={160} />}
      {history.data?.totalCount === 0 && <EmptyState title="No changes recorded yet" />}
      {history.data && history.data.items.length > 0 && (
        <Stack spacing={2}>
          <Box>
            {history.data.items.map((entry) => (
              <Accordion
                key={entry.id}
                disableGutters
                variant="outlined"
                sx={{ '&:not(:first-of-type)': { borderTop: 0 } }}
                slotProps={{ transition: { unmountOnExit: true } }}
              >
                <AccordionSummary expandIcon={<ExpandMoreRounded />}>
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={{ xs: 0.25, sm: 2 }}
                    sx={{ width: '100%', pr: 1 }}
                  >
                    <Typography sx={{ fontWeight: 500, minWidth: { sm: 220 } }}>
                      {auditActionLabel(entry.action)}
                    </Typography>
                    <Typography color="text.secondary" sx={{ flexGrow: 1 }}>
                      {entry.actorName}
                      {entry.comment ? `: ${entry.comment}` : ''}
                    </Typography>
                    <Typography
                      variant="body2"
                      color="text.secondary"
                      sx={{ whiteSpace: 'nowrap' }}
                    >
                      <RelativeTime value={entry.occurredAt} />
                    </Typography>
                  </Stack>
                </AccordionSummary>
                <AccordionDetails>
                  <AuditEntryDetails entry={entry} />
                </AccordionDetails>
              </Accordion>
            ))}
          </Box>
          {pageCount > 1 && (
            <Pagination count={pageCount} page={page} onChange={(_, next) => setPage(next)} />
          )}
        </Stack>
      )}
    </Section>
  );
}
