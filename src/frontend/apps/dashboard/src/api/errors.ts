/** A failed API call, parsed from an RFC 9457 ProblemDetails body when the server sent one. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail: string | undefined,
    readonly errors: Readonly<Record<string, readonly string[]>> = {},
    readonly extensions: Readonly<Record<string, unknown>> = {},
  ) {
    super(detail ?? title);
    this.name = 'ApiError';
  }

  /** The field errors under `prefix` (for example `config.`), with the prefix removed. */
  fieldErrors(prefix = ''): Record<string, readonly string[]> {
    return Object.fromEntries(
      Object.entries(this.errors)
        .filter(([path]) => path.startsWith(prefix))
        .map(([path, messages]) => [path.slice(prefix.length), messages]),
    );
  }
}

const knownProblemMembers = new Set(['type', 'title', 'status', 'detail', 'instance', 'errors']);

export async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    // Not JSON (a proxy error page, for example): fall back to the status text below.
  }

  if (body === null || typeof body !== 'object') {
    return new ApiError(
      response.status,
      response.statusText || `HTTP ${response.status}`,
      undefined,
    );
  }

  const problem = body as Record<string, unknown>;
  const errors = isErrorMap(problem.errors) ? problem.errors : {};
  const extensions = Object.fromEntries(
    Object.entries(problem).filter(([key]) => !knownProblemMembers.has(key)),
  );
  return new ApiError(
    response.status,
    typeof problem.title === 'string' ? problem.title : `HTTP ${response.status}`,
    typeof problem.detail === 'string' ? problem.detail : undefined,
    errors,
    extensions,
  );
}

/** A message suitable for a snackbar or an error state. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const firstFieldError = Object.values(error.errors)[0]?.[0];
    return error.detail ?? firstFieldError ?? error.title;
  }

  return error instanceof Error ? error.message : 'Something went wrong. Try again.';
}

function isErrorMap(value: unknown): value is Record<string, string[]> {
  return (
    value !== null &&
    typeof value === 'object' &&
    Object.values(value).every((messages) => Array.isArray(messages))
  );
}
