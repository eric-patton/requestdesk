import { STATUSES, isTerminal, transitionVerb } from './status';

describe('status helpers', () => {
  it('names the reopen move', () => {
    expect(transitionVerb('Resolved', 'InProgress')).toBe('Reopen');
  });

  it('distinguishes starting work from unblocking', () => {
    expect(transitionVerb('Triaged', 'InProgress')).toBe('Start work');
    expect(transitionVerb('Blocked', 'InProgress')).toBe('Unblock');
  });

  it('uses a verb for every other target', () => {
    expect(transitionVerb('New', 'Triaged')).toBe('Triage');
    expect(transitionVerb('InProgress', 'Blocked')).toBe('Mark blocked');
    expect(transitionVerb('InProgress', 'Resolved')).toBe('Resolve');
    expect(transitionVerb('Resolved', 'Closed')).toBe('Close');
    expect(transitionVerb('New', 'Cancelled')).toBe('Cancel request');
  });

  it('knows which statuses are terminal', () => {
    expect(STATUSES.filter(isTerminal)).toEqual(['Closed', 'Cancelled']);
  });
});
