/** The single source of TanStack Query keys, so invalidation matches what queries used. */
export const queryKeys = {
  meta: ['meta'] as const,
  projects: ['projects'] as const,
  project: (projectKey: string) => ['projects', projectKey] as const,
  environments: (projectKey: string) => ['projects', projectKey, 'environments'] as const,
  sdkKeys: (projectKey: string, environmentKey: string) =>
    ['projects', projectKey, 'environments', environmentKey, 'sdk-keys'] as const,
  flags: (projectKey: string) => ['projects', projectKey, 'flags'] as const,
  flagList: (projectKey: string, params: object) =>
    ['projects', projectKey, 'flags', 'list', params] as const,
  flag: (projectKey: string, flagKey: string) =>
    ['projects', projectKey, 'flags', 'detail', flagKey] as const,
  schedules: (projectKey: string, flagKey: string, environmentKey: string) =>
    ['projects', projectKey, 'flags', 'detail', flagKey, environmentKey, 'schedules'] as const,
  usage: (projectKey: string, flagKey: string, environmentKey: string, range: string) =>
    ['projects', projectKey, 'flags', 'detail', flagKey, environmentKey, 'usage', range] as const,
  stale: (projectKey: string) => ['projects', projectKey, 'stale'] as const,
  audit: (params: object) => ['audit', params] as const,
  users: (params: object) => ['users', params] as const,
};
