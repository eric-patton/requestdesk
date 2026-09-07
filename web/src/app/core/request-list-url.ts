import { Params } from '@angular/router';
import { RequestListParams, RequestPriority, RequestStatus } from './models';
import { PRIORITIES, STATUSES } from './status';

/**
 * Translation between the request list's state and the query string.
 *
 * The URL is the single source of truth for what the list is showing. That is what makes the
 * back button work, what survives a refresh, and what lets somebody paste "the urgent blocked
 * queue" into a message and have it mean the same thing at the other end.
 *
 * Everything here is a pure function so it can be tested without a router, a component or a
 * browser. The parsing side is deliberately strict: a query string is user input, and a
 * hand-edited one should degrade to the default rather than send nonsense to the API and get a
 * 400 back.
 */

/** Mirrors ListRequestsQuery.SortableFields on the server. */
export const SORTABLE_FIELDS: readonly string[] = [
  'updatedAt',
  'createdAt',
  'priority',
  'status',
  'title',
  'referenceNumber',
  'customer',
];

/** Mirrors the paginator's options, and the server's MaxPageSize of 100. */
export const PAGE_SIZES: readonly number[] = [10, 20, 50, 100];

export const DEFAULT_LIST_PARAMS: RequestListParams = {
  page: 1,
  pageSize: 20,
  status: [],
  priority: [],
  unassigned: false,
  search: '',
  sortBy: 'updatedAt',
  sortDescending: true,
};

/**
 * The list state as a query string, with anything still at its default left out. A URL that says
 * `?status=Blocked` rather than `?page=1&size=20&sort=updatedAt&dir=desc&status=Blocked` is one
 * somebody might actually read, and it keeps the address bar honest about what is filtered.
 */
export function toQueryParams(params: RequestListParams): Params {
  const query: Params = {};

  if (params.status.length > 0) query['status'] = params.status.join(',');
  if (params.priority.length > 0) query['priority'] = params.priority.join(',');
  if (params.unassigned) query['unassigned'] = 'true';
  if (params.search.trim().length > 0) query['q'] = params.search.trim();
  if (params.page > 1) query['page'] = String(params.page);
  if (params.pageSize !== DEFAULT_LIST_PARAMS.pageSize) query['size'] = String(params.pageSize);
  if (params.sortBy !== DEFAULT_LIST_PARAMS.sortBy) query['sort'] = params.sortBy;
  if (!params.sortDescending) query['dir'] = 'asc';

  return query;
}

/** True when the query string carries any of the keys this list owns. */
export function hasListParams(query: Params): boolean {
  return ['status', 'priority', 'unassigned', 'q', 'page', 'size', 'sort', 'dir'].some(
    (key) => query[key] != null,
  );
}

/**
 * Reads list state back out of a query string. Unknown statuses, unsortable fields, page sizes
 * the server would reject and non-numeric pages are all dropped in favour of the default, so a
 * mangled URL shows the normal list instead of an error.
 */
export function fromQueryParams(query: Params): RequestListParams {
  return {
    status: readEnum(query['status'], STATUSES),
    priority: readEnum(query['priority'], PRIORITIES),
    unassigned: query['unassigned'] === 'true',
    search: typeof query['q'] === 'string' ? query['q'].slice(0, 200) : '',
    page: readPositiveInt(query['page'], DEFAULT_LIST_PARAMS.page),
    pageSize: PAGE_SIZES.includes(Number(query['size']))
      ? Number(query['size'])
      : DEFAULT_LIST_PARAMS.pageSize,
    sortBy: SORTABLE_FIELDS.includes(query['sort']) ? query['sort'] : DEFAULT_LIST_PARAMS.sortBy,
    sortDescending: query['dir'] !== 'asc',
  };
}

function readEnum<T extends RequestStatus | RequestPriority>(
  raw: unknown,
  allowed: readonly T[],
): T[] {
  if (typeof raw !== 'string' || raw.length === 0) return [];

  const seen = new Set<T>();
  for (const part of raw.split(',')) {
    const match = allowed.find((value) => value === part.trim());
    if (match) seen.add(match);
  }
  return [...seen];
}

function readPositiveInt(raw: unknown, fallback: number): number {
  const value = Number(raw);
  return Number.isInteger(value) && value >= 1 ? value : fallback;
}
