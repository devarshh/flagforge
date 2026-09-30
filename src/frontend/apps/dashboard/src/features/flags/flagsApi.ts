import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type {
  Flag,
  FlagSummary,
  FlagType,
  PagedResult,
  Targeting,
  VariationInput,
} from '../../api/types';

export interface FlagListParams {
  search: string;
  tag: string | null;
  includeArchived: boolean;
  page: number;
  pageSize: number;
}

export function flagPath(projectKey: string, flagKey: string): string {
  return `/api/v1/projects/${projectKey}/flags/${flagKey}`;
}

export function useFlagList(projectKey: string, params: FlagListParams) {
  return useQuery({
    queryKey: queryKeys.flagList(projectKey, params),
    queryFn: () => {
      const query = new URLSearchParams({
        page: String(params.page),
        pageSize: String(params.pageSize),
        includeArchived: String(params.includeArchived),
      });
      if (params.search) {
        query.set('search', params.search);
      }

      if (params.tag) {
        query.set('tag', params.tag);
      }

      return apiClient.get<PagedResult<FlagSummary>>(
        `/api/v1/projects/${projectKey}/flags?${query.toString()}`,
      );
    },
    placeholderData: keepPreviousData,
  });
}

export function useFlag(projectKey: string, flagKey: string) {
  return useQuery({
    queryKey: queryKeys.flag(projectKey, flagKey),
    queryFn: () => apiClient.get<Flag>(flagPath(projectKey, flagKey)),
  });
}

/** Refreshes every list and detail view of a project's flags after a change. */
export function invalidateFlags(queryClient: QueryClient, projectKey: string) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.flags(projectKey) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.stale(projectKey) }),
  ]);
}

export interface CreateFlagInput {
  key: string;
  name: string;
  description?: string;
  type: FlagType;
  variations?: VariationInput[];
  tags: string[];
  isPermanent: boolean;
}

export function useCreateFlag(projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateFlagInput) =>
      apiClient.post<Flag>(`/api/v1/projects/${projectKey}/flags`, input),
    onSuccess: () => invalidateFlags(queryClient, projectKey),
  });
}

export interface UpdateFlagInput {
  name?: string;
  description?: string;
  tags?: string[];
  isPermanent?: boolean;
}

export function useUpdateFlag(projectKey: string, flagKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateFlagInput) =>
      apiClient.patch<Flag>(flagPath(projectKey, flagKey), input),
    onSuccess: (flag) => {
      queryClient.setQueryData(queryKeys.flag(projectKey, flagKey), flag);
      return invalidateFlags(queryClient, projectKey);
    },
  });
}

export function useReplaceVariations(projectKey: string, flagKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variations: VariationInput[]) =>
      apiClient.put<Flag>(`${flagPath(projectKey, flagKey)}/variations`, { variations }),
    onSuccess: (flag) => {
      queryClient.setQueryData(queryKeys.flag(projectKey, flagKey), flag);
      return invalidateFlags(queryClient, projectKey);
    },
  });
}

/** Archive or restore, depending on `archive`. */
export function useSetArchived(projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ flagKey, archive }: { flagKey: string; archive: boolean }) =>
      apiClient.post<Flag>(`${flagPath(projectKey, flagKey)}/${archive ? 'archive' : 'restore'}`),
    onSuccess: (flag) => {
      queryClient.setQueryData(queryKeys.flag(projectKey, flag.key), flag);
      return invalidateFlags(queryClient, projectKey);
    },
  });
}

export function useDeleteFlag(projectKey: string, flagKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => apiClient.delete(flagPath(projectKey, flagKey), { confirmKey: flagKey }),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: queryKeys.flag(projectKey, flagKey) });
      return invalidateFlags(queryClient, projectKey);
    },
  });
}

export interface ToggleInput {
  flagKey: string;
  environmentKey: string;
  enabled: boolean;
  comment?: string;
}

interface ToggleSnapshot {
  lists: [readonly unknown[], PagedResult<FlagSummary> | undefined][];
  detail: Flag | undefined;
}

/**
 * Turns a flag on or off in one environment. The lamp changes at once (optimistic update of every cached list and
 * the detail view) and rolls back if the server refuses.
 */
export function useToggleFlag(projectKey: string) {
  const queryClient = useQueryClient();
  const listKey = [...queryKeys.flags(projectKey), 'list'];
  return useMutation<Targeting, Error, ToggleInput, ToggleSnapshot>({
    mutationFn: ({ flagKey, environmentKey, enabled, comment }) =>
      apiClient.post<Targeting>(
        `${flagPath(projectKey, flagKey)}/environments/${environmentKey}/toggle`,
        {
          enabled,
          comment: comment || undefined,
        },
      ),
    onMutate: async ({ flagKey, environmentKey, enabled }) => {
      const detailKey = queryKeys.flag(projectKey, flagKey);
      await Promise.all([
        queryClient.cancelQueries({ queryKey: listKey }),
        queryClient.cancelQueries({ queryKey: detailKey }),
      ]);
      const snapshot: ToggleSnapshot = {
        lists: queryClient.getQueriesData<PagedResult<FlagSummary>>({ queryKey: listKey }),
        detail: queryClient.getQueryData<Flag>(detailKey),
      };
      queryClient.setQueriesData<PagedResult<FlagSummary>>(
        { queryKey: listKey },
        (page) =>
          page && {
            ...page,
            items: page.items.map((flag) =>
              flag.key !== flagKey
                ? flag
                : {
                    ...flag,
                    environments: flag.environments.map((environment) =>
                      environment.environmentKey === environmentKey
                        ? { ...environment, enabled }
                        : environment,
                    ),
                  },
            ),
          },
      );
      queryClient.setQueryData<Flag>(
        detailKey,
        (flag) =>
          flag && {
            ...flag,
            environments: flag.environments.map((environment) =>
              environment.environmentKey === environmentKey
                ? { ...environment, config: { ...environment.config, enabled } }
                : environment,
            ),
          },
      );
      return snapshot;
    },
    onError: (_error, { flagKey }, snapshot) => {
      if (!snapshot) {
        return;
      }

      for (const [key, data] of snapshot.lists) {
        queryClient.setQueryData(key, data);
      }

      queryClient.setQueryData(queryKeys.flag(projectKey, flagKey), snapshot.detail);
    },
    onSettled: () => invalidateFlags(queryClient, projectKey),
  });
}
