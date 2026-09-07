import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, firstValueFrom, shareReplay, tap } from 'rxjs';
import { AuthResult } from './models';

const STORAGE_KEY = 'requestdesk.session';

/**
 * Holds the signed-in session. The access token is short-lived and the refresh token rotates on
 * every use, so the interceptor calls `refresh()` when a request comes back 401.
 *
 * The session lives in sessionStorage: it survives a reload and dies with the tab. A production
 * deployment of this design would move the refresh token into an httpOnly cookie; for a demo, the
 * trade-off for a simpler same-origin setup is documented in the README.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<AuthResult | null>(readSession());
  private inflightRefresh: Observable<AuthResult> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly role = computed(() => this.user()?.role ?? null);
  readonly customerId = computed(() => this.session()?.customerId ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isStaff = computed(() => this.role() === 'Admin' || this.role() === 'Agent');
  readonly isAdmin = computed(() => this.role() === 'Admin');

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this.session()?.refreshToken ?? null;
  }

  async login(email: string, password: string): Promise<AuthResult> {
    const result = await firstValueFrom(
      this.http.post<AuthResult>('/api/auth/login', { email, password }),
    );
    this.set(result);
    return result;
  }

  /** Exchange the refresh token for a new pair. Concurrent callers share one in-flight request. */
  refresh(): Observable<AuthResult> {
    if (this.inflightRefresh) {
      return this.inflightRefresh;
    }

    const refreshToken = this.refreshToken;

    this.inflightRefresh = this.http.post<AuthResult>('/api/auth/refresh', { refreshToken }).pipe(
      tap((result) => this.set(result)),
      finalize(() => (this.inflightRefresh = null)),
      shareReplay(1),
    );

    return this.inflightRefresh;
  }

  logout(): void {
    this.session.set(null);
    sessionStorage.removeItem(STORAGE_KEY);
    void this.router.navigate(['/login']);
  }

  /** Where this user lands after signing in. */
  homeUrl(): string {
    return this.isAdmin() ? '/dashboard' : '/requests';
  }

  private set(result: AuthResult): void {
    this.session.set(result);
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(result));
  }
}

function readSession(): AuthResult | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResult) : null;
  } catch {
    return null;
  }
}
