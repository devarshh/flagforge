import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { AuditEntry, PagedResult } from '../../api/types';

export interface AuditParams {
  projectKey?: string;
  flagKey?: string;
  environmentKey?: string;
  actorId?: string;
  action?: string;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

export function useAuditLog(params: AuditParams) {
  return useQuery({
    queryKey: queryKeys.audit(params),
    queryFn: () => {
      const query = new URLSearchParams();
      for (const [name, value] of Object.entries(params)) {
        if (value !== undefined && value !== '') {
          query.set(name, String(value));
        }
      }

      return apiClient.get<PagedResult<AuditEntry>>(`/api/v1/audit?${query.toString()}`);
    },
    placeholderData: keepPreviousData,
  });
}
