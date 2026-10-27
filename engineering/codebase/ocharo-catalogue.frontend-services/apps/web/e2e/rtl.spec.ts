import { writeFile } from 'node:fs/promises';
import type { Page } from '@playwright/test';
import { catalogueLanguages } from '../src/domain/catalogue/CatalogueLanguages';
import { runToken, seedProduct } from './support/catalogueApi';
import { download, expect, test } from './support/fixtures';
import { inspectPdf, ocr, ocrLanguages, readDownload, renderPdf } from './support/pdf';
import { inkBounds } from './support/pixels';
import { groupedImages } from './support/products';
import { seedCatalogue } from './support/seed';
import { choose, designerStatus, openDesigner } from './support/ui';

/** Synthetic garment copy in each right-to-left catalogue language. */
const samples = [
  {
    code: 'ar',
    ocr: 'ara',
    pieces: 'قطعة',
    name: 'سترة كتان',
    fabric: 'كتان مغسول',
    description: 'سترة واسعة بخياطة يدوية.',
  },
  {
    code: 'fa',
    ocr: 'fas',
    pieces: 'عدد',
    name: 'جلیقه کتان',
    fabric: 'کتان شسته',
    description: 'جلیقه گشاد با دوخت دستی.',
  },
  {
    code: 'ur',
    ocr: 'urd',
    pieces: 'عدد',
    name: 'لینن واسکٹ',
    fabric: 'دھلا ہوا لینن',
    description: 'ہاتھ سے سلی ہوئی کھلی واسکٹ۔',
  },
] as const;
const arabicScript = /[؀-ۿ]/g;

/** Whether an element's text runs right to left and hugs the right edge of its box. */
function rightAligned(page: Page, selector: string) {
  return page
    .locator(selector)
    .first()
    .evaluate((element) => {
      const range = document.createRange();
      range.selectNodeContents(element);
      const text = range.getBoundingClientRect(),
        box = element.getBoundingClientRect();
      return {
        direction: getComputedStyle(element).direction,
        gapRight: Math.round(box.right - text.right),
        gapLeft: Math.round(text.left - box.left),
      };
    });
}

for (const sample of samples)
  test(`${sample.code} content: right-to-left web preview and PDF glyphs`, async ({ page, request, context, t }) => {
    const token = runToken();
    const product = await seedProduct(request, {
      variant: `${token}r`,
      locales: {
        en: { name: 'Linen Vest' },
        [sample.code]: { name: sample.name, fabric: sample.fabric, description: sample.description },
      },
      images: groupedImages([190, 160, 140]),
    });
    const { document } = await seedCatalogue(request, {
      title: `RTL ${sample.code} ${token}`,
      slug: `rtl-${sample.code}-${token}`,
      locale: sample.code,
      outputs: { web: true, pdf: true },
      web: [{ title: 'Looks', productIds: [product.document.id] }],
      pdf: [{ productId: product.document.id }],
    });
    const label = catalogueLanguages.find((language) => language.code === sample.code)!.label;

    await openDesigner(page, document.id, 'web', t('Web designer'));
    await expect(page.getByRole('combobox', { name: t('Catalogue language') })).toContainText(label);
    const web = page.locator('main.web-catalogue');
    await expect(web).toHaveAttribute('dir', 'rtl');
    await expect(web).toHaveAttribute('lang', sample.code);
    await expect(web.locator('h3')).toHaveText(sample.name);
    await expect(web.locator('p.description')).toHaveText(sample.description);
    const heading = await rightAligned(page, 'main.web-catalogue h3');
    expect(heading.direction).toBe('rtl');
    expect(heading.gapRight, 'product name starts at the right edge').toBeLessThanOrEqual(2);
    expect(heading.gapLeft).toBeGreaterThan(heading.gapRight);
    await choose(page, t('Preview size'), `${t('Mobile')} · 390`);
    expect(
      await web.evaluate((node) => node.scrollWidth - node.clientWidth),
      'phone preview overflow',
    ).toBeLessThanOrEqual(1);
    await web.screenshot({ path: test.info().outputPath(`web-${sample.code}-390.png`) });

    await openDesigner(page, document.id, 'pdf', t('PDF designer'));
    await page.locator('.page-rail ol > li').nth(1).locator('.page-name').click();
    const name = page.locator('.canvas-sheet .design-element.is-text').filter({ hasText: sample.name });
    await expect(name).toHaveCSS('direction', 'rtl');
    await expect(name).toHaveCSS('text-align', 'right');

    // A newly added language starts without shared labels; the export check requires its quantity unit.
    await page.getByText(t('Catalogue copy and price display')).click();
    const pieces = page.getByRole('textbox', { name: t('Quantity label'), exact: true });
    await expect(pieces).toHaveValue('');
    await pieces.fill(sample.pieces);
    await pieces.press('Tab');
    await expect(page.locator('.canvas-sheet')).toContainText(sample.pieces);
    const file = await download(
      page,
      () => page.getByRole('button', { name: t('Download page PDF') }).click(),
      t('Continue export'),
    );
    test.info().annotations.push({ type: 'export-check', description: `warned before export: ${file.warned}` });
    const bytes = await readDownload(await file.path());
    await writeFile(test.info().outputPath(`page-${sample.code}.pdf`), bytes);
    const pdf = await inspectPdf(bytes);
    expect(pdf.pageCount).toBe(1);
    expect(pdf.pages[0]).toMatchObject({ widthMm: 210, heightMm: 297 });
    const [image] = await renderPdf(bytes, 2.5);
    await writeFile(test.info().outputPath(`page-${sample.code}.png`), image!);

    // The product name frame: x 14, y 25, 182 × 25 mm on A4. Ink must exist and sit against the right edge.
    const ink = await inkBounds(context, image!, { x: 14 / 210, y: 25 / 297, width: 182 / 210, height: 25 / 297 });
    expect(ink.coverage, 'the product name was drawn').toBeGreaterThan(0.005);
    expect(ink.right, 'right-aligned name').toBeGreaterThan(0.97);
    expect(ink.left, 'name does not start at the left edge').toBeGreaterThan(0.3);

    const languages = await ocrLanguages();
    if (languages?.includes(sample.ocr)) {
      const text = await ocr(image!, sample.ocr);
      test.info().annotations.push({ type: `ocr-${sample.code}`, description: text.replace(/\s+/g, ' ').trim() });
      // Missing glyphs render as empty boxes, which OCR cannot read as Arabic-script letters.
      expect((text.match(arabicScript) ?? []).length, 'Arabic-script letters read back from the PDF').toBeGreaterThan(
        sample.name.replace(/\s/g, '').length,
      );
    } else
      test.info().annotations.push({ type: 'skipped-check', description: `tesseract ${sample.ocr} is not installed` });
  });

test('switching an English catalogue to Persian turns its PDF text right-to-left', async ({ page, request, t }) => {
  const token = runToken();
  const product = await seedProduct(request, {
    variant: `${token}s`,
    locales: { en: { name: 'Linen Vest' }, fa: { name: samples[1].name, fabric: samples[1].fabric } },
    images: groupedImages([170, 150, 130]),
  });
  const { document } = await seedCatalogue(request, {
    title: `Switch fa ${token}`,
    slug: `switch-fa-${token}`,
    locale: 'en',
    outputs: { web: false, pdf: true },
    pdf: [{ productId: product.document.id }],
  });
  await openDesigner(page, document.id, 'pdf', t('PDF designer'));
  await page.locator('.page-rail ol > li').nth(1).locator('.page-name').click();
  await expect(page.locator('.canvas-sheet .design-element.is-text').filter({ hasText: 'Linen Vest' })).toHaveCSS(
    'direction',
    'ltr',
  );
  await choose(page, t('Catalogue language'), 'فارسی');
  const name = page.locator('.canvas-sheet .design-element.is-text').filter({ hasText: samples[1].name });
  await expect(name).toHaveCSS('direction', 'rtl');
  await expect(page.locator('.canvas-sheet')).toHaveAttribute('lang', 'fa');
  await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
});
