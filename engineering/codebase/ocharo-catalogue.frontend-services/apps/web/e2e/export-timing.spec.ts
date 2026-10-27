import { writeFile } from 'node:fs/promises';
import type { ProductDetail } from '../src/domain/catalogue/CatalogueModels';
import { runToken, seedProduct } from './support/catalogueApi';
import { download, expect, test, timed } from './support/fixtures';
import { inspectPdf, readDownload } from './support/pdf';
import { seedCatalogue } from './support/seed';
import { designerStatus, openDesigner } from './support/ui';

/**
 * Photo-sized media: twelve 3000 × 4000 images, the size of a phone photograph, across six collage pages. The
 * synthetic pixels compress well, so upload is quick, but the browser decodes and draws every full-size image.
 */
test('exports and prepares a photo-heavy catalogue within budget', async ({ page, request, diagnostics, t }) => {
  test.setTimeout(420_000);
  const token = runToken();
  const products: ProductDetail[] = [];
  for (const [index, base] of (
    [
      [190, 150, 130],
      [130, 160, 140],
    ] as const
  ).entries())
    products.push(
      await seedProduct(request, {
        variant: `${token}l${index}`,
        locales: { en: { name: `Large ${index + 1} ${token}` } },
        images: Array.from({ length: 6 }, (_, image) => ({
          color: [base[0] - image * 12, base[1] - image * 8, base[2] - image * 6] as const,
          width: 3000,
          height: 4000,
        })),
      }),
    );
  const { document } = await seedCatalogue(request, {
    title: `Large ${token}`,
    slug: `large-${token}`,
    locale: 'en',
    outputs: { web: false, pdf: true },
    pdf: Array.from({ length: 6 }, (_, index) => ({
      productId: products[index % 2]!.document.id,
      layout: 'collage' as const,
    })),
  });
  await openDesigner(page, document.id, 'pdf', t('PDF designer'));
  await expect(page.locator('.page-rail ol > li')).toHaveCount(7);

  const file = await timed(diagnostics, 'Whole PDF, 7 pages, 12 photo-sized images', () =>
    download(
      page,
      () => page.getByRole('button', { name: t('Download PDF'), exact: true }).click(),
      t('Continue export'),
      300_000,
    ),
  );
  const bytes = await readDownload(await file.path());
  const pdf = await inspectPdf(bytes);
  expect(pdf.pageCount).toBe(7);
  test.info().annotations.push({ type: 'pdf-size', description: `${(bytes.length / 1024 / 1024).toFixed(1)} MiB` });
  await writeFile(test.info().outputPath('size.txt'), `${bytes.length}\n`);

  await page.getByText(t('Publication and saved history')).click();
  await expect(designerStatus(page)).toContainText(t('Saved'));
  await timed(diagnostics, 'Prepare release, 7 pages', async () => {
    await page.getByRole('button', { name: t('Prepare release') }).click();
    const prepared = page.getByRole('status').filter({ hasText: t('Prepared for review. Publish when ready.') });
    const warning = page.getByRole('alertdialog').getByRole('button', { name: t('Continue export') });
    await expect(prepared.or(warning)).toBeVisible({ timeout: 300_000 });
    if (await warning.isVisible()) await warning.click();
    await expect(prepared).toBeVisible({ timeout: 300_000 });
  });

  const [exportMs, prepareMs] = diagnostics.timings.map((timing) => timing.ms);
  expect(exportMs, 'whole-PDF export time').toBeLessThan(60_000);
  expect(prepareMs, 'prepare time').toBeLessThan(120_000);
});
