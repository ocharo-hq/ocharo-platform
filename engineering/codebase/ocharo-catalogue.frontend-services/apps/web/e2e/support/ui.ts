import { expect, type Locator, type Page } from '@playwright/test';

/** Picks an option in the SDK select picker that `CatalogueSelect` renders. */
export async function choose(scope: Page | Locator, label: string, option: string): Promise<void> {
  const page = 'page' in scope ? scope.page() : scope;
  await scope.getByRole('combobox', { name: label, exact: true }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
  await expect(scope.getByRole('combobox', { name: label, exact: true })).toContainText(option);
}

/** A gallery card, found by its exact catalogue title. */
export function galleryCard(page: Page, title: string): Locator {
  return page
    .locator('.catalogue-grid article')
    .filter({ has: page.getByRole('heading', { level: 2, name: title, exact: true }) });
}

/** Waits for a designer to finish loading its saved catalogue and product library. */
export async function openDesigner(page: Page, id: string, mode: 'web' | 'pdf', heading: string): Promise<void> {
  await page.goto(`/catalogues/${id}/${mode}`);
  await expect(page.getByRole('heading', { level: 1, name: heading, exact: true })).toBeVisible();
  await expect(page.locator('.designer-bar')).toBeVisible();
  await expect(page.locator('.designer-bar .status')).not.toContainText('undefined');
}

/** The designer's saved-state line, such as "Saved · Revision 3". */
export const designerStatus = (page: Page) => page.locator('.designer-bar .status');

/** Prepares the saved revision (accepting an export-check warning) and publishes it from the designer bar. */
export async function prepareAndPublish(page: Page, t: (source: string) => string): Promise<void> {
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
