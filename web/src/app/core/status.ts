import { RequestPriority, RequestStatus } from './models';

export const STATUSES: readonly RequestStatus[] = [
  'New',
  'Triaged',
  'InProgress',
  'Blocked',
  'Resolved',
  'Closed',
  'Cancelled',
];

export const PRIORITIES: readonly RequestPriority[] = ['Low', 'Normal', 'High', 'Urgent'];

export const STATUS_LABEL: Record<RequestStatus, string> = {
  New: 'New',
  Triaged: 'Triaged',
  InProgress: 'In progress',
  Blocked: 'Blocked',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
};

/** CSS class suffix; the colours live in styles.scss as `--status-*` variables. */
export const STATUS_CLASS: Record<RequestStatus, string> = {
  New: 'new',
  Triaged: 'triaged',
  InProgress: 'in-progress',
  Blocked: 'blocked',
  Resolved: 'resolved',
  Closed: 'closed',
  Cancelled: 'cancelled',
};

/** What the button that performs a transition should say. */
export function transitionVerb(from: RequestStatus, to: RequestStatus): string {
  if (from === 'Resolved' && to === 'InProgress') return 'Reopen';

  switch (to) {
    case 'Triaged':
      return 'Triage';
    case 'InProgress':
      return from === 'Blocked' ? 'Unblock' : 'Start work';
    case 'Blocked':
      return 'Mark blocked';
    case 'Resolved':
      return 'Resolve';
    case 'Closed':
      return 'Close';
    case 'Cancelled':
      return 'Cancel request';
    default:
      return `Move to ${STATUS_LABEL[to]}`;
  }
}

export function transitionIcon(from: RequestStatus, to: RequestStatus): string {
  if (from === 'Resolved' && to === 'InProgress') return 'replay';

  switch (to) {
    case 'Triaged':
      return 'fact_check';
    case 'InProgress':
      return 'play_arrow';
    case 'Blocked':
      return 'block';
    case 'Resolved':
      return 'task_alt';
    case 'Closed':
      return 'lock';
    case 'Cancelled':
      return 'cancel';
    default:
      return 'arrow_forward';
  }
}

export function isTerminal(status: RequestStatus): boolean {
  return status === 'Closed' || status === 'Cancelled';
}
