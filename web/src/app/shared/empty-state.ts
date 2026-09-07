import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="empty" role="status">
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
      <p class="headline">{{ headline() }}</p>
      @if (detail()) {
        <p class="detail">{{ detail() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 48px 24px;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
    mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--ep-ink-3);
    }
    .headline {
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
      margin: 0;
    }
    .detail {
      margin: 0;
      max-width: 40ch;
    }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly headline = input.required<string>();
  readonly detail = input<string>();
}
