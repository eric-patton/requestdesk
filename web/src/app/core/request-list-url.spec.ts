import { RequestListParams } from './models';
import {
  DEFAULT_LIST_PARAMS,
  fromQueryParams,
  hasListParams,
  toQueryParams,
} from './request-list-url';

/**
 * The query string is the list's memory: it is what the back button restores, what a refresh
 * keeps, and what somebody pastes into a message. It is also user input, so the parsing side has
 * to treat a hand-edited URL as hostile rather than pass it straight to the API.
 */
describe('request list query string', () => {
  const params = (patch: Partial<RequestListParams> = {}): RequestListParams => ({
    ...DEFAULT_LIST_PARAMS,
    ...patch,
  });

  describe('writing', () => {
    it('writes nothing when nothing has been changed', () => {
      expect(toQueryParams(params())).toEqual({});
    });

    it('writes only what differs from the default', () => {
      const query = toQueryParams(params({ status: ['Blocked'], priority: ['Urgent'] }));

      expect(query).toEqual({ status: 'Blocked', priority: 'Urgent' });
    });

    it('writes the filters, the page, the size and the sort', () => {
      const query = toQueryParams(
        params({
          status: ['New', 'Triaged'],
          unassigned: true,
          search: 'printer',
          page: 3,
          pageSize: 50,
          sortBy: 'title',
          sortDescending: false,
        }),
      );

      expect(query).toEqual({
        status: 'New,Triaged',
        unassigned: 'true',
        q: 'printer',
        page: '3',
        size: '50',
        sort: 'title',
        dir: 'asc',
      });
    });

    it('ignores a search of only whitespace', () => {
      expect(toQueryParams(params({ search: '   ' }))).toEqual({});
    });
  });

  describe('reading', () => {
    it('round-trips a filtered list', () => {
      const original = params({
        status: ['InProgress', 'Blocked'],
        priority: ['High'],
        unassigned: true,
        search: 'thermostat',
        page: 2,
        pageSize: 100,
        sortBy: 'customer',
        sortDescending: false,
      });

      expect(fromQueryParams(toQueryParams(original))).toEqual(original);
    });

    it('falls back to the default for an empty query', () => {
      expect(fromQueryParams({})).toEqual(DEFAULT_LIST_PARAMS);
    });

    it('drops statuses and priorities it does not recognise', () => {
      const restored = fromQueryParams({ status: 'Blocked,Nonsense', priority: 'Sideways' });

      expect(restored.status).toEqual(['Blocked']);
      expect(restored.priority).toEqual([]);
    });

    it('refuses a sort field the server would reject', () => {
      expect(fromQueryParams({ sort: 'password' }).sortBy).toBe(DEFAULT_LIST_PARAMS.sortBy);
    });

    it('refuses a page size the server would reject', () => {
      expect(fromQueryParams({ size: '5000' }).pageSize).toBe(DEFAULT_LIST_PARAMS.pageSize);
    });

    it('refuses a page that is not a positive whole number', () => {
      expect(fromQueryParams({ page: '0' }).page).toBe(1);
      expect(fromQueryParams({ page: '-4' }).page).toBe(1);
      expect(fromQueryParams({ page: 'first' }).page).toBe(1);
    });

    it('caps a search long enough to be rejected by the API', () => {
      expect(fromQueryParams({ q: 'x'.repeat(500) }).search.length).toBe(200);
    });

    it('does not repeat a status listed twice', () => {
      expect(fromQueryParams({ status: 'New,New' }).status).toEqual(['New']);
    });
  });

  describe('detecting', () => {
    it('knows a query string that carries list state', () => {
      expect(hasListParams({ status: 'New' })).toBe(true);
      expect(hasListParams({ page: '2' })).toBe(true);
    });

    it('ignores an empty one, or one carrying somebody else’s parameters', () => {
      expect(hasListParams({})).toBe(false);
      expect(hasListParams({ utm_source: 'email' })).toBe(false);
    });
  });
});
