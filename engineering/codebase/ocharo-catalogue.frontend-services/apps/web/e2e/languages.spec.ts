import type { Page } from '@playwright/test';
import { catalogueLanguages } from '../src/domain/catalogue/CatalogueLanguages';
import { uiMessages, translateUi, type UiLocale } from '../src/domain/shared/UiMessages';
import { getDesign, runToken } from './support/catalogueApi';
import { expect, test } from './support/fixtures';
import { garmentName, seedGarments } from './support/products';
import { seedCatalogue } from './support/seed';
import { choose, designerStatus, openDesigner } from './support/ui';

/**
 * English interface phrases still shown when another interface language is selected. Authored catalogue content
 * (the web preview, the PDF canvas and form values) is excluded: it follows the catalogue language instead.
 */
async function untranslated(page: Page, locale: UiLocale): Promise<string[]> {
  // Language names are endonyms in every interface language, so "English" is not an untranslated phrase.
  const endonyms = new Set<string>(catalogueLanguages.map((language) => language.label));
  const english = Object.keys(uiMessages).filter(
    (key) => translateUi(key, locale) !== key && /[a-z]{3}/i.test(key) && !endonyms.has(key),
  );
  return page.evaluate((keys) => {
    const known = new Set(keys);
    const authored = '.web-catalogue, .canvas-sheet, .image-picker, .products, .product-order, .pages, .page-rail ol';
    const found = new Set<string>();
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const parent = node.parentElement;
      if (!parent || parent.closest(authored) || parent.closest('script, style, [hidden]')) continue;
      const value = (node.textContent ?? '').replace(/^[+\s←·]+|\s+$/g, '');
      if (known.has(value) && parent.checkVisibility()) found.add(value);
    }
    for (const element of document.querySelectorAll('[aria-label], [placeholder], [title]')) {
      if (element.closest(authored)) continue;
      for (const name of ['aria-label', 'placeholder', 'title']) {
        const value = element.getAttribute(name);
        if (value && known.has(value)) found.add(`${name}: ${value}`);
      }
    }
    return [...found].sort();
  }, english);
}

for (const uiLocale of ['uz', 'ru', 'en'] as const)
  test.describe(`${uiLocale} interface with an English catalogue`, () => {
    test.use({ uiLocale });

    test('gallery and both designers use the interface language; content stays English', async ({
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
      await expect(page.locator('html')).toHaveAttribute('lang', uiLocale);
      await expect(page.getByRole('button', { name: t('New catalogue'), exact: true })).toBeVisible();
      await expect(page.getByText(t('Your catalogues'), { exact: true }).first()).toBeVisible();
      const gallery = await untranslated(page, uiLocale);

      await openDesigner(page, document.id, 'web', t('Web designer'));
      await expect(page.getByRole('combobox', { name: t('Catalogue language') })).toContainText('English');
      await expect(page.locator('main.web-catalogue')).toHaveAttribute('lang', 'en');
      await expect(page.locator('main.web-catalogue h3').first()).toHaveText(garmentName(0, token));
      await expect(page.getByRole('heading', { name: t('Draft preview') })).toBeVisible();
      await page.getByText(t('Catalogue copy and price display')).click();
      await page.getByText(t('Publication and saved history')).click();
      const web = await untranslated(page, uiLocale);

      await openDesigner(page, document.id, 'pdf', t('PDF designer'));
      await expect(page.getByRole('button', { name: t('Download PDF'), exact: true })).toBeVisible();
      await page.locator('.canvas-sheet .design-element.is-text').first().click();
      await expect(page.getByRole('heading', { name: t('Selected element') })).toBeVisible();
      await expect(page.locator('.canvas-sheet')).toHaveAttribute('lang', 'en');
      const pdf = await untranslated(page, uiLocale);

      expect({ gallery, web, pdf }, 'English interface text in a translated interface').toEqual({
        gallery: [],
        web: [],
        pdf: [],
      });
      await page.screenshot({ path: test.info().outputPath(`pdf-designer-${uiLocale}.png`) });
    });
  });

test('switching the interface language leaves the catalogue language and draft unchanged', async ({
  page,
  request,
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
  const ru = (source: string) => translateUi(source, 'ru');

  await page.goto('/catalogues');
  await page.getByRole('button', { name: translateUi('Account menu', 'en') }).click();
  await page
    .getByRole('group', { name: translateUi('Interface language', 'en') })
    .getByRole('menuitemradio', { name: 'Русский' })
    .click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'ru');
  await page.keyboard.press('Escape');
  await openDesigner(page, document.id, 'web', ru('Web designer'));
  await expect(page.getByRole('combobox', { name: ru('Catalogue language') })).toContainText('English');
  await expect(designerStatus(page)).toContainText(ru('Saved'));
  await expect(page.locator('main.web-catalogue h3')).toHaveText(garmentName(0, token));

  // Changing the catalogue language rewrites only the draft content, never the interface.
  await choose(page, ru('Catalogue language'), 'O‘zbekcha');
  await expect(page.locator('main.web-catalogue h3')).toHaveText(`${garmentName(0, token)} UZ`);
  await expect(page.locator('html')).toHaveAttribute('lang', 'ru');
  await expect(page.getByRole('heading', { level: 1, name: ru('Web designer') })).toBeVisible();
  await expect(designerStatus(page)).toContainText(ru('Unsaved changes'));
  expect((await getDesign(request, document.id)).snapshot.locale).toBe('en');
  expect(await page.evaluate(() => localStorage.getItem('ocharo.operator.locale'))).toBe('ru');
});
