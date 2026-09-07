import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

/**
 * The whole theme system rests on one attribute reaching the root element, so that is what these
 * check. If `data-ep-theme` is wrong, every `light-dark()` token in the design system resolves to
 * the wrong half and the application and the site stop agreeing with each other.
 */
describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.removeItem('ep-theme');
    document.documentElement.removeAttribute('data-ep-theme');
    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.removeItem('ep-theme');
    document.documentElement.removeAttribute('data-ep-theme');
  });

  it('follows the system by default and sets no attribute', () => {
    expect(service.preference()).toBe('system');
    expect(document.documentElement.hasAttribute('data-ep-theme')).toBe(false);
  });

  it('cycles system, light, dark and back to system', () => {
    service.cycle();
    expect(service.preference()).toBe('light');
    expect(document.documentElement.getAttribute('data-ep-theme')).toBe('light');

    service.cycle();
    expect(service.preference()).toBe('dark');
    expect(document.documentElement.getAttribute('data-ep-theme')).toBe('dark');

    service.cycle();
    expect(service.preference()).toBe('system');
    expect(document.documentElement.hasAttribute('data-ep-theme')).toBe(false);
  });

  it('remembers an explicit choice and forgets a return to system', () => {
    service.set('dark');
    expect(localStorage.getItem('ep-theme')).toBe('dark');

    service.set('system');
    expect(localStorage.getItem('ep-theme')).toBeNull();
  });

  it('restores a stored choice when it starts up', () => {
    localStorage.setItem('ep-theme', 'light');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});

    const restored = TestBed.inject(ThemeService);

    expect(restored.preference()).toBe('light');
    expect(document.documentElement.getAttribute('data-ep-theme')).toBe('light');
  });

  it('describes the current state and the next one, for the screen reader label', () => {
    expect(service.label()).toContain('following your system');
    expect(service.icon()).toBe('contrast');

    service.set('dark');
    expect(service.label()).toContain('dark');
    expect(service.icon()).toBe('dark_mode');
  });
});
