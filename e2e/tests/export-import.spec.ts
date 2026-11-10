import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';
import { Api, unique } from './api';

/** The export without its timestamp, for comparing two exports. */
function comparable(json: string): unknown {
  const { exportedAt: _, ...rest } = JSON.parse(json) as Record<string, unknown>;
  return rest;
}

test('export, then import, restores everything without loss', async ({ page }) => {
  const api = new Api(page.request);
  const id = unique();

  // Varied data: rates, a rich vacancy, an application with history, salaries and notes.
  await api.put('/api/settings/exchange-rates', { rates: [{ currency: 'CHF', rateToBase: 1.0525 }, { currency: 'RSD', rateToBase: 0.00854 }] });
  const company = await api.company(`Export Co ${id}`, { website: 'export.example', rating: 4, size: 'From51To200', notes: '**Bold** note' });
  const vacancy = await api.vacancy(`Export Role ${id}`, company.id, {
    description: '# Role\n\n- one\n- two, "quoted"\n\nÜnïcödé ✓',
    requiredTechnologies: ['Go', 'gRPC'],
    preferredTechnologies: ['Rust'],
    postedSalary: { min: null, max: 9000, currency: 'CHF', period: 'Monthly', type: 'Gross' },
    applicationDeadline: '2027-01-15',
  });
  const { id: applicationId } = await api.post<{ id: number }>(`/api/vacancies/${vacancy.id}/convert`, { status: 'Applied', priority: 'High' });
  await api.post(`/api/applications/${applicationId}/status`, { status: 'RecruiterInterview', note: 'Call on Monday' });
  await api.put(`/api/applications/${applicationId}`, {
    priority: 'High',
    notes: 'Prep: system design',
    expectedSalary: { amount: 8500.5, currency: 'CHF', period: 'Monthly', type: 'Gross' },
    cvVersion: 'EU v5',
  });

  // Export through the UI.
  await page.goto('/settings');
  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('link', { name: 'Export JSON' }).click()]);
  const exportPath = test.info().outputPath('export.json');
  await download.saveAs(exportPath);
  const original = await readFile(exportPath, 'utf8');
  expect(JSON.parse(original).schemaVersion).toBe(1);

  // A change after the export, which the import must undo.
  await api.company(`Created After Export ${id}`);

  // Import through the UI, with the confirmation.
  const [chooser] = await Promise.all([page.waitForEvent('filechooser'), page.getByRole('button', { name: 'Import JSON…' }).click()]);
  await chooser.setFiles(exportPath);
  const confirm = page.getByRole('alertdialog', { name: 'Replace all data?' });
  await confirm.getByRole('button', { name: 'Replace everything' }).click();
  await expect(page.getByText(/^Imported \d+ companies/)).toBeVisible();

  // Exporting again yields exactly the same data.
  const again = await page.request.get('/api/data/export');
  expect(comparable(await again.text())).toEqual(comparable(original));

  // And the app shows the restored records.
  await page.goto(`/applications/${applicationId}`);
  await expect(page.getByRole('heading', { name: `Export Role ${id}` })).toBeVisible();
  await expect(page.getByText('Call on Monday')).toBeVisible();
  await page.goto('/companies?archived=All&search=Created%20After%20Export');
  await expect(page.getByText('No companies match')).toBeVisible();
});
