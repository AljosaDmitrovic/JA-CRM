import { expect, test as setup } from '@playwright/test';

setup('sign in once', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Password').fill('e2e-password-123');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  await page.context().storageState({ path: '.auth/owner.json' });
});
