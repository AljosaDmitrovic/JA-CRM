import { defineConfig, devices } from '@playwright/test';
import { resolve } from 'node:path';

/**
 * End-to-end tests for the critical flows. They run against the production Angular build served
 * by the real API, with a fresh SQLite database in e2e/.data (deleted before every run).
 *
 *   cd web && npx ng build      # once, or after frontend changes
 *   cd e2e && npm test
 */
const PORT = 5181;
// Absolute paths: the API resolves relative ones against its own project folder.
const DATA_DIR = resolve('.data');
const WEB_ROOT = resolve('../web/dist/web/browser');
export const BASE_URL = `http://localhost:${PORT}`;

// Test-only password ("e2e-password-123"). The hash is not a secret; the database is throwaway.
const PASSWORD_HASH = 'AQAAAAIAAYagAAAAEHUiEmAHhtoqp3K92edkfdbkW3hAAHTl5K/7+RA9TeOzUaMvAOSb9p/uBlfuBq4BuQ==';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], storageState: '.auth/owner.json' },
      dependencies: ['setup'],
    },
  ],
  webServer: {
    command: `rm -rf .data && dotnet run --project ../src/JaCrm.Api --no-launch-profile -- --urls ${BASE_URL} --webroot ${WEB_ROOT}`,
    url: `${BASE_URL}/healthz`,
    timeout: 120_000,
    reuseExistingServer: false,
    env: {
      App__DataDirectory: DATA_DIR,
      Auth__PasswordHash: PASSWORD_HASH,
      Auth__SecureCookie: 'false',
      Auth__LoginAttemptsPerMinute: '100',
    },
  },
});
