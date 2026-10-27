import { writeFile } from 'node:fs/promises';
import type { Page } from '@playwright/test';
import { editProductCopy, getDesign, runToken } from './support/catalogueApi';
import { download, expect, test, timed } from './support/fixtures';
import { pngSize } from './support/images';
import { inspectPdf, ocr, ocrLanguages, readDownload, renderPdf } from './support/pdf';
import { imageDifference } from './support/pixels';
import { garmentName, garments, seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';
import { choose, designerStatus, openDesigner } from './support/ui';

const rail = (page: Page) => page.locator('.page-rail ol > li');
async function selectPdfPage(page: Page, number: number) {
  await rail(page)
    .nth(number - 1)
    .locator('.page-name')
    .click();
  await expect(rail(page).nth(number - 1)).toHaveClass(/selected/);
}
/** Image sources on every PDF page, read page by page from the editing canvas. */
async function canvasImages(page: Page): Promise<string[][]> {
  const result: string[][] = [];
  for (let index = 1; index <= (await rail(page).count()); index++) {
    await selectPdfPage(page, index);
    result.push(
      await page
        .locator('.canvas-sheet .design-element img')
        .evaluateAll((items) => items.map((item) => item.getAttribute('src') ?? '')),
    );
  }
  return result;
}

test.describe('PDF designer', () => {
  test('keeps exact page sizes and order in whole-PDF and current-page PDF/PNG exports', async ({
    page,
    request,
    context,
    diagnostics,
    t,
  }) => {
    const token = runToken(),
      title = `PDF catalogue ${token}`;
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title,
      slug: `pdf-${token}`,
      locale: 'en',
      outputs: { web: false, pdf: true },
      pdf: [
        { productId: products.a!.document.id, layout: 'product' },
        { productId: products.b!.document.id, layout: 'collage' },
      ],
    });
    await openDesigner(page, document.id, 'pdf', t('PDF designer'));
    await expect(rail(page)).toHaveCount(3);

    // Add a continuation page for the third garment, make it A5 and move it before the collage.
    await choose(page.locator('.page-rail'), t('Choose a product'), garmentName(2, token));
    await choose(page.locator('.page-rail'), t('Page template'), t('Product continuation'));
    await page.getByRole('button', { name: `+ ${t('Add product page')}` }).click();
    await expect(rail(page)).toHaveCount(4);
    for (const [label, value] of [
      ['Width', '148'],
      ['Height', '210'],
    ] as const) {
      const field = page.locator('.page-rail').getByRole('spinbutton', { name: t(label), exact: true });
      await field.fill(value);
      await field.press('Tab');
    }
    await expect(page.locator('.page-rail .hint').filter({ hasText: t('Page dimensions') })).toContainText(
      '148.0 × 210.0 mm',
    );
    await rail(page)
      .nth(3)
      .getByRole('button', { name: t('Move page up') })
      .click();
    await expect(rail(page).locator('.page-name')).toHaveText([
      new RegExp(`^1 · ${title}`),
      new RegExp(`^2 · ${garmentName(0, token)}`),
      new RegExp(`^3 · ${garmentName(2, token)}`),
      new RegExp(`^4 · ${garmentName(1, token)}`),
    ]);
    await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
    await expect(designerStatus(page)).toContainText(`${t('Saved')} · ${t('Revision')} 2`);
    const saved = (await getDesign(request, document.id)).snapshot;
    expect(saved.pages.map(({ width, height }) => [width, height])).toEqual([
      [210, 297],
      [210, 297],
      [148, 210],
      [210, 297],
    ]);

    // Whole catalogue.
    const whole = await timed(diagnostics, 'Whole PDF, 4 pages', () =>
      download(
        page,
        () => page.getByRole('button', { name: t('Download PDF'), exact: true }).click(),
        t('Continue export'),
      ),
    );
    await expect(page.getByRole('status').filter({ hasText: t('Download ready.') })).toBeVisible();
    // The A5 page keeps its A4 composition, so the page check warns that elements will be cropped.
    expect(whole.warned, 'export check warned about the cropped A5 page').toBe(true);
    const wholeBytes = await readDownload(await whole.path());
    await writeFile(test.info().outputPath('whole-catalogue.pdf'), wholeBytes);
    expect(whole.suggestedFilename()).toBe(`PDF-catalogue-${token}.pdf`);
    const wholePdf = await inspectPdf(wholeBytes);
    expect(wholePdf.pageCount).toBe(4);
    expect(wholePdf.pages.map(({ widthMm, heightMm }) => [widthMm, heightMm])).toEqual([
      [210, 297],
      [210, 297],
      [148, 210],
      [210, 297],
    ]);
    expect(wholePdf.title).toBe(title);
    expect(wholePdf.creator).toBe('Ocharo Catalogue');
    expect(
      wholePdf.pages.every((item) => item.images === 1),
      'each page is one rendered image',
    ).toBe(true);
    test.info().annotations.push({
      type: 'pdf-text-layer',
      description: `text extracted by pdf.js: ${JSON.stringify(wholePdf.text)}`,
    });
    const rendered = await renderPdf(wholeBytes, 1);
    for (const [index, image] of rendered.entries())
      await writeFile(test.info().outputPath(`whole-page-${index + 1}.png`), image);

    if ((await ocrLanguages())?.includes('eng')) {
      const text = await ocr((await renderPdf(wholeBytes, 3))[1]!, 'eng');
      expect(text).toContain(garments[0]!.name);
      expect(text.replace(/\s/g, '')).toContain('420,000');
    } else
      test
        .info()
        .annotations.push({ type: 'skipped-check', description: 'tesseract is not installed; PDF text was not read' });

    // Current page as PDF and PNG.
    await selectPdfPage(page, 3);
    const single = await timed(diagnostics, 'Page PDF, A5', () =>
      download(page, () => page.getByRole('button', { name: t('Download page PDF') }).click(), t('Continue export')),
    );
    expect(single.suggestedFilename()).toBe(`PDF-catalogue-${token}-pages-3.pdf`);
    const singleBytes = await readDownload(await single.path());
    const singlePdf = await inspectPdf(singleBytes);
    expect(singlePdf.pageCount).toBe(1);
    expect(singlePdf.pages[0]).toMatchObject({ widthMm: 148, heightMm: 210 });

    const png = await timed(diagnostics, 'Page PNG, A5', () =>
      download(page, () => page.getByRole('button', { name: t('Download page PNG') }).click(), t('Continue export')),
    );
    expect(png.suggestedFilename()).toBe(`PDF-catalogue-${token}-${garmentName(2, token).replace(/\s+/g, '-')}.png`);
    const pngBytes = await readDownload(await png.path());
    expect(pngSize(pngBytes)).toEqual({
      width: Math.round((148 * 300) / 25.4),
      height: Math.round((210 * 300) / 25.4),
    });
    await writeFile(test.info().outputPath('page-3.png'), pngBytes);

    // The PDF page, the PNG and the designer canvas show the same composition.
    const [singleRender] = await renderPdf(singleBytes, 2);
    await choose(page, t('Zoom'), '75%');
    const canvas = await page.locator('.canvas-sheet .design-canvas').screenshot();
    await writeFile(test.info().outputPath('designer-page-3.png'), canvas);
    const pdfToPng = await imageDifference(context, singleRender!, pngBytes);
    const canvasToPng = await imageDifference(context, canvas, pngBytes);
    test.info().annotations.push({
      type: 'image-difference',
      description: `pdf↔png ${pdfToPng.toFixed(1)}, canvas↔png ${canvasToPng.toFixed(1)}`,
    });
    expect(pdfToPng).toBeLessThan(4);
    expect(canvasToPng).toBeLessThan(12);
    expect(await imageDifference(context, rendered[1]!, pngBytes), 'a different page differs').toBeGreaterThan(
      pdfToPng,
    );
  });

  test('undo restores every media choice after randomizing catalogue images', async ({ page, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `Randomize ${token}`,
      slug: `randomize-${token}`,
      locale: 'en',
      outputs: { web: false, pdf: true },
      pdf: [
        { productId: products.a!.document.id, layout: 'product' },
        { productId: products.b!.document.id, layout: 'collage' },
      ],
    });
    await openDesigner(page, document.id, 'pdf', t('PDF designer'));
    const original = await canvasImages(page);
    expect(original.flat().length).toBeGreaterThan(5);

    let clicks = 0,
      randomized = original;
    while (clicks < 6 && JSON.stringify(randomized) === JSON.stringify(original)) {
      await page.getByRole('button', { name: t('Randomize catalogue images') }).click();
      clicks++;
      randomized = await canvasImages(page);
    }
    expect(randomized, 'randomizing changed at least one image').not.toEqual(original);
    // Every chosen image still belongs to its page's product.
    for (const [index, sources] of randomized.entries()) {
      const product = index === 1 ? products.a! : index === 2 ? products.b! : null;
      if (product)
        for (const source of sources)
          expect(product.document.media.map((media) => `/api/catalogue/media/${media.sha256}`)).toContain(source);
    }
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));

    for (let undo = 0; undo < clicks; undo++) await page.getByRole('button', { name: t('Undo'), exact: true }).click();
    expect(await canvasImages(page)).toEqual(original);
    await expect(designerStatus(page)).toContainText(t('Saved'));
    await expect(page.getByRole('button', { name: t('Undo'), exact: true })).toBeDisabled();
    for (let redo = 0; redo < clicks; redo++) await page.getByRole('button', { name: t('Redo'), exact: true }).click();
    expect(await canvasImages(page)).toEqual(randomized);

    // A page-level randomization leaves the other pages untouched.
    await page.getByRole('button', { name: t('Undo'), exact: true }).click();
    const before = await canvasImages(page);
    await selectPdfPage(page, 3);
    for (let attempt = 0; attempt < 6; attempt++) {
      await page.getByRole('button', { name: t('Randomize page images') }).click();
      if (JSON.stringify((await canvasImages(page))[2]) !== JSON.stringify(before[2])) break;
      await selectPdfPage(page, 3);
    }
    const after = await canvasImages(page);
    expect(after[1]).toEqual(before[1]);
  });

  test('refreshing product fields updates bound text and keeps the page layout', async ({ page, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `Refresh ${token}`,
      slug: `refresh-${token}`,
      locale: 'en',
      outputs: { web: false, pdf: true },
      pdf: [{ productId: products.a!.document.id }],
    });
    await openDesigner(page, document.id, 'pdf', t('PDF designer'));
    await selectPdfPage(page, 2);
    const frames = () =>
      page
        .locator('.canvas-sheet .design-element')
        .evaluateAll((items) =>
          items.map(
            (item) =>
              (item as HTMLElement)
                .getAttribute('style')
                ?.match(/left[^;]+;\s*top[^;]+;\s*width[^;]+;\s*height[^;]+/)?.[0],
          ),
        );
    const before = await frames();
    expect(before.length > 5 && before.every(Boolean), 'element frames were read').toBe(true);
    const name = page.locator('.canvas-sheet .design-element.is-text').filter({ hasText: garmentName(0, token) });
    await expect(name).toHaveText(garmentName(0, token));

    await editProductCopy(request, products.a!.document.id, 'en', { name: `Renewed ${token}` });
    await page.getByRole('button', { name: t('Refresh product fields') }).click();
    await expect(
      page.getByRole('status').filter({ hasText: t('Product fields refreshed. Save to keep this version.') }),
    ).toBeVisible();
    await expect(
      page.locator('.canvas-sheet .design-element.is-text').filter({ hasText: `Renewed ${token}` }),
    ).toHaveCount(1);
    expect(await frames(), 'every element keeps its frame').toEqual(before);
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
  });
});
