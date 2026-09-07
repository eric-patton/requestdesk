import { HttpErrorResponse } from '@angular/common/http';
import { describeError, fieldErrorsOf, problemOf } from './problem-details';

function httpError(status: number, body: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body, url: '/api/requests' });
}

describe('problem details helpers', () => {
  it('prefers the field messages of a validation problem', () => {
    const error = httpError(400, {
      title: 'One or more fields are invalid.',
      errors: { title: ['A title is required.'], description: ['A description is required.'] },
    });

    expect(describeError(error)).toBe('A title is required. A description is required.');
    expect(fieldErrorsOf(error)).toEqual({
      title: ['A title is required.'],
      description: ['A description is required.'],
    });
  });

  it('falls back to detail, then title', () => {
    expect(
      describeError(httpError(400, { title: 'Rule', detail: 'That customer does not exist.' })),
    ).toBe('That customer does not exist.');
    expect(describeError(httpError(409, { title: 'That status change is not allowed.' }))).toBe(
      'That status change is not allowed.',
    );
  });

  it('has a plain sentence for status codes with no body', () => {
    expect(describeError(httpError(0))).toBe('The server could not be reached.');
    expect(describeError(httpError(401))).toBe('Your session has expired. Please sign in again.');
    expect(describeError(httpError(403))).toBe('You are not allowed to do that.');
    expect(describeError(httpError(404))).toBe('That was not found.');
    expect(describeError(httpError(429))).toBe('Too many attempts. Wait a minute and try again.');
    expect(describeError(httpError(500), 'Custom fallback')).toBe('Custom fallback');
  });

  it('exposes the conflict extensions the API adds', () => {
    const problem = problemOf(
      httpError(409, {
        status: 409,
        from: 'New',
        to: 'Closed',
        legalTransitions: ['Triaged', 'Cancelled'],
      }),
    );

    expect(problem?.legalTransitions).toEqual(['Triaged', 'Cancelled']);
    expect(problem?.from).toBe('New');
  });

  it('returns null for things that are not HTTP errors', () => {
    expect(problemOf(new Error('boom'))).toBeNull();
    expect(fieldErrorsOf(new Error('boom'))).toEqual({});
    expect(describeError(new Error('boom'))).toBe('Something went wrong. Please try again.');
  });
});
