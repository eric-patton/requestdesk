import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RequestPriority } from '../core/models';

@Component({
  selector: 'app-priority-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="priority priority-{{ priority().toLowerCase() }}">{{
    priority()
  }}</span>`,
  styles: `
    .priority {
      display: inline-block;
      padding: 1px 8px;
      border-radius: 6px;
      font: var(--mat-sys-label-small);
      font-weight: 600;
      letter-spacing: 0.02em;
      text-transform: uppercase;
      background: var(--priority-bg);
      color: var(--priority-fg);
    }
  `,
})
export class PriorityBadge {
  readonly priority = input.required<RequestPriority>();
}
