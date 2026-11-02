import type { Page } from '@playwright/test';
import { getDesign, runToken } from './support/catalogueApi';
import { expect, test } from './support/fixtures';
import { garmentName, groupImage, seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';
import { choose, designerStatus, openDesigner } from './support/ui';
import { readWebModel, type WebSection } from './support/webModel';

const preview = (page: Page) => page.locator('main.web-catalogue');
const pageList = (page: Page) => page.locator('.page-panel ol.pages > li');

async function selectWebPage(page: Page, title: string) {
  await pageList(page).filter({ hasText: title }).locator('.page-name').click();
  await expect(preview(page).locator('section.web-page h2')).toHaveText(title);
}

test.describe('web designer', () => {
  test('orders pages, sets theme, copy, image group and prices; preview matches the released reader', async ({
    page,
    request,
    t,
  }) => {
    const token = runToken(),
      slug = `web-${token}`;
    const products = await seedGarments(request, token);
    const { a, b, c } = products as Record<'a' | 'b' | 'c', (typeof products)[string]>;
    const { document } = await seedCatalogue(request, {
      title: `Web catalogue ${token}`,
      slug,
      locale: 'en',
      outputs: { web: true, pdf: false },
      description: 'Autumn pieces for the web check.',
    });
    await openDesigner(page, document.id, 'web', t('Web designer'));
    await expect(designerStatus(page)).toContainText(`${t('Saved')} · ${t('Revision')} 1`);

    // Three pages; titles commit on change, as an operator leaving the field would.
    const addPage = async (title: string, productNames: string[]) => {
      await page.getByRole('button', { name: `+ ${t('Add page')}` }).click();
      const field = page.getByRole('textbox', { name: t('Page title') });
      await field.fill(title);
      await field.press('Tab');
      for (const name of productNames) await page.getByRole('checkbox', { name, exact: true }).check();
      await expect(pageList(page).filter({ hasText: title })).toContainText(
        t('{count} products', { count: productNames.length }),
      );
    };
    await addPage('Opening looks', [garmentName(0, token), garmentName(1, token)]);
    await addPage('The grid', [garmentName(2, token), garmentName(0, token)]);
    await page
      .getByRole('group', { name: t('Page template') })
      .getByRole('button', { name: t('Product grid') })
      .click();
    await addPage('Draft page', [garmentName(1, token)]);
    await page.getByRole('checkbox', { name: t('Active page') }).uncheck();
    await expect(preview(page).getByText(t('Inactive page'))).toBeVisible();

    await pageList(page)
      .filter({ hasText: 'The grid' })
      .getByRole('button', { name: t('Move page up') })
      .click();
    await expect(pageList(page).locator('.page-name')).toHaveText([
      /^1 · The grid/,
      /^2 · Opening looks/,
      /^3 · Draft page/,
    ]);

    await selectWebPage(page, 'Opening looks');
    await page
      .locator('.product-order li')
      .filter({ hasText: b.document.id })
      .getByRole('button', { name: t('Move product up') })
      .click();
    await expect(page.locator('.product-order li span')).toHaveText([b.document.id, a.document.id]);

    // Theme, with undo and redo of that one change.
    await choose(page, t('Web theme'), t('Grid'));
    await expect(preview(page)).toHaveClass(/theme-grid/);
    await page.getByRole('button', { name: t('Undo'), exact: true }).click();
    await expect(preview(page)).toHaveClass(/theme-editorial/);
    await page.getByRole('button', { name: t('Redo'), exact: true }).click();
    await expect(preview(page)).toHaveClass(/theme-grid/);

    await page.getByText(t('Catalogue copy and price display')).click();
    const copy = {
      header: 'Ocharo atelier',
      collection: 'Autumn 2026',
      coverNote: 'Twelve pieces, cut by hand.',
      footer: 'Made in Tashkent',
    };
    for (const [label, value] of [
      ['Header', copy.header],
      ['Collection title', copy.collection],
      ['Cover note', copy.coverNote],
      ['Footer', copy.footer],
    ] as const) {
      const field = page.getByRole('textbox', { name: t(label), exact: true });
      await field.fill(value);
      await field.press('Tab');
    }
    await choose(page, t('Image group for all products'), t('Detail images'));
    await page.getByRole('checkbox', { name: t('Show wholesale price') }).uncheck();
    await expect(page.getByRole('checkbox', { name: t('Show wholesale price') })).not.toBeChecked();

    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
    await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
    await expect(designerStatus(page)).toContainText(`${t('Saved')} · ${t('Revision')} 2`);

    const saved = (await getDesign(request, document.id)).snapshot;
    expect(saved.catalogue?.web.theme).toBe('grid');
    expect(
      saved.catalogue?.web.pages.map(({ title, layout, isActive, productIds }) => ({
        title,
        layout,
        isActive,
        productIds,
      })),
    ).toEqual([
      { title: 'The grid', layout: 'grid', isActive: true, productIds: [c.document.id, a.document.id] },
      { title: 'Opening looks', layout: 'spread', isActive: true, productIds: [b.document.id, a.document.id] },
      { title: 'Draft page', layout: 'spread', isActive: false, productIds: [b.document.id] },
    ]);
    expect(saved.catalogueSettings?.copyByLocale.en).toMatchObject(copy);
    expect(saved.catalogueSettings?.mediaGroup).toBe('detail');
    expect(saved.catalogueSettings?.priceDisplay).toEqual({
      show: true,
      showCurrency: true,
      showQuantity: true,
      showRetail: true,
      showWholesale: false,
    });

    // The draft preview of each active page.
    const previewSections: WebSection[] = [];
    for (const title of ['The grid', 'Opening looks']) {
      await selectWebPage(page, title);
      await expect(preview(page).locator('.photographs img').first()).toBeVisible();
      previewSections.push(...(await readWebModel(preview(page), 'preview')).sections);
    }
    const previewModel = await readWebModel(preview(page), 'preview');
    expect(previewSections[1]!.products.map((product) => product.name)).toEqual([
      garmentName(1, token),
      garmentName(0, token),
    ]);
    expect(previewSections[1]!.products[0]!.images).toEqual([groupImage(b, 'detail')]);
    expect(previewSections[1]!.products[0]!.prices).toEqual([`510,000 UZS · 1–9 ${'pieces'}`]);
    await page.locator('.preview-panel').scrollIntoViewIfNeeded();
    await page.screenshot({ path: test.info().outputPath('web-designer-desktop.png') });

    for (const width of ['768', '390']) {
      await choose(page, t('Preview size'), `${t(width === '768' ? 'Tablet' : 'Mobile')} · ${width}`);
      const box = await preview(page).boundingBox();
      expect(Math.round(box?.width ?? 0), `preview width at ${width}`).toBeLessThanOrEqual(Number(width));
      expect(
        await preview(page).evaluate((node) => node.scrollWidth - node.clientWidth),
        'preview overflow',
      ).toBeLessThanOrEqual(1);
    }
    await preview(page).screenshot({ path: test.info().outputPath('web-preview-390.png') });

    // Release the saved revision and read it as a buyer would.
    await page.getByText(t('Publication and saved history')).click();
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
    await page.getByRole('link', { name: t('Open public catalogue') }).click();
    await expect(page).toHaveURL(new RegExp(`/c/${slug}\\?locale=en$`));
    const reader = page.locator('article.web-reader');
    await expect(reader.locator('.photographs img').first()).toBeVisible();
    const released = await readWebModel(reader, 'reader');

    expect(released.theme).toBe(previewModel.theme);
    expect(released.coverNote).toBe(copy.coverNote);
    expect(released.footer).toBe(copy.footer);
    await expect(reader.locator('.collection-kicker')).toHaveText(`${copy.header} · ${copy.collection}`);
    expect(released.sections.map(({ title, layout }) => ({ title, layout }))).toEqual(
      previewSections.map(({ title, layout }) => ({ title, layout })),
    );
    for (const [index, section] of released.sections.entries())
      expect(section.products, `released section ${section.title}`).toEqual(previewSections[index]!.products);
    await page.screenshot({ path: test.info().outputPath('web-reader-desktop.png'), fullPage: true });
  });
});
