import { expect, test } from '@playwright/test';

/**
 * The one journey that proves the stack: sign in as an agent, open a request for a customer, move it
 * through a status, watch the history row appear, leave a comment, sign out.
 */
test('an agent opens a request, triages it and the history records it', async ({ page }) => {
  const title = `Playwright: hallway light flickering ${Date.now()}`;

  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'RequestDesk' })).toBeVisible();

  await page.getByRole('button', { name: 'Continue as Agent' }).click();
  await expect(page).toHaveURL(/\/requests$/);
  await expect(page.getByRole('heading', { name: 'Requests', exact: true })).toBeVisible();
  await expect(page.getByRole('table')).toBeVisible();

  // Open a request on behalf of the first customer.
  await page.getByRole('link', { name: 'New request' }).first().click();
  await expect(page.getByRole('heading', { name: 'New request' })).toBeVisible();

  await page.getByLabel('Customer').click();
  await page.getByRole('option').first().click();
  await page.getByLabel('Title').fill(title);
  await page
    .getByLabel('Description')
    .fill('The fixture between floors two and three flickers constantly after dark.');
  await page.getByLabel('Priority').click();
  await page.getByRole('option', { name: /High/ }).click();
  await page.getByRole('button', { name: 'Open request' }).click();

  // The detail page shows the new request as New, with the creation row in the timeline.
  await expect(page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);
  await expect(page.getByText(/^RD-\d{6}/)).toBeVisible();
  await expect(page.getByText('opened this request')).toBeVisible();

  // Triage it with a reason.
  await page.getByRole('button', { name: 'Triage' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await dialog.getByLabel('Reason (optional)').fill('Confirmed with the site manager.');
  await dialog.getByRole('button', { name: 'Triage' }).click();

  await expect(page.getByText('is now Triaged')).toBeVisible();
  await expect(page.getByText('moved this from')).toBeVisible();
  await expect(page.getByText('Confirmed with the site manager.')).toBeVisible();

  // The buttons now reflect the legal moves from Triaged, and only those.
  await expect(page.getByRole('button', { name: 'Start work' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Cancel request' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Triage' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Resolve' })).toHaveCount(0);

  // A comment lands in the same timeline.
  await page.getByLabel('Add a comment').fill('Picking this up this afternoon.');
  await page.getByRole('button', { name: 'Comment' }).click();
  await expect(page.getByText('Picking this up this afternoon.')).toBeVisible();

  // Sign out.
  await page.getByRole('button', { name: /Signed in as/ }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test('a customer sees only their own requests and cannot reach the dashboard', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Continue as Customer' }).click();
  await expect(page).toHaveURL(/\/requests$/);

  // No customer column for a customer, and no dashboard link.
  await expect(page.getByRole('columnheader', { name: 'Customer' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Dashboard' })).toHaveCount(0);

  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/requests$/);
});

test('an admin sees the dashboard with every status listed', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Continue as Admin' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

  const statusChart = page.getByRole('list', { name: 'Requests by status' });
  await expect(statusChart).toBeVisible();
  for (const label of [
    'New',
    'Triaged',
    'In progress',
    'Blocked',
    'Resolved',
    'Closed',
    'Cancelled',
  ]) {
    await expect(statusChart.getByText(label, { exact: true })).toBeVisible();
  }
});
