import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RequestStatus } from '../core/models';
import { STATUS_CLASS, STATUS_LABEL } from '../core/status';

@Component({
  selector: 'app-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-chip status-{{ cssClass() }}">{{ label() }}</span>`,
  styles: `
    .status-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 10px 2px 8px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      font-weight: 600;
      white-space: nowrap;
      background: var(--status-bg);
      color: var(--status-fg);
      border: 1px solid var(--status-border);
    }
    .status-chip::before {
      content: '';
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--status-dot);
    }
  `,
})
export class StatusChip {
  readonly status = input.required<RequestStatus>();
  readonly label = computed(() => STATUS_LABEL[this.status()]);
  readonly cssClass = computed(() => STATUS_CLASS[this.status()]);
}
