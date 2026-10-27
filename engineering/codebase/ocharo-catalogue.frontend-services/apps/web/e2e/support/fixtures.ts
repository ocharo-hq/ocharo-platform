import { writeFile } from 'node:fs/promises';
import { test as base, expect, type Download, type Page } from '@playwright/test';
import { translateUi, type UiLocale } from '../../src/domain/shared/UiMessages';

export interface Diagnostics {
  console: { type: string; text: string; url: string }[];
  pageErrors: string[];
  failedRequests: { url: string; failure: string }[];
  errorResponses: { url: string; status: number; method: string }[];
  timings: { label: string; ms: number }[];
}

interface CatalogueFixtures {
  /** Interface language stored before the app boots; tests that switch it use the Settings page. */
  uiLocale: UiLocale;
  /** Console messages that a test expects, such as a deliberately rejected request. */
  allowedConsole: RegExp[];
  diagnostics: Diagnostics;
  /** Translates an English interface phrase into the test's interface language. */
  t: (source: string, variables?: Record<string, string | number>) => string;
}

const ignoredConsole = [/\[vite\]/, /Download the Vue Devtools/];

export const test = base.extend<CatalogueFixtures>({
  uiLocale: ['en', { option: true }],
  allowedConsole: [[], { option: true }],
  t: async ({ uiLocale }, use) => use((source, variables) => translateUi(source, uiLocale, variables)),
  diagnostics: [
    async ({ context, uiLocale, allowedConsole }, use, testInfo) => {
      const diagnostics: Diagnostics = {
        console: [],
        pageErrors: [],
        failedRequests: [],
        errorResponses: [],
        timings: [],
      };
      await context.addInitScript((locale) => {
        try {
          if (!localStorage.getItem('ocharo.operator.locale')) localStorage.setItem('ocharo.operator.locale', locale);
        } catch {
          /* about:blank analysis pages have no storage. */
        }
      }, uiLocale);
      const watch = (page: Page) => {
        page.on('console', (message) => {
          if (!['error', 'warning'].includes(message.type())) return;
          if (ignoredConsole.some((pattern) => pattern.test(message.text()))) return;
          diagnostics.console.push({ type: message.type(), text: message.text(), url: page.url() });
        });
        page.on('pageerror', (error) => diagnostics.pageErrors.push(`${error.name}: ${error.message}`));
        page.on('requestfailed', (request) => {
          const failure = request.failure()?.errorText ?? 'failed';
          // Navigations away from a page abort its pending image and module requests; those are not app failures.
          if (failure !== 'net::ERR_ABORTED')
            diagnostics.failedRequests.push({ url: new URL(request.url()).pathname, failure });
        });
        page.on('response', (response) => {
          if (response.status() >= 400)
            diagnostics.errorResponses.push({
              url: new URL(response.url()).pathname,
              status: response.status(),
              method: response.request().method(),
            });
        });
      };
      context.pages().forEach(watch);
      context.on('page', watch);
      await use(diagnostics);
      const body = JSON.stringify(diagnostics, null, 2);
      await writeFile(testInfo.outputPath('diagnostics.json'), body);
      await testInfo.attach('diagnostics', { body, contentType: 'application/json' });
      const unexpected = diagnostics.console.filter(
        (entry) => entry.type === 'error' && !allowedConsole.some((pattern) => pattern.test(entry.text)),
      );
      expect.soft(diagnostics.pageErrors, 'uncaught page errors').toEqual([]);
      expect.soft(unexpected, 'unexpected console errors').toEqual([]);
    },
    { auto: true },
  ],
});
export { expect };

/** Measures one user-visible operation and records it with the test's diagnostics. */
export async function timed<T>(diagnostics: Diagnostics, label: string, action: () => Promise<T>): Promise<T> {
  const started = performance.now();
  try {
    return await action();
  } finally {
    const ms = Math.round(performance.now() - started);
    diagnostics.timings.push({ label, ms });
    test.info().annotations.push({ type: 'timing', description: `${label}: ${(ms / 1000).toFixed(1)} s` });
  }
}

/** Accepts the app's confirmation modal when it appears; export checks only ask when they find warnings. */
export async function confirmIfAsked(page: Page, confirmLabel: string, timeout = 1500): Promise<boolean> {
  const button = page.getByRole('alertdialog').getByRole('button', { name: confirmLabel, exact: true });
  try {
    await button.waitFor({ state: 'visible', timeout });
  } catch {
    return false;
  }
  await button.click();
  return true;
}

/** Clicks an export button and waits for its file, accepting a preflight warning when one is raised. */
export async function download(page: Page, click: () => Promise<void>, continueLabel: string, timeout = 90_000) {
  const pending = page.waitForEvent('download', { timeout });
  await click();
  const warned = await confirmIfAsked(page, continueLabel);
  const file: Download = await pending;
  return Object.assign(file, { warned });
}

/** True when the document is wider than the viewport, the usual sign of a layout break on a phone. */
export async function horizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
}
