import { getDesign, runToken } from './support/catalogueApi';
import { expect, test } from './support/fixtures';
import { garmentName, seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';
import { designerStatus, openDesigner } from './support/ui';

test.describe('designer safety', () => {
  test('duplicates and removes a web page; undo restores it', async ({ page, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `Pages ${token}`,
      slug: `pages-${token}`,
      locale: 'en',
      outputs: { web: true, pdf: false },
      web: [{ title: 'Looks', productIds: [products.a!.document.id] }],
    });
    await openDesigner(page, document.id, 'web', t('Web designer'));
    const pages = page.locator('.page-panel ol.pages > li .page-name');
    await page.getByRole('button', { name: t('Duplicate page') }).click();
    await expect(pages).toHaveText([/^1 · Looks/, new RegExp(`^2 · Looks · ${t('Copy')}`)]);
    await page.getByRole('button', { name: t('Remove page') }).click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: t('Remove page') })
      .click();
    await expect(pages).toHaveCount(1);
    await page.getByRole('button', { name: t('Undo'), exact: true }).click();
    await expect(pages).toHaveCount(2);
  });

  test('restores a saved revision as an unsaved draft', async ({ page, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `History ${token}`,
      slug: `history-${token}`,
      locale: 'en',
      outputs: { web: true, pdf: false },
      web: [{ title: 'Original title', productIds: [products.a!.document.id] }],
    });
    await openDesigner(page, document.id, 'web', t('Web designer'));
    const field = page.getByRole('textbox', { name: t('Page title') });
    await field.fill('Changed title');
    await field.press('Tab');
    await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
    await expect(designerStatus(page)).toContainText(`${t('Revision')} 2`);

    await page.getByText(t('Publication and saved history')).click();
    await page.getByRole('button', { name: t('Load saved history') }).click();
    const revision1 = page.locator('.publication ol li').filter({ hasText: `${t('Revision')} 1 ·` });
    await revision1.getByRole('button', { name: t('Restore revision') }).click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: t('Restore revision') })
      .click();
    await expect(
      page.getByRole('status').filter({ hasText: t('Historical revision loaded. Save to create a new revision.') }),
    ).toBeVisible();
    await expect(page.locator('main.web-catalogue section.web-page h2')).toHaveText('Original title');
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
    await page.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
    await expect(designerStatus(page)).toContainText(`${t('Revision')} 3`);
    expect((await getDesign(request, document.id)).snapshot.catalogue?.web.pages[0]?.title).toBe('Original title');
  });

  test('offers the recovery copy after a reload with unsaved changes', async ({ page, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `Recovery ${token}`,
      slug: `recovery-${token}`,
      locale: 'en',
      outputs: { web: true, pdf: false },
      web: [{ title: 'Before', productIds: [products.a!.document.id] }],
    });
    await openDesigner(page, document.id, 'web', t('Web designer'));
    await page.getByRole('checkbox', { name: garmentName(1, token), exact: true }).check();
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
    page.once('dialog', (dialog) => void dialog.accept());
    await page.reload();
    await expect(page.getByText(t('Unsaved changes from this browser tab are available.'))).toBeVisible();
    await page.getByRole('button', { name: t('Restore recovery copy') }).click();
    await expect(page.locator('.product-order li span')).toHaveText([products.a!.document.id, products.b!.document.id]);
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
  });
});

test.describe('designer conflicts', () => {
  // The refused save is a 409 response, which Chromium also logs to the console.
  test.use({ allowedConsole: [/status of 409/] });

  test('a save from a stale tab is refused and keeps the edits', async ({ page, context, request, t }) => {
    const token = runToken();
    const products = await seedGarments(request, token);
    const { document } = await seedCatalogue(request, {
      title: `Conflict ${token}`,
      slug: `conflict-${token}`,
      locale: 'en',
      outputs: { web: true, pdf: false },
      web: [{ title: 'Shared', productIds: [products.a!.document.id] }],
    });
    const other = await context.newPage();
    await openDesigner(page, document.id, 'web', t('Web designer'));
    await openDesigner(other, document.id, 'web', t('Web designer'));

    const rename = async (tab: typeof page, value: string) => {
      const field = tab.getByRole('textbox', { name: t('Page title') });
      await field.fill(value);
      await field.press('Tab');
      await tab.getByRole('button', { name: t('Save catalogue'), exact: true }).click();
    };
    await rename(other, 'Saved in the other tab');
    await expect(designerStatus(other)).toContainText(`${t('Revision')} 2`);
    await rename(page, 'Stale edit');
    await expect(page.getByText(t('This item changed elsewhere. Reload before saving.')).first()).toBeVisible();
    await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
    await expect(page.getByRole('textbox', { name: t('Page title') })).toHaveValue('Stale edit');
    expect((await getDesign(request, document.id)).snapshot.catalogue?.web.pages[0]?.title).toBe(
      'Saved in the other tab',
    );
  });
});
