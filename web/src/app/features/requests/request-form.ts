import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LIMITS } from '../../core/limits';
import { CustomerSummary, RequestPriority } from '../../core/models';
import { describeError, fieldErrorsOf } from '../../core/problem-details';
import { PRIORITIES } from '../../core/status';

/**
 * Open a request. The validation here mirrors the server validator field for field, and any
 * failure the server still reports is mapped back onto the matching control.
 */
@Component({
  selector: 'app-request-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
  ],
  templateUrl: './request-form.html',
  styleUrl: './request-form.scss',
})
export class RequestForm {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly limits = LIMITS;
  protected readonly priorities = PRIORITIES;
  protected readonly customers = signal<CustomerSummary[]>([]);
  protected readonly saving = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly submitted = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(LIMITS.title)]],
    description: ['', [Validators.required, Validators.maxLength(LIMITS.description)]],
    priority: ['Normal' as RequestPriority, Validators.required],
    customerId: ['', this.auth.isStaff() ? [Validators.required] : []],
  });

  protected readonly priorityHelp: Record<RequestPriority, string> = {
    Low: 'Whenever there is time.',
    Normal: 'Within the usual service window.',
    High: 'Disrupting work; needs attention soon.',
    Urgent: 'Safety, security or the business is stopped.',
  };

  constructor() {
    if (this.auth.isStaff()) {
      this.api.listCustomers().subscribe((customers) => this.customers.set(customers));
    }
  }

  /** The list the error summary reads out. Only fields that are actually invalid right now. */
  protected errorSummary(): { control: string; label: string; message: string }[] {
    const c = this.form.controls;
    const items: { control: string; label: string; message: string }[] = [];

    if (c.title.invalid)
      items.push({ control: 'title', label: 'Title', message: this.titleError() });
    if (c.description.invalid)
      items.push({
        control: 'description',
        label: 'Description',
        message: this.descriptionError(),
      });
    if (c.customerId.invalid)
      items.push({
        control: 'customerId',
        label: 'Customer',
        message: 'Choose the customer this request is for.',
      });
    if (c.priority.invalid)
      items.push({ control: 'priority', label: 'Priority', message: 'Pick a priority.' });

    return items;
  }

  protected titleError(): string {
    const c = this.form.controls.title;
    if (c.hasError('server')) return c.getError('server') as string;
    if (c.hasError('required')) return 'A title is required.';
    if (c.hasError('maxlength')) return `Keep the title under ${LIMITS.title} characters.`;
    return '';
  }

  protected descriptionError(): string {
    const c = this.form.controls.description;
    if (c.hasError('server')) return c.getError('server') as string;
    if (c.hasError('required')) return 'A description is required.';
    if (c.hasError('maxlength'))
      return `Keep the description under ${LIMITS.description} characters.`;
    return '';
  }

  protected customerName(id: string): string {
    return this.customers().find((c) => c.id === id)?.name ?? '';
  }

  protected focus(controlName: string): void {
    document.getElementById(`field-${controlName}`)?.focus();
  }

  protected submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);

    this.api
      .createRequest({
        title: value.title.trim(),
        description: value.description.trim(),
        priority: value.priority,
        customerId: this.auth.isStaff() ? value.customerId : undefined,
      })
      .subscribe({
        next: (created) => void this.router.navigate(['/requests', created.id]),
        error: (error: unknown) => {
          this.saving.set(false);
          const fieldErrors = fieldErrorsOf(error);
          let mapped = false;

          for (const [field, messages] of Object.entries(fieldErrors)) {
            const control = this.form.get(field);
            if (control) {
              control.setErrors({ server: messages.join(' ') });
              control.markAsTouched();
              mapped = true;
            }
          }

          if (!mapped) {
            this.serverError.set(describeError(error, 'The request could not be saved.'));
          }
        },
      });
  }
}
