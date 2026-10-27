import { writeFile } from 'node:fs/promises';
import type { Page } from '@playwright/test';
import { releases, runToken, seedProduct, setProductLocale } from './support/catalogueApi';
import { download, expect, horizontalOverflow, test, timed } from './support/fixtures';
import { pngSize } from './support/images';
import { inspectPdf, readDownload, renderPdf } from './support/pdf';
import { toJpeg } from './support/pixels';
import { groupedImages } from './support/products';
import { seedCatalogue } from './support/seed';
import { designerStatus, openDesigner } from './support/ui';
import { readWebModel } from './support/webModel';

async function prepareAndPublish(page: Page, t: (source: string) => string) {
  await page.getByText(t('Publication and saved history')).click();
  const prepared = page.getByRole('status').filter({ hasText: t('Prepared for review. Publish when ready.') });
  await page.getByRole('button', { name: t('Prepare release') }).click();
  const warning = page.getByRole('alertdialog').getByRole('button', { name: t('Continue export') });
  await expect(prepared.or(warning)).toBeVisible({ timeout: 90_000 });
  if (await warning.isVisible()) await warning.click();
  await expect(prepared).toBeVisible({ timeout: 90_000 });
  await page.getByRole('button', { name: t('Publish catalogue'), exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: t('Publish catalogue') })
    .click();
  await expect(page.getByRole('status').filter({ hasText: t('Catalogue published.') })).toBeVisible();
}

test('a missing translation is omitted from web and PDF; the release serves its reader and downloads', async ({
  page,
  request,
  context,
  diagnostics,
  t,
}) => {
  const token = runToken(),
    slug = `release-${token}`,
    title = `Release ${token}`;
  const colors = { a: [200, 150, 120], b: [120, 160, 130], c: [130, 140, 190] } as const;
  const seed = (key: 'a' | 'b' | 'c', ru: string | null, garment: 'nimcha' | 'baska' | 'korset') =>
    seedProduct(request, {
      variant: `${token}${key}`,
      garment,
      locales: {
        en: { name: `Garment ${key.toUpperCase()}` },
        ...(ru ? { ru: { name: ru, fabric: 'Лён' } } : {}),
      },
      images: groupedImages(colors[key]),
      prices: [400_000 + key.charCodeAt(0), 350_000],
    });
  const a = await seed('a', 'Льняной жилет', 'nimcha'),
    b = await seed('b', null, 'baska'),
    c = await seed('c', 'Кружевной корсет', 'korset');
  const { document } = await seedCatalogue(request, {
    title,
    slug,
    locale: 'ru',
    outputs: { web: true, pdf: true },
    description: 'Проверка выпуска.',
    web: [
      { title: 'Образы', productIds: [a.document.id, b.document.id] },
      { title: 'Только перевод', productIds: [b.document.id] },
    ],
    pdf: [{ productId: a.document.id }, { productId: b.document.id }, { productId: c.document.id, layout: 'collage' }],
  });

  // Draft designers keep the untranslated product visible and say why it will be left out.
  await openDesigner(page, document.id, 'web', t('Web designer'));
  await page.locator('.page-panel ol.pages > li').first().locator('.page-name').click();
  const draft = page.locator('main.web-catalogue');
  await expect(draft.locator('article').filter({ hasText: b.document.id })).toContainText(t('Needs review'));
  await openDesigner(page, document.id, 'pdf', t('PDF designer'));
  await expect(page.locator('.excluded')).toContainText(b.document.id);
  await expect(page.locator('.page-rail ol > li')).toHaveCount(4);

  const exported = await download(
    page,
    () => page.getByRole('button', { name: t('Download PDF'), exact: true }).click(),
    t('Continue export'),
  );
  const exportedPdf = await inspectPdf(await readDownload(await exported.path()));
  expect(exportedPdf.pageCount, 'designer export omits the untranslated page').toBe(3);

  await timed(diagnostics, 'Prepare and publish, 3 PDF pages', () => prepareAndPublish(page, t));
  const [release] = await releases(request, document.id);
  expect(release?.publishedAtUtc).toBeTruthy();

  // The public reader: web view first.
  await page.getByRole('link', { name: t('Open public catalogue') }).click();
  await expect(page).toHaveURL(new RegExp(`/c/${slug}\\?locale=ru$`));
  const reader = page.locator('article.web-reader');
  await expect(reader.locator('.photographs img').first()).toBeVisible();
  const web = await readWebModel(reader, 'reader');
  expect(web.sections.map((section) => [section.title, section.products.map((product) => product.name)])).toEqual([
    ['Образы', ['Льняной жилет']],
  ]);
  await expect(page.locator('section.published-catalogue')).toHaveAttribute('lang', 'ru');
  await page.screenshot({ path: test.info().outputPath('reader-web.png'), fullPage: true });

  // PDF view and downloads.
  await page.getByRole('button', { name: t('PDF pages') }).click();
  const figures = page.locator('.pdf-grid figure');
  await expect(figures).toHaveCount(3);
  await expect(figures.locator('figcaption > span')).toHaveText([
    `1 · ${title}`,
    '2 · Льняной жилет',
    '3 · Кружевной корсет',
  ]);
  for (const image of await figures.locator('img').all()) {
    await expect(image).toBeVisible();
    expect(await image.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBeGreaterThan(1000);
  }
  const whole = await download(
    page,
    () => page.getByRole('link', { name: t('Download catalogue PDF') }).click(),
    t('Continue export'),
  );
  expect(whole.suggestedFilename(), "released PDF keeps the reader's file name").toBe(`ocharo-${slug}-ru.pdf`);
  const wholeBytes = await readDownload(await whole.path());
  await writeFile(test.info().outputPath('released-catalogue.pdf'), wholeBytes);
  const released = await inspectPdf(wholeBytes);
  expect(released.pageCount).toBe(3);
  expect(released.pages.map(({ widthMm, heightMm }) => [widthMm, heightMm])).toEqual([
    [210, 297],
    [210, 297],
    [210, 297],
  ]);
  expect(released.title).toBe(title);
  for (const [index, image] of (await renderPdf(wholeBytes, 1)).entries())
    await writeFile(test.info().outputPath(`released-page-${index + 1}.jpg`), await toJpeg(context, image, 700));

  const second = await download(
    page,
    () =>
      figures
        .nth(1)
        .getByRole('link', { name: t('Download page PDF') })
        .click(),
    t('Continue export'),
  );
  expect(second.suggestedFilename(), "released page keeps the reader's file name").toBe(`ocharo-${slug}-2.pdf`);
  const page2 = await inspectPdf(await readDownload(await second.path()));
  expect(page2.pageCount).toBe(1);
  expect(page2.pages[0]).toMatchObject({ widthMm: 210, heightMm: 297 });
  const png = await page.request.get((await figures.nth(1).locator('img').getAttribute('src'))!);
  expect(png.headers()['content-type']).toBe('image/png');
  const size = pngSize(await png.body());
  expect(size.width / size.height).toBeCloseTo(210 / 297, 2);

  for (const viewport of [
    { width: 390, height: 844 },
    { width: 1440, height: 900 },
  ]) {
    await page.setViewportSize(viewport);
    expect(await horizontalOverflow(page), `reader overflow at ${viewport.width}px`).toBeLessThanOrEqual(0);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: test.info().outputPath('reader-pdf-390.png') });

  // The release is immutable: revoking a published product's translation later does not change what it serves.
  await setProductLocale(request, a.document.id, 'ru', 'draft');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.reload();
  await page.getByRole('button', { name: t('Read online') }).click();
  const after = await readWebModel(page.locator('article.web-reader'), 'reader');
  expect(after.sections.map((section) => section.products.map((product) => product.name))).toEqual([['Льняной жилет']]);
});

test('the release is pinned: republishing replaces the edition and keeps the earlier release link working', async ({
  page,
  request,
  t,
}) => {
  const token = runToken(),
    slug = `pinned-${token}`;
  const product = await seedProduct(request, {
    variant: `${token}p`,
    locales: { en: { name: 'Pinned Vest' } },
    images: groupedImages([180, 150, 140]),
  });
  const { document } = await seedCatalogue(request, {
    title: `Pinned ${token}`,
    slug,
    locale: 'en',
    outputs: { web: true, pdf: false },
    web: [{ title: 'First edition', productIds: [product.document.id] }],
  });
  await openDesigner(page, document.id, 'web', t('Web designer'));
  await prepareAndPublish(page, t);
  const [first] = await releases(request, document.id);

  const field = page.getByRole('textbox', { name: t('Page title') });
  await field.fill('Second edition');
  await field.press('Tab');
  await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
  await expect(designerStatus(page)).toContainText(`${t('Saved')} · ${t('Revision')} 2`);
  await page.getByRole('button', { name: t('Prepare release') }).click();
  await expect(
    page.getByRole('status').filter({ hasText: t('Prepared for review. Publish when ready.') }),
  ).toBeVisible();
  await page.getByRole('button', { name: t('Publish catalogue'), exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: t('Publish catalogue') })
    .click();
  await expect(page.getByRole('status').filter({ hasText: t('Catalogue published.') })).toBeVisible();

  await page.goto(`/c/${slug}?locale=en`);
  await expect(page.locator('section.collection-page h2')).toHaveText('Second edition');
  await page.goto(`/c/${slug}?locale=en&release=${first!.id}`);
  await expect(page.locator('section.collection-page h2')).toHaveText('First edition');
});
