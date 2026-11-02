import { readFileSync } from 'node:fs';
import { writeFile } from 'node:fs/promises';
import { releases, runToken, seedProduct } from './support/catalogueApi';
import { download, expect, test, timed } from './support/fixtures';
import { inspectPdf, readDownload, renderPdf } from './support/pdf';
import { inkBounds, toJpeg } from './support/pixels';
import { seedCatalogue } from './support/seed';
import { designerStatus, openDesigner, prepareAndPublish } from './support/ui';

interface TemplatePage {
  title: string;
  width: number;
  height: number;
  productIds?: string[];
  elements: { src?: string }[];
}
const template = JSON.parse(
  readFileSync(new URL('../src/domain/design/templates/catalogue-en.json', import.meta.url), 'utf8'),
) as { pages: TemplatePage[] };
const productIds = [...new Set(template.pages.flatMap((page) => page.productIds ?? []))].sort();
const firstAsset = template.pages.flatMap((page) => page.elements).find((element) => element.src)!.src!;
/** Without `git lfs pull` the template images are small pointer files that no browser can draw. */
const lfsPointer = readFileSync(new URL(`../public${firstAsset}`, import.meta.url))
  .subarray(0, 40)
  .toString('utf8')
  .startsWith('version https://git-lfs');

test('the original design connects to products and exports all its pages', async ({
  page,
  request,
  context,
  diagnostics,
  t,
}) => {
  test.skip(!!process.env.CATALOGUE_E2E_URL, "seeds the original design's product IDs; runs on the suite's own store");
  test.skip(lfsPointer, 'the template images are Git LFS pointers; run git lfs pull first');
  test.setTimeout(300_000);
  const token = runToken();
  for (const [index, id] of productIds.entries()) {
    const [garment, variant] = id.split('-') as ['abaya' | 'baska' | 'korset' | 'kostyum' | 'nimcha', string];
    await seedProduct(request, {
      variant,
      garment,
      locales: { en: { name: `${garment[0]!.toUpperCase()}${garment.slice(1)} ${variant.slice(1)}`, fabric: 'Crepe' } },
      images: [{ color: [150 + (index % 6) * 15, 140, 130 + (index % 4) * 20], role: 'cutout', group: 'product' }],
    });
  }
  const { document } = await seedCatalogue(request, {
    title: `Original ${token}`,
    slug: `original-${token}`,
    locale: 'en',
    outputs: { web: false, pdf: true },
  });
  await openDesigner(page, document.id, 'pdf', t('PDF designer'));
  await page.getByRole('button', { name: t('Use original design') }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: t('Use original design') })
    .click();
  await expect(page.locator('.page-rail ol > li')).toHaveCount(template.pages.length);
  await expect(page.locator('.page-rail ol > li .page-name').first()).toContainText(template.pages[0]!.title);

  const file = await timed(diagnostics, `Whole PDF, original design, ${template.pages.length} pages`, () =>
    download(
      page,
      () => page.getByRole('button', { name: t('Download PDF'), exact: true }).click(),
      t('Continue export'),
      240_000,
    ),
  );
  const bytes = await readDownload(await file.path());
  const pdf = await inspectPdf(bytes);
  expect(pdf.pageCount).toBe(template.pages.length);
  expect(pdf.pages.map(({ widthMm, heightMm }) => [widthMm, heightMm])).toEqual(
    template.pages.map((item) => [Math.round(item.width * 10) / 10, Math.round(item.height * 10) / 10]),
  );
  test.info().annotations.push({ type: 'pdf-size', description: `${(bytes.length / 1024 / 1024).toFixed(1)} MiB` });

  // The bundled artwork and garment cut-outs are drawn: no page renders as an empty sheet.
  const rendered = await renderPdf(bytes, 0.6);
  for (const [index, image] of rendered.entries()) {
    const ink = await inkBounds(context, image, { x: 0, y: 0, width: 1, height: 1 }, 200);
    expect(ink.coverage, `page ${index + 1} has drawn content`).toBeGreaterThan(0.01);
  }
  for (const index of [0, 1])
    await writeFile(
      test.info().outputPath(`original-page-${index + 1}.jpg`),
      await toJpeg(context, rendered[index]!, 700),
    );

  // A release of the same pages uploads the whole PDF plus a PDF and a PNG per page within the API's limits.
  await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
  await expect(designerStatus(page)).toContainText(`${t('Saved')} · ${t('Revision')} 2`);
  await timed(diagnostics, `Prepare and publish, original design, ${template.pages.length} pages`, () =>
    prepareAndPublish(page, t),
  );
  expect((await releases(request, document.id))[0]?.publishedAtUtc).toBeTruthy();
});
