import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 problem body returned by the API for every error. */
export interface Problem {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function asProblem(error: unknown): Problem | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as Problem;
  }
  return null;
}

/** A human-readable message for any API error. */
export function problemMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'Cannot reach the server. Check your connection.';
  }
  const problem = asProblem(error);
  const firstFieldError = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
  return firstFieldError ?? problem?.detail ?? problem?.title ?? fallback;
}

/** Field → message map from a 400 validation problem (keys are camelCase request properties). */
export function fieldErrors(error: unknown): Record<string, string> {
  const errors = asProblem(error)?.errors ?? {};
  return Object.fromEntries(Object.entries(errors).map(([key, messages]) => [key, messages[0] ?? '']));
}
