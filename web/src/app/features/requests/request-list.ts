import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipListboxChange, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import {
  RequestListItem,
  RequestListParams,
  RequestPriority,
  RequestStatus,
} from '../../core/models';
import { describeError } from '../../core/problem-details';
import { RequestListState } from '../../core/request-list-state.service';
import {
  DEFAULT_LIST_PARAMS,
  fromQueryParams,
  hasListParams,
  toQueryParams,
} from '../../core/request-list-url';
import { PRIORITIES, STATUSES, STATUS_LABEL } from '../../core/status';
import { EmptyState } from '../../shared/empty-state';
import { PriorityBadge } from '../../shared/priority-badge';
import { RowNavigationDirective } from '../../shared/row-navigation.directive';
import { StatusChip } from '../../shared/status-chip';
import { TimeAgoPipe } from '../../shared/time-ago.pipe';

/**
 * The request list. Every filter, the search, the sort and the page are sent to the server; the
 * table only ever holds one page. Rows are focusable and the arrow keys move between them.
 *
 * The filter state lives in the query string, so the back button, a refresh and a pasted link all
 * show the same list. Changes replace the current history entry rather than pushing a new one:
 * otherwise the back button would undo one filter at a time instead of leaving the list.
 */
@Component({
  selector: 'app-request-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTooltipModule,
    StatusChip,
    PriorityBadge,
    TimeAgoPipe,
    EmptyState,
    RowNavigationDirective,
  ],
  templateUrl: './request-list.html',
  styleUrl: './request-list.scss',
})
export class RequestList implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly listState = inject(RequestListState);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly auth = inject(AuthService);
  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly statusLabel = STATUS_LABEL;

  protected readonly params = signal<RequestListParams>({ ...DEFAULT_LIST_PARAMS });
  protected readonly rows = signal<RequestListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly search = new FormControl('', { nonNullable: true });

  protected readonly columns = computed(() =>
    this.auth.isStaff()
      ? ['referenceNumber', 'title', 'customer', 'priority', 'status', 'assignee', 'updatedAt']
      : ['referenceNumber', 'title', 'priority', 'status', 'updatedAt'],
  );

  protected readonly hasFilters = computed(() => {
    const p = this.params();
    return (
      p.status.length > 0 || p.priority.length > 0 || p.unassigned || p.search.trim().length > 0
    );
  });

  protected readonly summary = computed(() => {
    const total = this.total();
    if (total === 0) return 'No requests';
    const p = this.params();
    const from = (p.page - 1) * p.pageSize + 1;
    const to = Math.min(p.page * p.pageSize, total);
    return `Showing ${from} to ${to} of ${total} request${total === 1 ? '' : 's'}`;
  });

  ngOnInit(): void {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((search) => this.update({ search, page: 1 }));

    this.restore();
    this.load();
  }

  /**
   * Works out what the list should be showing. The URL wins, because it is the thing a person
   * can see and share. Failing that, the filters from earlier in this session, so following a
   * plain link back to /requests does not silently drop the queue somebody just narrowed down.
   */
  private restore(): void {
    const fromUrl = this.route.snapshot.queryParams;
    const remembered = this.listState.lastQuery();

    if (hasListParams(fromUrl)) {
      this.params.set(fromQueryParams(fromUrl));
    } else if (hasListParams(remembered)) {
      this.params.set(fromQueryParams(remembered));
      this.syncUrl();
    }

    // The search box is not driven by the signal, so it has to be told separately, and without
    // firing valueChanges or it would immediately reload what was just restored.
    this.search.setValue(this.params().search, { emitEvent: false });
  }

  protected onStatusFilter(event: MatChipListboxChange): void {
    this.update({ status: (event.value ?? []) as RequestStatus[], page: 1 });
  }

  protected onPriorityFilter(event: MatChipListboxChange): void {
    this.update({ priority: (event.value ?? []) as RequestPriority[], page: 1 });
  }

  protected onUnassigned(checked: boolean): void {
    this.update({ unassigned: checked, page: 1 });
  }

  protected onSort(sort: Sort): void {
    if (!sort.direction) {
      this.update({
        sortBy: DEFAULT_LIST_PARAMS.sortBy,
        sortDescending: DEFAULT_LIST_PARAMS.sortDescending,
        page: 1,
      });
      return;
    }
    this.update({ sortBy: sort.active, sortDescending: sort.direction === 'desc', page: 1 });
  }

  protected onPage(event: PageEvent): void {
    this.update({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected clearFilters(): void {
    this.search.setValue('', { emitEvent: false });
    this.update({
      ...DEFAULT_LIST_PARAMS,
      sortBy: this.params().sortBy,
      sortDescending: this.params().sortDescending,
    });
  }

  protected open(row: RequestListItem): void {
    void this.router.navigate(['/requests', row.id]);
  }

  protected rowLabel(row: RequestListItem): string {
    return `${row.referenceNumber}: ${row.title}, ${STATUS_LABEL[row.status]}`;
  }

  private update(patch: Partial<RequestListParams>): void {
    this.params.update((current) => ({ ...current, ...patch }));
    this.syncUrl();
    this.load();
  }

  private syncUrl(): void {
    const query = toQueryParams(this.params());
    this.listState.remember(query);

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: query,
      replaceUrl: true,
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.listRequests(this.params()).subscribe({
      next: (page) => {
        this.rows.set(page.items);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(describeError(error, 'The request list could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
