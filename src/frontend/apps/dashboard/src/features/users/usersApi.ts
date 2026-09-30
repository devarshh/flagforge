import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/client';
import { queryKeys } from '../../api/queryKeys';
import type { PagedResult, Role, User } from '../../api/types';

export function useUsers(params: { page: number; pageSize: number }, enabled = true) {
  return useQuery({
    queryKey: queryKeys.users(params),
    queryFn: () =>
      apiClient.get<PagedResult<User>>(
        `/api/v1/users?page=${params.page}&pageSize=${params.pageSize}`,
      ),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export interface CreateUserInput {
  email: string;
  displayName: string;
  role: Role;
}

export interface CreatedUser {
  user: User;
  temporaryPassword: string;
}

function useInvalidateUsers() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['users'] });
}

export function useCreateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: (input: CreateUserInput) => apiClient.post<CreatedUser>('/api/v1/users', input),
    onSuccess: invalidate,
  });
}

export function useUpdateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({
      userId,
      changes,
    }: {
      userId: string;
      changes: { displayName?: string; role?: Role; isActive?: boolean };
    }) => apiClient.patch<User>(`/api/v1/users/${userId}`, changes),
    onSuccess: invalidate,
  });
}

export function useResetPassword() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: (userId: string) =>
      apiClient.post<{ temporaryPassword: string }>(`/api/v1/users/${userId}/reset-password`),
    onSuccess: invalidate,
  });
}
