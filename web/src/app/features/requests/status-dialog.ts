import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { LIMITS } from '../../core/limits';
import { RequestStatus } from '../../core/models';
import { STATUS_LABEL, transitionIcon, transitionVerb } from '../../core/status';
import { StatusChip } from '../../shared/status-chip';

export interface StatusDialogData {
  referenceNumber: string;
  from: RequestStatus;
  to: RequestStatus;
}

/** Confirms a status change and collects the optional reason that goes into the history row. */
@Component({
  selector: 'app-status-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    StatusChip,
  ],
  template: `
    <h2 mat-dialog-title>{{ verb }} {{ data.referenceNumber }}</h2>
    <mat-dialog-content>
      <p class="move">
        <app-status-chip [status]="data.from" />
        <mat-icon aria-hidden="true">arrow_forward</mat-icon>
        <app-status-chip [status]="data.to" />
        <span class="visually-hidden">from {{ label[data.from] }} to {{ label[data.to] }}</span>
      </p>
      <mat-form-field appearance="outline" class="reason">
        <mat-label>Reason (optional)</mat-label>
        <textarea
          matInput
          [formControl]="reason"
          rows="3"
          [maxlength]="maxReason"
          cdkFocusInitial
          placeholder="What changed, for whoever reads the history later"
        ></textarea>
        <mat-hint align="end">{{ reason.value.length }} / {{ maxReason }}</mat-hint>
        @if (reason.hasError('maxlength')) {
          <mat-error>Keep the reason under {{ maxReason }} characters.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="ref.close()">Keep as {{ label[data.from] }}</button>
      <button mat-flat-button type="button" (click)="confirm()" [disabled]="reason.invalid">
        <mat-icon aria-hidden="true">{{ icon }}</mat-icon>
        {{ verb }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .move {
      display: flex;
      align-items: center;
      gap: 12px;
      margin: 0 0 16px;
    }
    .reason {
      width: min(480px, 100%);
    }
    mat-dialog-content {
      min-width: min(480px, 90vw);
    }
  `,
})
export class StatusDialog {
  protected readonly ref = inject<MatDialogRef<StatusDialog, string | null>>(MatDialogRef);
  protected readonly data = inject<StatusDialogData>(MAT_DIALOG_DATA);
  protected readonly label = STATUS_LABEL;
  protected readonly maxReason = LIMITS.reason;
  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(LIMITS.reason)],
  });

  protected get verb(): string {
    return transitionVerb(this.data.from, this.data.to);
  }

  protected get icon(): string {
    return transitionIcon(this.data.from, this.data.to);
  }

  protected confirm(): void {
    if (this.reason.invalid) return;
    this.ref.close(this.reason.value.trim() || null);
  }
}
