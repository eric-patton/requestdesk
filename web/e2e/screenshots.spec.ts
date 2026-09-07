import { expect, test } from '@playwright/test';
import { copyFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Not a test of the product: this drives a running stack and captures the README screenshots and
 * the demo video, so they can be regenerated with one command and never drift from the real UI.
 *
 *   BASE_URL=http://localhost:8085 npx playwright test --project screenshots
 */

const out = resolve(__dirname, '../../docs/screenshots');
mkdirSync(out, { recursive: true });

async function signInAs(page: import('@playwright/test').Page, role: string) {
  await page.goto('/login');
  await page.getByRole('button', { name: `Continue as ${role}` }).click();
  await page.waitForURL(/\/(requests|dashboard)$/);
}

test('00 login', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('button', { name: 'Continue as Agent' })).toBeVisible();
  await page.screenshot({ path: `${out}/00-login.png` });
});

test('01 request list with filters', async ({ page }) => {
  await signInAs(page, 'Agent');
  await page.getByRole('option', { name: 'In progress' }).click();
  await page.getByRole('option', { name: 'Blocked' }).click();
  await page.getByRole('option', { name: 'High' }).click();
  await page.getByRole('option', { name: 'Urgent' }).click();
  await expect(page.getByText(/Showing \d+ to \d+ of/)).toBeVisible();
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${out}/01-request-list.png` });
});

test('02 request detail with a long history', async ({ page }) => {
  await signInAs(page, 'Admin');

  // Pick the seeded request with the most comments; it will have the richest timeline.
  const list = await page.request.get('/api/requests?pageSize=100&status=Closed&status=Resolved', {
    headers: { Authorization: `Bearer ${await accessToken(page)}` },
  });
  const items = (await list.json()).items as { id: string; commentCount: number }[];
  items.sort((a, b) => b.commentCount - a.commentCount);

  await page.goto(`/requests/${items[0].id}`);
  await expect(page.getByRole('heading', { name: 'Timeline' })).toBeVisible();
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${out}/02-request-detail.png`, fullPage: true });
});

test('03 new request form showing validation', async ({ page }) => {
  await signInAs(page, 'Agent');
  await page.goto('/requests/new');
  await page.getByLabel('Title').fill('x'.repeat(30));
  await page.getByLabel('Title').fill('');
  await page.getByRole('button', { name: 'Open request' }).click();
  await expect(page.getByRole('alert')).toContainText('Fix');
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${out}/03-new-request-validation.png` });
});

test('04 admin dashboard', async ({ page }) => {
  await signInAs(page, 'Admin');
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Requests by status' })).toBeVisible();
  await page.waitForTimeout(600);
  await page.screenshot({ path: `${out}/04-dashboard.png`, fullPage: true });
});

test('05 openapi reference', async ({ page }) => {
  await page.goto('/scalar/');
  await expect(page.getByRole('heading', { name: /RequestDesk API/ }).first()).toBeVisible({
    timeout: 30_000,
  });
  // Open the Requests group so the operation list is what the picture shows.
  await page.getByRole('link', { name: 'Requests', exact: true }).first().click();
  await expect(page.getByText('/api/requests/{id}/status').first()).toBeVisible({
    timeout: 30_000,
  });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${out}/05-openapi.png` });
});

test('06 record the demo video', async ({ page }) => {
  test.setTimeout(120_000);

  await signInAs(page, 'Agent');
  await page.waitForTimeout(800);

  await page.getByRole('link', { name: 'New request' }).first().click();
  await page.getByLabel('Customer').click();
  await page.waitForTimeout(300);
  await page.getByRole('option').first().click();
  await typeSlowly(page, 'Title', 'Conference room projector shows no signal');
  await typeSlowly(
    page,
    'Description',
    'Laptop connects but the projector reports no signal on HDMI 1. Started this morning.',
    8,
  );
  await page.getByLabel('Priority').click();
  await page.waitForTimeout(300);
  await page.getByRole('option', { name: /High/ }).click();
  await page.waitForTimeout(500);
  await page.getByRole('button', { name: 'Open request' }).click();

  await expect(page.getByText('opened this request')).toBeVisible();
  await page.waitForTimeout(1200);

  await page.getByRole('button', { name: 'Triage' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await page.waitForTimeout(400);
  await dialog
    .getByLabel('Reason (optional)')
    .pressSequentially('Confirmed with the office manager.', { delay: 25 });
  await page.waitForTimeout(400);
  await dialog.getByRole('button', { name: 'Triage' }).click();

  await expect(page.getByText('moved this from')).toBeVisible();
  await page.waitForTimeout(2500);

  const video = page.video();
  await page.close();
  const path = await video!.path();
  copyFileSync(path, `${out}/demo.webm`);
});

async function accessToken(page: import('@playwright/test').Page): Promise<string> {
  const raw = await page.evaluate(() => sessionStorage.getItem('requestdesk.session'));
  return (JSON.parse(raw!) as { accessToken: string }).accessToken;
}

async function typeSlowly(
  page: import('@playwright/test').Page,
  label: string,
  text: string,
  delay = 40,
) {
  const field = page.getByLabel(label);
  await field.click();
  await field.pressSequentially(text, { delay });
  await page.waitForTimeout(300);
}
