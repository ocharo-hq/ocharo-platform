import { defineConfig } from '@playwright/test';

/**
 * Browser suite for the Catalogue designers. By default it starts its own API and Vite server on separate ports with a
 * temporary store; CATALOGUE_E2E_URL points it at an already running Catalogue instead, whose data it then writes to.
 */
const external = process.env.CATALOGUE_E2E_URL;
const webPort = Number(process.env.CATALOGUE_E2E_WEB_PORT ?? 18282);
const apiPort = Number(process.env.CATALOGUE_E2E_API_PORT ?? 18278);

export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results/e2e',
  timeout: 180_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: Number(process.env.CATALOGUE_E2E_WORKERS ?? 2),
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL: external ?? `https://localhost:${webPort}`,
    ignoreHTTPSErrors: true,
    acceptDownloads: true,
    browserName: 'chromium',
    locale: 'en-US',
    viewport: { width: 1440, height: 900 },
    actionTimeout: 20_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium' }],
  webServer: external
    ? undefined
    : [
        {
          command: 'node e2e/support/start-api.mjs',
          url: `https://localhost:${apiPort}/api/catalogue/designs`,
          ignoreHTTPSErrors: true,
          reuseExistingServer: false,
          timeout: 300_000,
          env: { CATALOGUE_E2E_API_PORT: String(apiPort) },
          stdout: 'ignore',
        },
        {
          command: `pnpm exec vite --port ${webPort}`,
          url: `https://localhost:${webPort}/`,
          ignoreHTTPSErrors: true,
          reuseExistingServer: false,
          timeout: 120_000,
          env: { VITE_API_TARGET: `https://localhost:${apiPort}` },
        },
      ],
});
