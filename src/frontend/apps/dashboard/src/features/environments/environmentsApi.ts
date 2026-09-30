import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { Environment } from '../../api/types';

export interface EnvironmentInput {
  key: string;
  name: string;
  color: string;
  isProtected: boolean;
}

export interface EnvironmentChanges {
  name?: string;
  color?: string;
  isProtected?: boolean;
  sortOrder?: number;
}

function useInvalidateProject(projectKey: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.projects }),
      queryClient.invalidateQueries({ queryKey: queryKeys.flags(projectKey) }),
    ]);
}

export function useCreateEnvironment(projectKey: string) {
  const invalidate = useInvalidateProject(projectKey);
  return useMutation({
    mutationFn: (input: EnvironmentInput) =>
      apiClient.post<Environment>(`/api/v1/projects/${projectKey}/environments`, input),
    onSuccess: invalidate,
  });
}

export function useUpdateEnvironment(projectKey: string) {
  const invalidate = useInvalidateProject(projectKey);
  return useMutation({
    mutationFn: ({
      environmentKey,
      changes,
    }: {
      environmentKey: string;
      changes: EnvironmentChanges;
    }) =>
      apiClient.patch<Environment>(
        `/api/v1/projects/${projectKey}/environments/${environmentKey}`,
        changes,
      ),
    onSuccess: invalidate,
  });
}

/** Saves a new order: every environment whose position changed gets its index as sort order. */
export function useReorderEnvironments(projectKey: string) {
  const invalidate = useInvalidateProject(projectKey);
  return useMutation({
    mutationFn: async (ordered: readonly Environment[]) => {
      for (const [index, environment] of ordered.entries()) {
        if (environment.sortOrder !== index) {
          await apiClient.patch<Environment>(
            `/api/v1/projects/${projectKey}/environments/${environment.key}`,
            { sortOrder: index },
          );
        }
      }
    },
    onSettled: invalidate,
  });
}

export function useDeleteEnvironment(projectKey: string) {
  const invalidate = useInvalidateProject(projectKey);
  return useMutation({
    mutationFn: (environmentKey: string) =>
      apiClient.delete(`/api/v1/projects/${projectKey}/environments/${environmentKey}`, {
        confirmKey: environmentKey,
      }),
    onSuccess: invalidate,
  });
}
