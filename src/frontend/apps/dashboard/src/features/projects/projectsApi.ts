import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { Project } from '../../api/types';

export function useProjects() {
  return useQuery({
    queryKey: queryKeys.projects,
    queryFn: () => apiClient.get<Project[]>('/api/v1/projects'),
  });
}

export function useProject(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.project(projectKey),
    queryFn: () => apiClient.get<Project>(`/api/v1/projects/${projectKey}`),
  });
}

export interface CreateProjectInput {
  key: string;
  name: string;
  description?: string;
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateProjectInput) => apiClient.post<Project>('/api/v1/projects', input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.projects }),
  });
}

export function useUpdateProject(projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { name?: string; description?: string }) =>
      apiClient.patch<Project>(`/api/v1/projects/${projectKey}`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.projects }),
  });
}

export function useDeleteProject(projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiClient.delete(`/api/v1/projects/${projectKey}`, { confirmKey: projectKey }),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: queryKeys.project(projectKey) });
      return queryClient.invalidateQueries({ queryKey: queryKeys.projects });
    },
  });
}
