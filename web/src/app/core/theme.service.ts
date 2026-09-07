import { Injectable, signal } from '@angular/core';

/**
 * What the user asked for, which is not the same as what they see. "system" means follow the
 * operating system, and is the default.
 */
export type ThemePreference = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'ep-theme';
const ORDER: readonly ThemePreference[] = ['system', 'light', 'dark'];

/**
 * Theme switching for the whole application.
 *
 * The work here is deliberately tiny: every colour in the design system is declared once as a
 * `light-dark()` pair, so the browser picks the half it needs from the `color-scheme` in force.
 * Setting one attribute on the root element re-resolves all of them, Angular Material's own
 * variables included, which is why there is no palette to swap and no class to toggle on each
 * component.
 *
 * The matching pre-paint script in index.html applies a stored choice before the first frame, so
 * the page never flashes the wrong theme on load.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly current = signal<ThemePreference>(this.read());

  readonly preference = this.current.asReadonly();

  constructor() {
    this.apply(this.current());
  }

  /** Advances system to light to dark and back to system. */
  cycle(): void {
    const next = ORDER[(ORDER.indexOf(this.current()) + 1) % ORDER.length];
    this.set(next);
  }

  set(preference: ThemePreference): void {
    this.current.set(preference);
    this.apply(preference);

    try {
      if (preference === 'system') {
        localStorage.removeItem(STORAGE_KEY);
      } else {
        localStorage.setItem(STORAGE_KEY, preference);
      }
    } catch {
      // Storage is unavailable in private windows and when site data is blocked. The theme still
      // applies for this page; it just will not be remembered, which is the right way to fail.
    }
  }

  /** The Material Symbols glyph describing the current preference. */
  icon(): string {
    switch (this.current()) {
      case 'light':
        return 'light_mode';
      case 'dark':
        return 'dark_mode';
      default:
        return 'contrast';
    }
  }

  /** Announces the current state and what activating the control will do next. */
  label(): string {
    switch (this.current()) {
      case 'light':
        return 'Theme: light. Activate for dark.';
      case 'dark':
        return 'Theme: dark. Activate to follow your system.';
      default:
        return 'Theme: following your system. Activate for light.';
    }
  }

  private read(): ThemePreference {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'light' || saved === 'dark') return saved;
    } catch {
      // Same as above: an unreadable store just means no saved preference.
    }
    return 'system';
  }

  private apply(preference: ThemePreference): void {
    const root = document.documentElement;
    if (preference === 'system') {
      root.removeAttribute('data-ep-theme');
    } else {
      root.setAttribute('data-ep-theme', preference);
    }
  }
}
