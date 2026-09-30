import {
  Box,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { alpha } from '@mui/material/styles';
import CloseRounded from '@mui/icons-material/CloseRounded';
import { useFlagForgeClient } from '@flagforge/sdk/react';
import type { EvaluationContext } from '@flagforge/sdk';
import { describeReason, formatValue } from '../reason';
import { ConnectionChip } from './ConnectionChip';
import { useFlagChanges } from './useFlagChanges';

export interface FlagInspectorProps {
  context: EvaluationContext;
  onClose: () => void;
}

/** What FlagForge served for this shopper: value, variation, and why. Rows light up briefly when they change. */
export function FlagInspector({ context, onClose }: FlagInspectorProps) {
  const client = useFlagForgeClient();
  const { flags, changed } = useFlagChanges(client);
  const keys = Object.keys(flags).sort((left, right) => left.localeCompare(right));
  return (
    <Stack spacing={2} sx={{ p: 2 }} component="section" aria-labelledby="inspector-title">
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Typography id="inspector-title" variant="h3" sx={{ flexGrow: 1 }}>
          Flag inspector
        </Typography>
        <ConnectionChip />
        <IconButton aria-label="Close flag inspector" size="small" onClick={onClose}>
          <CloseRounded fontSize="small" />
        </IconButton>
      </Stack>
      <Typography variant="body2" color="text.secondary">
        Evaluated for <span className="mono">{context.key}</span>. Change a flag in the FlagForge
        dashboard and watch this list and the store update.
      </Typography>
      {keys.length === 0 ? (
        <Typography color="text.secondary">Waiting for flags...</Typography>
      ) : (
        <Table size="small" aria-label="Flags served to this shopper">
          <TableHead>
            <TableRow>
              <TableCell>Flag</TableCell>
              <TableCell>Value</TableCell>
              <TableCell>Why</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {keys.map((key) => {
              const evaluation = flags[key]!;
              return (
                <TableRow
                  key={key}
                  data-changed={changed.has(key) || undefined}
                  sx={(theme) => ({
                    transition: theme.transitions.create('background-color', { duration: 600 }),
                    bgcolor: changed.has(key)
                      ? alpha(theme.palette.secondary.main, 0.22)
                      : 'transparent',
                  })}
                >
                  <TableCell className="mono" sx={{ fontSize: 12, overflowWrap: 'break-word' }}>
                    {key}
                  </TableCell>
                  <TableCell sx={{ maxWidth: 140 }}>
                    <Tooltip title={formatValue(evaluation.value)}>
                      <Typography className="mono" noWrap sx={{ fontSize: 12 }}>
                        {formatValue(evaluation.value)}
                      </Typography>
                    </Tooltip>
                    {evaluation.variationId && (
                      <Typography
                        className="mono"
                        variant="caption"
                        color="text.secondary"
                        noWrap
                        component="div"
                      >
                        {evaluation.variationId}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{describeReason(evaluation.reason)}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
      <Box />
    </Stack>
  );
}
