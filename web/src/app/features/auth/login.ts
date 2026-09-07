import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { DemoService } from '../../core/demo.service';
import { DemoAccount, UserRole } from '../../core/models';
import { describeError } from '../../core/problem-details';

/**
 * Sign in. In demo mode this is three buttons: nobody evaluating the app should have to type a
 * password to see it work. The email form is still there for a non-demo deployment.
 */
@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly demo = inject(DemoService);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly showForm = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly blurb: Record<UserRole, string> = {
    Admin: 'Watches the queue. Reassigns, reopens, and sees the dashboard.',
    Agent: 'Works requests. Triages, resolves, and claims unassigned work.',
    Customer: 'Submits requests and follows them. Sees only their own account.',
  };

  protected readonly icon: Record<UserRole, string> = {
    Admin: 'admin_panel_settings',
    Agent: 'engineering',
    Customer: 'person',
  };

  constructor() {
    if (!this.demo.loaded()) {
      this.demo.load();
    }
  }

  protected async signInAs(account: DemoAccount): Promise<void> {
    await this.signIn(account.email, account.password, account.role);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    await this.signIn(email, password, 'form');
  }

  private async signIn(email: string, password: string, key: string): Promise<void> {
    this.busy.set(key);
    this.error.set(null);

    try {
      await this.auth.login(email, password);
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      this.error.set(describeError(error, 'Sign-in failed. Please try again.'));
    } finally {
      this.busy.set(null);
    }
  }
}
