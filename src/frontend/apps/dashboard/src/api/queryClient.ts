import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './errors';

/** Client errors (4xx) are not retried: repeating them cannot help. Other failures get one retry. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false;
  }

  return failureCount < 1;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 10_000, refetchOnWindowFocus: true, retry: shouldRetry },
      mutations: { retry: false },
    },
  });
}
