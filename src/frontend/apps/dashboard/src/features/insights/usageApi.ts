import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { UsageBucket } from '../../api/types';
import { flagPath } from '../flags/flagsApi';
import { usageWindow, type UsageRange } from './usage';

export function useUsage(
  projectKey: string,
  flagKey: string,
  environmentKey: string,
  range: UsageRange,
) {
  return useQuery({
    queryKey: queryKeys.usage(projectKey, flagKey, environmentKey, range),
    queryFn: async () => {
      const window = usageWindow(range, Date.now());
      const query = new URLSearchParams({
        granularity: window.granularity,
        from: window.from.toISOString(),
        to: window.to.toISOString(),
      });
      const usage = await apiClient.get<UsageBucket[]>(
        `${flagPath(projectKey, flagKey)}/environments/${environmentKey}/usage?${query.toString()}`,
      );
      return { window, usage };
    },
  });
}
