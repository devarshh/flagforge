import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type {
  ScheduledChange,
  ScheduledChangeAction,
  Serve,
  WeightedVariation,
} from '../../api/types';
import { flagPath } from '../flags/flagsApi';

function basePath(projectKey: string, flagKey: string, environmentKey: string) {
  return `${flagPath(projectKey, flagKey)}/environments/${environmentKey}`;
}

/** Scheduled changes for one environment; polls while any are waiting, since the worker runs every 15 seconds. */
export function useSchedules(projectKey: string, flagKey: string, environmentKey: string) {
  return useQuery({
    queryKey: queryKeys.schedules(projectKey, flagKey, environmentKey),
    queryFn: () =>
      apiClient.get<ScheduledChange[]>(
        `${basePath(projectKey, flagKey, environmentKey)}/scheduled-changes`,
      ),
    refetchInterval: (query) =>
      query.state.data?.some(
        (change) => change.status === 'pending' || change.status === 'processing',
      )
        ? 15_000
        : false,
  });
}

function useInvalidateSchedules(projectKey: string, flagKey: string, environmentKey: string) {
  const queryClient = useQueryClient();
  return () =>
    queryClient.invalidateQueries({
      queryKey: queryKeys.schedules(projectKey, flagKey, environmentKey),
    });
}

export interface CreateScheduleInput {
  executeAt: string;
  action: ScheduledChangeAction;
  payload?: Serve;
}

export function useCreateSchedule(projectKey: string, flagKey: string, environmentKey: string) {
  const invalidate = useInvalidateSchedules(projectKey, flagKey, environmentKey);
  return useMutation({
    mutationFn: (input: CreateScheduleInput) =>
      apiClient.post<ScheduledChange>(
        `${basePath(projectKey, flagKey, environmentKey)}/scheduled-changes`,
        input,
      ),
    onSuccess: invalidate,
  });
}

export interface ReleasePlanInput {
  steps: { executeAt: string; weights: WeightedVariation[] }[];
  bucketBy: string;
}

export function useCreateReleasePlan(projectKey: string, flagKey: string, environmentKey: string) {
  const invalidate = useInvalidateSchedules(projectKey, flagKey, environmentKey);
  return useMutation({
    mutationFn: (input: ReleasePlanInput) =>
      apiClient.post<ScheduledChange[]>(
        `${basePath(projectKey, flagKey, environmentKey)}/release-plan`,
        input,
      ),
    onSuccess: invalidate,
  });
}

export function useCancelSchedule(projectKey: string, flagKey: string, environmentKey: string) {
  const invalidate = useInvalidateSchedules(projectKey, flagKey, environmentKey);
  return useMutation({
    mutationFn: (changeId: string) =>
      apiClient.delete(
        `${basePath(projectKey, flagKey, environmentKey)}/scheduled-changes/${changeId}`,
      ),
    onSuccess: invalidate,
  });
}
