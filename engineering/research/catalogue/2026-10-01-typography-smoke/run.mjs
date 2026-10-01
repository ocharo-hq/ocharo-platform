import fs from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, resolve, join } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath, pathToFileURL } from 'node:url';

const evidenceDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(evidenceDirectory, '../../../..');
const app = join(repository, 'engineering/codebase/brand-workspace.frontend-services/apps/web');
const require = createRequire(join(app, 'package.json'));
const { build } = await import(pathToFileURL(require.resolve('vite')).href);
// Optional external Playwright installation keeps verification dependencies out of the application lockfile.
const playwrightEntry = process.env.OCHARO_PLAYWRIGHT_MODULE
  ? resolve(process.env.OCHARO_PLAYWRIGHT_MODULE)
  : require.resolve('playwright');
const { chromium } = await import(pathToFileURL(playwrightEntry).href);
const output = process.argv[2] ? resolve(process.argv[2]) : await fs.mkdtemp(join(tmpdir(), 'ocharo-typography-'));
await fs.mkdir(output, { recursive: true });
await build({
  root: app, configFile: false, resolve: { alias: { '@': join(app, 'src') } },
  build: {
    outDir: join(output, 'dist'), emptyOutDir: true, minify: false,
    lib: { entry: join(evidenceDirectory, 'fixture.ts'), name: 'TypographySmoke', formats: ['iife'], fileName: () => 'smoke.js' },
  },
});
const browser = await chromium.launch({ channel: process.env.OCHARO_BROWSER_CHANNEL || 'chrome', headless: true });
try {
  const page = await browser.newPage();
  const script = await fs.readFile(join(output, 'dist/smoke.js'), 'utf8');
  // Serve the fixture from memory; no HTTP server, user profile, authentication or live API is used.
  await page.route('**/*', route => route.request().url() === 'https://typography.ocharo.test/'
    ? route.fulfill({ contentType: 'text/html', body: '<!doctype html><html><head><meta charset="utf-8"></head><body><script>' + script.replaceAll('</script', '<\\/script') + '</script></body></html>' })
    : route.abort());
  await page.goto('https://typography.ocharo.test/');
  const result = await page.evaluate(() => window.runTypography());
  await fs.writeFile(join(output, 'catalogue-typography.pdf'), Buffer.from(result.pdf, 'base64'));
  for (const item of result.pages) await fs.writeFile(join(output, `${item.id}.png`), Buffer.from(item.png, 'base64'));
  const evidence = { browser: await browser.version(), report: result.report, layouts: result.layouts };
  await fs.writeFile(join(output, 'results.json'), JSON.stringify(evidence, null, 2) + '\n');
  console.log(JSON.stringify({ output, ...evidence }, null, 2));
  if (result.report.issues.length) throw new Error('Unexpected typography preflight warnings');
} finally {
  await browser.close();
}
