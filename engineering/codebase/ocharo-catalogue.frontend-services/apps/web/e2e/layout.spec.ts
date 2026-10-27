import type { Page } from '@playwright/test';
import { runToken } from './support/catalogueApi';
import { expect, horizontalOverflow, test } from './support/fixtures';
import { seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';

const viewports = [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 820, height: 1180 },
  { name: 'desktop', width: 1440, height: 900 },
] as const;

/** Visible controls whose box extends past the viewport's right edge, which a reader cannot reach without panning. */
function clippedControls(page: Page): Promise<string[]> {
  return page.evaluate(() =>
    [...document.querySelectorAll('button, a[href], input, textarea, [role="combobox"]')]
      .filter((element) => (element as HTMLElement).checkVisibility?.() !== false)
      .filter((element) => !element.closest('.canvas-scroll, .preview-viewport, [data-preview-viewport]'))
      .filter((element) => {
        const box = element.getBoundingClientRect();
        return box.width > 0 && box.right > window.innerWidth + 1;
      })
      .map((element) =>
        (element.getAttribute('aria-label') ?? element.textContent ?? element.tagName).trim().slice(0, 60),
      ),
  );
}

test('gallery, both designers and the public reader fit phone, tablet and desktop widths', async ({
  page,
  request,
  t,
}) => {
  const token = runToken();
  const products = await seedGarments(request, token);
  const { document } = await seedCatalogue(request, {
    title: `Layout ${token}`,
    slug: `layout-${token}`,
    locale: 'en',
    outputs: { web: true, pdf: true },
    web: [{ title: 'Looks', productIds: [products.a!.document.id, products.b!.document.id] }],
    pdf: [{ productId: products.a!.document.id }],
  });
  const places = [
    {
      name: 'gallery',
      path: '/catalogues',
      ready: page.getByRole('button', { name: t('New catalogue'), exact: true }),
    },
    { name: 'web-designer', path: `/catalogues/${document.id}/web`, ready: page.locator('main.web-catalogue') },
    { name: 'pdf-designer', path: `/catalogues/${document.id}/pdf`, ready: page.locator('.canvas-sheet') },
  ];
  const problems: Record<string, unknown> = {};
  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    for (const place of places) {
      await page.goto(place.path);
      await expect(place.ready.first()).toBeVisible();
      await page.waitForLoadState('networkidle');
      const overflow = await horizontalOverflow(page),
        clipped = await clippedControls(page);
      if (overflow > 0 || clipped.length) problems[`${place.name} @ ${viewport.width}`] = { overflow, clipped };
      await page.screenshot({ path: test.info().outputPath(`${place.name}-${viewport.name}.png`), fullPage: true });
    }
  }
  expect(problems, 'pages wider than the screen or controls past its edge').toEqual({});
});
