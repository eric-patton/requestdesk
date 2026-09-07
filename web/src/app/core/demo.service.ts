import { Injectable, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { ApiService } from './api.service';
import { DemoInfo } from './models';

/** Whether the API is running in demo mode, and the details the banner and login screen show. */
@Injectable({ providedIn: 'root' })
export class DemoService {
  private readonly api = inject(ApiService);

  readonly info = signal<DemoInfo | null>(null);
  readonly loaded = signal(false);

  load(): void {
    this.api
      .demo()
      .pipe(catchError(() => of(null)))
      .subscribe((info) => {
        this.info.set(info?.enabled ? info : null);
        this.loaded.set(true);
      });
  }
}
