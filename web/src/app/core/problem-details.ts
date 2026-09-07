import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Pull the problem details body out of an HTTP error, if there is one. */
export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/** One sentence a person can act on. Never the raw exception. */
export function describeError(
  error: unknown,
  fallback = 'Something went wrong. Please try again.',
): string {
  const problem = problemOf(error);

  if (problem) {
    if (problem.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) return messages.join(' ');
    }
    if (problem.detail) return problem.detail;
    if (problem.title) return problem.title;
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'The server could not be reached.';
    if (error.status === 401) return 'Your session has expired. Please sign in again.';
    if (error.status === 403) return 'You are not allowed to do that.';
    if (error.status === 404) return 'That was not found.';
    if (error.status === 429) return 'Too many attempts. Wait a minute and try again.';
  }

  return fallback;
}

/** Field errors keyed the way the form controls are named (camelCase), for mapping back onto a form. */
export function fieldErrorsOf(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}
