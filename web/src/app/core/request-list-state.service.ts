import { Injectable, signal } from '@angular/core';
import { Params } from '@angular/router';

/**
 * Remembers the query string the request list was last showing.
 *
 * The URL already carries the filters, which covers the back button and a refresh. This covers
 * the other two ways people get back to the list: the "All requests" link on a request, and
 * Requests in the toolbar. Both are plain links to `/requests`, and without this they would drop
 * you into an unfiltered queue seconds after you had narrowed it down, which is the thing that
 * makes a ticket list annoying to work in.
 *
 * Deliberately in memory only. It should survive moving around the application, not outlive the
 * tab: coming back tomorrow to yesterday's filters, with no clue why the list looks short, is
 * worse than starting clean.
 */
@Injectable({ providedIn: 'root' })
export class RequestListState {
  private readonly query = signal<Params>({});

  /** The query params to send somebody back to the list they were last looking at. */
  readonly lastQuery = this.query.asReadonly();

  remember(query: Params): void {
    this.query.set(query);
  }
}
