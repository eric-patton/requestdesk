import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Bar {
  label: string;
  value: number;
  /** A CSS colour or variable for this bar. */
  color?: string;
}

/**
 * Horizontal bars, no chart library. Each row is real text, so a screen reader gets the label and
 * the number without an image description, and the bar itself is decoration.
 */
@Component({
  selector: 'app-bar-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="bars" [attr.aria-label]="ariaLabel()">
      @for (bar of bars(); track bar.label) {
        <li class="row">
          <span class="label">{{ bar.label }}</span>
          <span class="track" aria-hidden="true">
            <span
              class="fill"
              [style.width.%]="percent(bar.value)"
              [style.background]="bar.color ?? 'var(--mat-sys-primary)'"
            ></span>
          </span>
          <span class="value">{{ bar.value }}</span>
        </li>
      }
    </ol>
  `,
  styles: `
    .bars {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .row {
      display: grid;
      grid-template-columns: minmax(96px, 30%) 1fr 3ch;
      align-items: center;
      gap: 10px;
      font: var(--mat-sys-body-medium);
    }
    .label {
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .track {
      height: 14px;
      border-radius: 7px;
      background: var(--mat-sys-surface-container-high);
      overflow: hidden;
    }
    .fill {
      display: block;
      height: 100%;
      border-radius: 7px;
      min-width: 2px;
      transition: width 300ms ease;
    }
    .value {
      text-align: right;
      font-variant-numeric: tabular-nums;
      font-weight: 600;
    }
  `,
})
export class BarChart {
  readonly bars = input.required<Bar[]>();
  readonly ariaLabel = input<string>('Bar chart');

  private readonly max = computed(() => Math.max(1, ...this.bars().map((b) => b.value)));

  protected percent(value: number): number {
    return (value / this.max()) * 100;
  }
}
