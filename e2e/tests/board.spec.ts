import { Locator, Page, expect, test } from '@playwright/test';
import { Api, unique } from './api';

function column(page: Page, status: string): Locator {
  return page.locator('.column').filter({ has: page.locator('.column-head .label', { hasText: new RegExp(`^${status}$`) }) });
}

/** CDK drag and drop needs real pointer movement past its threshold, in several steps. */
async function drag(page: Page, from: Locator, to: Locator): Promise<void> {
  const source = (await from.boundingBox())!;
  const target = (await to.boundingBox())!;
  await page.mouse.move(source.x + source.width / 2, source.y + 20);
  await page.mouse.down();
  await page.mouse.move(source.x + source.width / 2 + 10, source.y + 30, { steps: 5 });
  await page.mouse.move(target.x + target.width / 2, target.y + 60, { steps: 20 });
  await page.mouse.up();
}

test('cards move between statuses by drag and drop and by the keyboard menu', async ({ page }) => {
  const api = new Api(page.request);
  const id = unique();
  const title = `Board Role ${id}`;
  const { id: applicationId } = await api.application(title, `Board Co ${id}`);

  await page.goto('/applications?view=board');
  const card = page.locator('app-application-card', { hasText: title });
  await expect(column(page, 'Interested').locator('app-application-card', { hasText: title })).toBeVisible();

  // 1. Drag and drop: Interested -> Applied.
  await drag(page, card, column(page, 'Applied'));
  await expect(column(page, 'Applied').locator('app-application-card', { hasText: title })).toBeVisible();
  await expect(page.getByText('Moved to Applied.')).toBeVisible();
  await expect.poll(async () => (await api.getApplication(applicationId)).status).toBe('Applied');

  // 2. Keyboard only: focus the card, M opens "Move to…", arrows pick the status, Enter moves it.
  await card.focus();
  await page.keyboard.press('m');
  const menu = page.getByRole('menu');
  await expect(menu).toBeVisible();
  for (let i = 0; i < 15; i++) {
    const focused = await page.evaluate(() => document.activeElement?.textContent?.trim() ?? '');
    if (focused.startsWith('Technical Interview')) break;
    await page.keyboard.press('ArrowDown');
  }
  await page.keyboard.press('Enter');
  await expect(menu).toBeHidden();
  await expect(column(page, 'Technical Interview').locator('app-application-card', { hasText: title })).toBeVisible();

  // Every move is persisted and recorded in the status history.
  await expect.poll(async () => (await api.getApplication(applicationId)).status).toBe('TechnicalInterview');
  const { history } = await api.getApplication(applicationId);
  expect(history.map((h) => `${h.fromStatus}->${h.toStatus}`).reverse()).toEqual([
    'null->Interested',
    'Interested->Applied',
    'Applied->TechnicalInterview',
  ]);

  // The board reflects the stored state after a reload.
  await page.reload();
  await expect(column(page, 'Technical Interview').locator('app-application-card', { hasText: title })).toBeVisible();
});
