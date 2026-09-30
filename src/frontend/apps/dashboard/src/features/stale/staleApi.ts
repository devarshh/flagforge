import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { StaleFlag } from '../../api/types';

export function useStaleFlags(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.stale(projectKey),
    queryFn: () => apiClient.get<StaleFlag[]>(`/api/v1/projects/${projectKey}/stale-flags`),
  });
}
