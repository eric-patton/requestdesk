import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { SummaryReport } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { STATUS_CLASS, STATUS_LABEL } from '../../core/status';
import { EmptyState } from '../../shared/empty-state';
import { Bar, BarChart } from './bar-chart';

interface Tile {
  label: string;
  value: string;
  icon: string;
  hint: string;
  link?: string;
  query?: Record<string, string>;
}

/** The admin's view of the queue. Every number comes from one SQL query on the server. */
@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    BarChart,
    EmptyState,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly report = signal<SummaryReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly tiles = computed<Tile[]>(() => {
    const r = this.report();
    if (!r) return [];

    return [
      {
        label: 'Open',
        value: String(r.openCount),
        icon: 'inbox',
        hint: 'Not yet closed or cancelled',
        link: '/requests',
      },
      {
        label: 'In progress',
        value: String(r.inProgressCount),
        icon: 'play_circle',
        hint: 'Being worked right now',
        link: '/requests',
      },
      {
        label: 'Blocked',
        value: String(r.blockedCount),
        icon: 'block',
        hint: 'Waiting on a part, a vendor or the customer',
        link: '/requests',
      },
      {
        label: 'Resolved, last 7 days',
        value: String(r.resolvedThisWeek),
        icon: 'task_alt',
        hint: 'Distinct requests marked resolved this week',
      },
      {
        label: 'Opened, last 7 days',
        value: String(r.createdThisWeek),
        icon: 'add_circle',
        hint: 'New requests this week',
      },
      {
        label: 'Median open age',
        value: r.medianOpenAgeHours === undefined ? 'n/a' : formatHours(r.medianOpenAgeHours),
        icon: 'schedule',
        hint: 'Half the open requests are older than this',
      },
    ];
  });

  protected readonly byStatus = computed<Bar[]>(() =>
    (this.report()?.byStatus ?? []).map((s) => ({
      label: STATUS_LABEL[s.status],
      value: s.count,
      color: `var(--status-${STATUS_CLASS[s.status]}-dot)`,
    })),
  );

  protected readonly byPriority = computed<Bar[]>(() =>
    (this.report()?.openByPriority ?? []).map((p) => ({
      label: p.priority,
      value: p.count,
      color: { Low: '#94a3b8', Normal: '#0ea5e9', High: '#f97316', Urgent: '#dc2626' }[p.priority],
    })),
  );

  protected readonly aging = computed<Bar[]>(() =>
    (this.report()?.aging ?? []).map((bucket, index) => ({
      label: bucket.label,
      value: bucket.count,
      color: ['#10b981', '#84cc16', '#f59e0b', '#f97316', '#dc2626'][index] ?? '#64748b',
    })),
  );

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.summary().subscribe({
      next: (report) => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(describeError(error, 'The dashboard could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}

function formatHours(hours: number): string {
  if (hours < 1) return `${Math.round(hours * 60)} min`;
  if (hours < 48) return `${Math.round(hours)} h`;
  return `${Number((hours / 24).toFixed(1))} days`;
}
