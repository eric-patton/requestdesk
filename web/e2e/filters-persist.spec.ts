import { expect, test } from '@playwright/test';

/**
 * Filters have to survive leaving the list and coming back. Setting them again every time you
 * look at a ticket is the thing that makes a queue tiring to work in, and it is not something a
 * unit test can prove: it depends on the router, the history stack and the links.
 */
test('filters survive opening a request and coming back, by every route back', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Continue as Agent' }).click();
  await expect(page).toHaveURL(/\/requests$/);
  await expect(page.getByRole('table')).toBeVisible();

  // Narrow the queue down, the way somebody working it would.
  await page.getByRole('option', { name: 'In progress' }).click();
  await page.getByRole('option', { name: 'Urgent' }).click();

  // The filters are in the address bar, which is what makes the rest of this work.
  await expect(page).toHaveURL(/status=InProgress/);
  await expect(page).toHaveURL(/priority=Urgent/);

  const filtered = await page.getByRole('row').count();
  expect(filtered).toBeGreaterThan(1);

  const expectStillFiltered = async () => {
    await expect(page).toHaveURL(/status=InProgress/);
    await expect(page.getByRole('option', { name: 'In progress' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await expect(page.getByRole('option', { name: 'Urgent' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  };

  // 1. The back button.
  await page.getByRole('row').nth(1).click();
  await expect(page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/);
  await page.goBack();
  await expectStillFiltered();

  // 2. The "All requests" link on a request, which is a plain link and used to lose them.
  await page.getByRole('row').nth(1).click();
  await expect(page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/);
  await page.getByRole('link', { name: 'All requests' }).click();
  await expectStillFiltered();

  // 3. Requests in the toolbar, from somewhere else entirely.
  await page.getByRole('link', { name: 'New request' }).first().click();
  await expect(page.getByRole('heading', { name: 'New request' })).toBeVisible();
  await page.getByRole('link', { name: 'Requests', exact: true }).click();
  await expectStillFiltered();

  // 4. A reload, because the state is in the URL rather than in memory.
  await page.reload();
  await expectStillFiltered();

  // Clearing really does clear, including out of the address bar.
  await page.getByRole('button', { name: 'Clear filters' }).click();
  await expect(page).not.toHaveURL(/status=/);
  await expect(page.getByRole('option', { name: 'In progress' })).toHaveAttribute(
    'aria-selected',
    'false',
  );
});

test('a filtered list can be shared as a link', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Continue as Agent' }).click();
  await expect(page).toHaveURL(/\/requests$/);

  // Somebody pastes a link to the blocked queue, sorted oldest first.
  await page.goto('/requests?status=Blocked&sort=title&dir=asc');

  await expect(page.getByRole('option', { name: 'Blocked' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByRole('table')).toBeVisible();
});

test('a mangled query string shows the normal list rather than an error', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Continue as Agent' }).click();
  await expect(page).toHaveURL(/\/requests$/);

  await page.goto('/requests?status=Nonsense&sort=password&size=99999&page=-3');

  await expect(page.getByRole('table')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Requests', exact: true })).toBeVisible();
});
