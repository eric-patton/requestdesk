import { defineConfig, devices } from '@playwright/test';

/**
 * Two projects against a running RequestDesk (the compose stack on http://localhost:8085 by default):
 *
 * - `chromium`: the end-to-end tests CI runs.
 * - `screenshots`: regenerates the README screenshots and demo video. Not run by CI.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env['BASE_URL'] ?? 'http://localhost:8085',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      testIgnore: /screenshots/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'screenshots',
      testMatch: /screenshots/,
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1600, height: 960 },
        deviceScaleFactor: 1,
        video: { mode: 'on', size: { width: 1280, height: 800 } },
        colorScheme: 'light',
      },
    },
  ],
});
