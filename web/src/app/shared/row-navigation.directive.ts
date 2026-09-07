import { Directive, ElementRef, inject } from '@angular/core';

/**
 * Arrow-key navigation between focusable rows in a table. Put it on the `<table>`; rows carry
 * `tabindex="0"`. Home and End jump to the first and last row. Enter and Space are left to the row.
 */
@Directive({
  selector: 'table[appRowNavigation]',
  host: {
    '(keydown)': 'onKeydown($event)',
  },
})
export class RowNavigationDirective {
  private readonly host = inject<ElementRef<HTMLTableElement>>(ElementRef);

  onKeydown(event: KeyboardEvent): void {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;

    const rows = Array.from(
      this.host.nativeElement.querySelectorAll<HTMLTableRowElement>('tbody tr[tabindex]'),
    );
    if (rows.length === 0) return;

    const current = rows.findIndex(
      (row) => row === document.activeElement || row.contains(document.activeElement),
    );
    let next: number;

    switch (event.key) {
      case 'ArrowDown':
        next = current < 0 ? 0 : Math.min(current + 1, rows.length - 1);
        break;
      case 'ArrowUp':
        next = current < 0 ? rows.length - 1 : Math.max(current - 1, 0);
        break;
      case 'Home':
        next = 0;
        break;
      default:
        next = rows.length - 1;
    }

    event.preventDefault();
    rows[next].focus();
  }
}
