import { expect, test } from '@playwright/test';
import { Api, unique } from './api';

test('a vacancy becomes an application that keeps its posting snapshot', async ({ page }) => {
  const api = new Api(page.request);
  const id = unique();
  const company = await api.company(`Convert Co ${id}`);
  const vacancy = await api.vacancy(`Platform Engineer ${id}`, company.id, {
    description: '## About the role\n\nBuild the **platform**.',
    requiredTechnologies: ['C#', 'Kubernetes'],
    workArrangement: 'Remote',
    postedSalary: { min: 80000, max: 95000, currency: 'EUR', period: 'Annual', type: 'Gross' },
  });

  await page.goto(`/vacancies/${vacancy.id}`);
  await expect(page.getByRole('heading', { name: `Platform Engineer ${id}` })).toBeVisible();
  await page.getByRole('button', { name: 'Convert to application' }).click();

  const dialog = page.getByRole('dialog', { name: 'Convert to application' });
  await dialog.getByLabel('Initial status').selectOption('ReadyToApply');
  await dialog.getByLabel('Priority').selectOption('High');
  await dialog.getByRole('button', { name: 'Create application' }).click();

  await expect(page).toHaveURL(/\/applications\/\d+$/);
  const applicationId = Number(page.url().split('/').pop());
  await expect(page.locator('app-status-badge').first()).toHaveText('Ready to Apply');
  await expect(page.getByText('Created as Ready to Apply')).toBeVisible();
  await expect(page.getByText('Kubernetes')).toBeVisible();

  // The vacancy now links to its application and cannot be converted twice.
  await page.goto(`/vacancies/${vacancy.id}`);
  await expect(page.getByRole('link', { name: /Open application/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Convert to application' })).toHaveCount(0);

  // Editing the vacancy later does not change the application's snapshot.
  await api.put(`/api/vacancies/${vacancy.id}`, { title: `Renamed ${id}`, companyId: company.id });
  await page.goto(`/applications/${applicationId}`);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Renamed ${id}`);
  await expect(page.locator('dd', { hasText: `Platform Engineer ${id}` })).toBeVisible();
  expect((await api.getApplication(applicationId)).snapshot.jobTitle).toBe(`Platform Engineer ${id}`);
});
