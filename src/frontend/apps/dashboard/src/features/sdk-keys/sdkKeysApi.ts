import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { CreatedSdkKey, SdkKey } from '../../api/types';

function keysPath(projectKey: string, environmentKey: string) {
  return `/api/v1/projects/${projectKey}/environments/${environmentKey}/sdk-keys`;
}

export function useSdkKeys(projectKey: string, environmentKey: string) {
  return useQuery({
    queryKey: queryKeys.sdkKeys(projectKey, environmentKey),
    queryFn: () => apiClient.get<SdkKey[]>(keysPath(projectKey, environmentKey)),
  });
}

export function useCreateSdkKey(projectKey: string, environmentKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      apiClient.post<CreatedSdkKey>(keysPath(projectKey, environmentKey), { name }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: queryKeys.sdkKeys(projectKey, environmentKey) }),
  });
}

export function useRevokeSdkKey(projectKey: string, environmentKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (keyId: string) =>
      apiClient.delete(`${keysPath(projectKey, environmentKey)}/${keyId}`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: queryKeys.sdkKeys(projectKey, environmentKey) }),
  });
}
