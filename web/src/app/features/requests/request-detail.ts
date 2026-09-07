import { DatePipe, LowerCasePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ATTACHMENTS, LIMITS, formatBytes, isAllowedContentType } from '../../core/limits';
import {
  HistoryEntry,
  RequestAttachment,
  RequestComment,
  RequestDetail,
  RequestStatus,
  UserSummary,
} from '../../core/models';
import { describeError, problemOf } from '../../core/problem-details';
import { RequestListState } from '../../core/request-list-state.service';
import {
  STATUS_CLASS,
  STATUS_LABEL,
  isTerminal,
  transitionIcon,
  transitionVerb,
} from '../../core/status';
import { EmptyState } from '../../shared/empty-state';
import { PriorityBadge } from '../../shared/priority-badge';
import { StatusChip } from '../../shared/status-chip';
import { TimeAgoPipe } from '../../shared/time-ago.pipe';
import { StatusDialog, StatusDialogData } from './status-dialog';

type TimelineItem =
  | { kind: 'status'; at: string; entry: HistoryEntry }
  | { kind: 'comment'; at: string; comment: RequestComment };

/**
 * One request: the description, a merged timeline of status changes and comments, and the
 * controls. Which controls appear is decided entirely by what the API said this user may do.
 */
@Component({
  selector: 'app-request-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    LowerCasePipe,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule,
    MatProgressBarModule,
    MatTooltipModule,
    StatusChip,
    PriorityBadge,
    TimeAgoPipe,
    EmptyState,
  ],
  templateUrl: './request-detail.html',
  styleUrl: './request-detail.scss',
})
export class RequestDetailPage {
  protected readonly listState = inject(RequestListState);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  /** Bound from the route by `withComponentInputBinding`. */
  readonly id = input.required<string>();

  protected readonly auth = inject(AuthService);
  protected readonly request = signal<RequestDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly staff = signal<UserSummary[]>([]);
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly statusClass = STATUS_CLASS;
  protected readonly maxComment = LIMITS.comment;
  protected readonly maxFileSize = formatBytes(ATTACHMENTS.maxSizeBytes);
  protected readonly allowedTypes = ATTACHMENTS.allowedContentTypes.join(',');

  protected readonly comment = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(LIMITS.comment)],
  });

  protected readonly timeline = computed<TimelineItem[]>(() => {
    const r = this.request();
    if (!r) return [];

    const items: TimelineItem[] = [
      ...r.history.map((entry) => ({ kind: 'status' as const, at: entry.occurredAt, entry })),
      ...r.comments.map((comment) => ({
        kind: 'comment' as const,
        at: comment.createdAt,
        comment,
      })),
    ];

    return items.sort((a, b) => a.at.localeCompare(b.at));
  });

  protected readonly transitions = computed(() => {
    const r = this.request();
    if (!r) return [];
    return r.allowedTransitions.map((to) => ({
      to,
      verb: transitionVerb(r.status, to),
      icon: transitionIcon(r.status, to),
      destructive: to === 'Cancelled',
    }));
  });

  protected readonly isTerminal = computed(() => {
    const r = this.request();
    return r ? isTerminal(r.status) : false;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      this.load(id);
    });
  }

  protected formatSize(bytes: number): string {
    return formatBytes(bytes);
  }

  protected changeStatus(to: RequestStatus): void {
    const r = this.request();
    if (!r) return;

    const data: StatusDialogData = { referenceNumber: r.referenceNumber, from: r.status, to };

    this.dialog
      .open<StatusDialog, StatusDialogData, string | null>(StatusDialog, {
        data,
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .subscribe((reason) => {
        if (reason === undefined) return;
        this.run('status', this.api.changeStatus(r.id, to, reason ?? undefined), () =>
          this.snack.open(`${r.referenceNumber} is now ${STATUS_LABEL[to]}.`, 'OK', {
            duration: 4000,
          }),
        );
      });
  }

  protected assign(agentId: string | null): void {
    const r = this.request();
    if (!r || (r.assignedAgent?.id ?? null) === agentId) return;

    this.run('assign', this.api.assign(r.id, agentId), () => {
      const name = agentId ? this.staff().find((s) => s.id === agentId)?.displayName : null;
      this.snack.open(name ? `Assigned to ${name}.` : 'Assignment cleared.', 'OK', {
        duration: 3000,
      });
    });
  }

  protected submitComment(): void {
    const r = this.request();
    if (!r) return;

    if (this.comment.invalid) {
      this.comment.markAsTouched();
      return;
    }

    this.run('comment', this.api.addComment(r.id, this.comment.value), () =>
      this.comment.reset(''),
    );
  }

  protected onFileChosen(event: Event): void {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = '';
    const r = this.request();
    if (!file || !r) return;

    if (file.size > ATTACHMENTS.maxSizeBytes) {
      this.snack.open(
        `That file is ${formatBytes(file.size)}. The limit is ${this.maxFileSize}.`,
        'OK',
        { duration: 5000 },
      );
      return;
    }

    if (!isAllowedContentType(file.type)) {
      this.snack.open(
        `${file.type || 'That file type'} is not accepted. Images, PDFs, text and CSV are.`,
        'OK',
        { duration: 5000 },
      );
      return;
    }

    this.run('upload', this.api.uploadAttachment(r.id, file), () =>
      this.snack.open(`Attached ${file.name}.`, 'OK', { duration: 3000 }),
    );
  }

  protected download(attachment: RequestAttachment): void {
    const r = this.request();
    if (!r) return;

    this.api.downloadAttachment(r.id, attachment.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 10_000);
      },
      error: (error: unknown) =>
        this.snack.open(describeError(error, 'The file could not be downloaded.'), 'OK', {
          duration: 5000,
        }),
    });
  }

  protected trackTimeline(_: number, item: TimelineItem): string {
    return item.kind === 'status' ? item.entry.id : item.comment.id;
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getRequest(id).subscribe({
      next: (request) => {
        this.request.set(request);
        this.loading.set(false);

        if (request.permissions.canChangeAssignment && this.staff().length === 0) {
          this.api.listStaff().subscribe((staff) => this.staff.set(staff));
        }
      },
      error: (error: unknown) => {
        this.error.set(describeError(error, 'This request could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  /** Run a mutation, then reload the request so every control reflects the server's view. */
  private run<T>(
    key: string,
    call: {
      subscribe: (observer: { next: (value: T) => void; error: (e: unknown) => void }) => unknown;
    },
    onDone: (value: T) => void,
  ): void {
    this.busy.set(key);

    call.subscribe({
      next: (value) => {
        onDone(value);
        this.busy.set(null);
        this.load(this.id());
      },
      error: (error: unknown) => {
        this.busy.set(null);
        const problem = problemOf(error);

        if (problem?.legalTransitions) {
          const legal = problem.legalTransitions.map((s) => STATUS_LABEL[s]).join(', ') || 'none';
          this.snack.open(
            `Not allowed from ${STATUS_LABEL[problem.from!]}. Legal moves: ${legal}.`,
            'OK',
            { duration: 6000 },
          );
        } else {
          this.snack.open(describeError(error), 'OK', { duration: 6000 });
        }

        // The server's view won.
        this.load(this.id());
      },
    });
  }
}
