import {
  Box,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { BarChart } from '@mui/x-charts/BarChart';
import { useMemo, useState } from 'react';
import type { Environment, Flag } from '../../api/types';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { RelativeTime } from '../../components/RelativeTime';
import { Section } from '../../components/Section';
import { variationColor } from '../../theme/tokens';
import { VariationLabel } from '../targeting/VariationLabel';
import { seriesByVariation, type UsageRange } from './usage';
import { useUsage } from './usageApi';

const numberFormat = new Intl.NumberFormat('en');
const hourLabel = new Intl.DateTimeFormat('en', { hour: '2-digit', minute: '2-digit' });
const dayLabel = new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' });

export function InsightsTab({
  projectKey,
  flag,
  environment,
}: {
  projectKey: string;
  flag: Flag;
  environment: Environment;
}) {
  const [range, setRange] = useState<UsageRange>('24h');
  const usage = useUsage(projectKey, flag.key, environment.key, range);
  const lastEvaluatedAt =
    flag.environments.find((item) => item.environmentKey === environment.key)?.lastEvaluatedAt ??
    null;

  const chart = useMemo(() => {
    if (!usage.data) {
      return null;
    }

    const { window, usage: buckets } = usage.data;
    const knownIds = flag.variations.map((variation) => variation.id);
    const series = seriesByVariation(buckets, window.buckets, knownIds);
    const format = window.granularity === 'hour' ? hourLabel : dayLabel;
    const totals = [...series.entries()].map(([variationId, counts]) => ({
      variationId,
      total: counts.reduce((sum, count) => sum + count, 0),
    }));
    return {
      labels: window.buckets.map((start) => format.format(new Date(start))),
      series: [...series.entries()].map(([variationId, counts]) => {
        const index = knownIds.indexOf(variationId);
        return {
          id: variationId,
          data: counts,
          stack: 'evaluations',
          label: flag.variations[index]?.name ?? variationId,
          color: variationColor(index >= 0 ? index : knownIds.length),
        };
      }),
      totals,
      grandTotal: totals.reduce((sum, item) => sum + item.total, 0),
    };
  }, [usage.data, flag.variations]);

  return (
    <Section
      title="Evaluations"
      description={
        <>
          How often SDKs evaluated this flag in {environment.name}, by variation. Last evaluated{' '}
          <RelativeTime value={lastEvaluatedAt} fallback="never" resolution="hour" />.
        </>
      }
      actions={
        <ToggleButtonGroup
          exclusive
          size="small"
          value={range}
          onChange={(_, next: UsageRange | null) => next && setRange(next)}
          aria-label="Time range"
        >
          <ToggleButton value="24h">24 hours</ToggleButton>
          <ToggleButton value="30d">30 days</ToggleButton>
        </ToggleButtonGroup>
      }
    >
      {usage.error && <ErrorState error={usage.error} onRetry={() => void usage.refetch()} />}
      {usage.isPending && <Skeleton variant="rounded" height={300} />}
      {chart && chart.grandTotal === 0 && (
        <EmptyState
          title="No evaluations in this period"
          description="Counts appear here within a minute of an SDK evaluating the flag. Evaluations from the test panel are not counted."
        />
      )}
      {chart && chart.grandTotal > 0 && (
        <Stack spacing={3}>
          <Box sx={{ width: '100%', minWidth: 0 }}>
            <BarChart
              height={300}
              series={chart.series}
              xAxis={[{ scaleType: 'band', data: chart.labels, tickLabelMinGap: 12 }]}
              yAxis={[
                {
                  width: 56,
                  valueFormatter: (value: number | null) =>
                    value === null ? '' : numberFormat.format(value),
                },
              ]}
              grid={{ horizontal: true }}
              aria-label={`Evaluations per variation, ${range === '24h' ? 'last 24 hours by hour' : 'last 30 days by day'}`}
            />
          </Box>
          <Table size="small" aria-label="Totals per variation" sx={{ maxWidth: 560 }}>
            <TableHead>
              <TableRow>
                <TableCell>Variation</TableCell>
                <TableCell align="right">Evaluations</TableCell>
                <TableCell align="right">Share</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {chart.totals.map(({ variationId, total }) => {
                const index = flag.variations.findIndex(
                  (variation) => variation.id === variationId,
                );
                const variation = flag.variations[index];
                return (
                  <TableRow key={variationId}>
                    <TableCell>
                      {variation ? (
                        <VariationLabel variation={variation} index={index} />
                      ) : (
                        <Typography className="mono">{variationId}</Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">{numberFormat.format(total)}</TableCell>
                    <TableCell align="right">
                      {((total / chart.grandTotal) * 100).toFixed(1)}%
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Stack>
      )}
    </Section>
  );
}
