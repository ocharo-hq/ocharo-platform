import { getDesign, runToken } from './support/catalogueApi';
import { expect, test } from './support/fixtures';
import { garmentName, seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';
import { choose, designerStatus, openDesigner } from './support/ui';

/**
 * The interface is English only until the SDKs' translation layer is improved (developer, 2026-11-20), so there is no
 * interface language to switch. The catalogue's own content language stays a separate, authored choice.
 */
test('gallery and both designers show the English interface, with no language choice in the account menu', async ({
  page,
  request,
  t,
}) => {
  const token = runToken();
  const products = await seedGarments(request, token);
  const { document } = await seedCatalogue(request, {
    title: `Interface ${token}`,
    slug: `interface-${token}`,
    locale: 'en',
    outputs: { web: true, pdf: true },
    web: [{ title: 'Looks', productIds: [products.a!.document.id, products.b!.document.id] }],
    pdf: [{ productId: products.a!.document.id }],
  });

  await page.goto('/catalogues');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('button', { name: t('New catalogue'), exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: t('Catalogues'), exact: true })).toBeAttached();
  await page.getByRole('button', { name: t('Account menu') }).click();
  await expect(page.getByRole('group', { name: 'Interface language' })).toHaveCount(0);
  await page.keyboard.press('Escape');

  await openDesigner(page, document.id, 'web', t('Web designer'));
  await expect(page.getByRole('combobox', { name: t('Catalogue language') })).toContainText('English');
  await expect(page.locator('main.web-catalogue')).toHaveAttribute('lang', 'en');
  await expect(page.locator('main.web-catalogue h3').first()).toHaveText(garmentName(0, token));
  await expect(page.getByRole('heading', { name: t('Draft preview') })).toBeVisible();

  await openDesigner(page, document.id, 'pdf', t('PDF designer'));
  await expect(page.getByRole('button', { name: t('Download PDF'), exact: true })).toBeVisible();
  await page.locator('.canvas-sheet .design-element.is-text').first().click();
  await expect(page.getByRole('heading', { name: t('Selected element') })).toBeVisible();
  await expect(page.locator('.canvas-sheet')).toHaveAttribute('lang', 'en');
  await page.screenshot({ path: test.info().outputPath('pdf-designer-en.png') });
});

test('switching the catalogue language leaves the English interface and the draft language unchanged', async ({
  page,
  request,
  t,
}) => {
  const token = runToken();
  const products = await seedGarments(request, token, (garment, name) => ({
    en: { name, fabric: garment.fabric },
    uz: { name: `${name} UZ`, fabric: garment.fabric },
  }));
  const { document } = await seedCatalogue(request, {
    title: `Switch ${token}`,
    slug: `switch-${token}`,
    locale: 'en',
    outputs: { web: true, pdf: false },
    web: [{ title: 'Looks', productIds: [products.a!.document.id] }],
  });

  await page.goto('/catalogues');
  await openDesigner(page, document.id, 'web', t('Web designer'));
  await expect(page.getByRole('combobox', { name: t('Catalogue language') })).toContainText('English');
  await expect(designerStatus(page)).toContainText(t('Saved'));
  await expect(page.locator('main.web-catalogue h3')).toHaveText(garmentName(0, token));

  // Changing the catalogue language rewrites only the draft content, never the interface.
  await choose(page, t('Catalogue language'), 'O‘zbekcha');
  await expect(page.locator('main.web-catalogue h3')).toHaveText(`${garmentName(0, token)} UZ`);
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('heading', { level: 1, name: t('Web designer') })).toBeAttached();
  await expect(designerStatus(page)).toContainText(t('Unsaved changes'));
  expect((await getDesign(request, document.id)).snapshot.locale).toBe('en');
});
