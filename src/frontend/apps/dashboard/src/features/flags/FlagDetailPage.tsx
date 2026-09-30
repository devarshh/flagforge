import { Box, Skeleton, Stack, Tab, Tabs } from '@mui/material';
import { useMemo } from 'react';
import { useParams, useSearchParams } from 'react-router';
import { ApiError } from '../../api/errors';
import { EmptyState } from '../../components/EmptyState';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { ErrorState } from '../../components/ErrorState';
import { sortEnvironments } from '../environments/order';
import { HistoryTab } from '../history/HistoryTab';
import { InsightsTab } from '../insights/InsightsTab';
import { useProject } from '../projects/projectsApi';
import { ScheduleTab } from '../schedules/ScheduleTab';
import { TargetingTab } from '../targeting/TargetingTab';
import { VariationsTab } from '../variations/VariationsTab';
import { FlagHeader } from './FlagHeader';
import { FlagSettingsTab } from './FlagSettingsTab';
import { flagTabs, isFlagTab, type FlagTabId } from './flagTabs';
import { useFlag } from './flagsApi';

/** One flag, with page tabs and environment tabs deep-linked as `?tab=targeting&env=production`. */
export function FlagDetailPage() {
  const { projectKey = '', flagKey = '' } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const project = useProject(projectKey);
  const flagQuery = useFlag(projectKey, flagKey);
  const environments = useMemo(
    () => sortEnvironments(project.data?.environments ?? []),
    [project.data],
  );

  const requestedTab = searchParams.get('tab');
  const tab: FlagTabId = isFlagTab(requestedTab) ? requestedTab : 'targeting';
  const environment =
    environments.find((item) => item.key === searchParams.get('env')) ?? environments[0];
  const select = (changes: { tab?: FlagTabId; env?: string }) =>
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (changes.tab) next.set('tab', changes.tab);
      if (changes.env) next.set('env', changes.env);
      return next;
    });

  if (flagQuery.error instanceof ApiError && flagQuery.error.status === 404) {
    return (
      <EmptyState
        title="Flag not found"
        description={`There is no flag ${flagKey} in this project. It may have been deleted.`}
      />
    );
  }

  const error = flagQuery.error ?? project.error;
  if (error) {
    return (
      <ErrorState
        error={error}
        onRetry={() => {
          void flagQuery.refetch();
          void project.refetch();
        }}
      />
    );
  }

  const flag = flagQuery.data;
  if (!flag || !project.data) {
    return (
      <Stack spacing={2}>
        <Skeleton variant="text" width={320} height={48} />
        <Skeleton variant="rounded" height={56} />
        <Skeleton variant="rounded" height={360} />
      </Stack>
    );
  }

  const perEnvironment = flagTabs.find((item) => item.id === tab)?.perEnvironment ?? false;
  const saved = environment
    ? flag.environments.find((item) => item.environmentKey === environment.key)?.config
    : undefined;
  return (
    <>
      <FlagHeader projectKey={projectKey} flag={flag} environments={environments} />
      <Tabs
        value={tab}
        onChange={(_, next: FlagTabId) => select({ tab: next })}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Flag sections"
        sx={{ borderBottom: 1, borderColor: 'divider' }}
      >
        {flagTabs.map((item) => (
          <Tab
            key={item.id}
            value={item.id}
            label={item.label}
            id={`flag-tab-${item.id}`}
            aria-controls="flag-tab-panel"
          />
        ))}
      </Tabs>
      {perEnvironment && environment && (
        <Tabs
          value={environment.key}
          onChange={(_, next: string) => select({ env: next })}
          variant="scrollable"
          allowScrollButtonsMobile
          aria-label="Environment"
          sx={{ mt: 2, minHeight: 40, '& .MuiTabs-indicator': { display: 'none' } }}
        >
          {environments.map((item) => (
            <Tab
              key={item.key}
              value={item.key}
              aria-label={item.name}
              sx={{ minHeight: 40, px: 0.75 }}
              label={
                <EnvironmentChip
                  name={item.name}
                  color={item.color}
                  isProtected={item.isProtected}
                  selected={item.key === environment.key}
                />
              }
            />
          ))}
        </Tabs>
      )}
      <Box role="tabpanel" id="flag-tab-panel" aria-labelledby={`flag-tab-${tab}`} sx={{ pt: 3 }}>
        {tab === 'targeting' && environment && saved && (
          <TargetingTab
            key={environment.key}
            projectKey={projectKey}
            flag={flag}
            environment={environment}
            saved={saved}
          />
        )}
        {tab === 'variations' && (
          <VariationsTab projectKey={projectKey} flag={flag} environments={environments} />
        )}
        {tab === 'schedule' && environment && saved && (
          <ScheduleTab
            key={environment.key}
            projectKey={projectKey}
            flag={flag}
            environment={environment}
            saved={saved}
          />
        )}
        {tab === 'insights' && environment && (
          <InsightsTab
            key={environment.key}
            projectKey={projectKey}
            flag={flag}
            environment={environment}
          />
        )}
        {tab === 'history' && <HistoryTab projectKey={projectKey} flag={flag} />}
        {tab === 'settings' && <FlagSettingsTab projectKey={projectKey} flag={flag} />}
      </Box>
    </>
  );
}
