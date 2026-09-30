import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type {
  EvaluationResult,
  Flag,
  JsonValue,
  Targeting,
  TargetingConfig,
} from '../../api/types';
import { flagPath, invalidateFlags } from '../flags/flagsApi';

export interface SaveTargetingInput {
  config: TargetingConfig;
  expectedVersion: number;
  comment?: string;
}

export function useSaveTargeting(projectKey: string, flagKey: string, environmentKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ config, expectedVersion, comment }: SaveTargetingInput) =>
      apiClient.put<Targeting>(`${flagPath(projectKey, flagKey)}/environments/${environmentKey}`, {
        config,
        expectedVersion,
        comment: comment || undefined,
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData<Flag>(
        queryKeys.flag(projectKey, flagKey),
        (flag) =>
          flag && {
            ...flag,
            environments: flag.environments.map((environment) =>
              environment.environmentKey === environmentKey
                ? { ...environment, config: saved }
                : environment,
            ),
          },
      );
      return invalidateFlags(queryClient, projectKey);
    },
  });
}

export interface PreviewInput {
  context: JsonValue;
  draftConfig?: TargetingConfig;
}

/** Evaluates a context against a draft (or the saved config) without counting usage. */
export function usePreviewEvaluation(projectKey: string, flagKey: string, environmentKey: string) {
  return useMutation({
    mutationFn: (input: PreviewInput) =>
      apiClient.post<EvaluationResult>(
        `${flagPath(projectKey, flagKey)}/environments/${environmentKey}/evaluate-preview`,
        input,
      ),
  });
}
