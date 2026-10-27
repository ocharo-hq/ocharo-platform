import { getDesign, listDesigns, revisions, runToken } from './support/catalogueApi';
import { expect, test } from './support/fixtures';
import { galleryCard, openDesigner } from './support/ui';

test.describe('catalogue gallery', () => {
  test('creates, renames, duplicates, activates and archives a catalogue; history remains', async ({
    page,
    request,
    t,
  }) => {
    const token = runToken(),
      title = `Gallery ${token}`,
      renamed = `Gallery renamed ${token}`;
    await page.goto('/catalogues');
    await page.getByRole('searchbox', { name: t('Find a catalogue') }).fill(token);

    await page.getByRole('button', { name: t('New catalogue'), exact: true }).click();
    const dialog = page.getByRole('dialog', { name: t('New catalogue') });
    await dialog.getByRole('textbox', { name: t('Name'), exact: true }).fill(title);
    await expect(dialog.getByRole('textbox', { name: t('Slug') })).toHaveValue(`gallery-${token}`);
    await dialog.getByRole('textbox', { name: t('Description') }).fill('Spring capsule for the gallery check.');
    await expect(dialog.getByRole('checkbox', { name: t('Web catalogue') })).toBeChecked();
    await expect(dialog.getByRole('checkbox', { name: t('PDF catalogue') })).toBeChecked();
    await dialog.getByRole('button', { name: t('Save catalogue') }).click();
    await expect(dialog).toBeHidden();

    const card = galleryCard(page, title);
    await expect(card).toBeVisible();
    await expect(card).toContainText(`/gallery-${token}`);
    await expect(card).toContainText(t('Inactive'));
    const created = (await listDesigns(request)).find((item) => item.title === title)!;
    expect(created.revision).toBe(1);
    expect(created.catalogue).toMatchObject({
      slug: `gallery-${token}`,
      isActive: false,
      outputs: { web: true, pdf: true },
    });

    // Rename through Settings; an existing catalogue keeps its slug until it is edited explicitly.
    await card.getByRole('button', { name: t('Settings'), exact: true }).click();
    const settings = page.getByRole('dialog', { name: t('Catalogue settings') });
    await settings.getByRole('textbox', { name: t('Name'), exact: true }).fill(renamed);
    await expect(settings.getByRole('textbox', { name: t('Slug') })).toHaveValue(`gallery-${token}`);
    await settings.getByRole('textbox', { name: t('Slug') }).fill(`gallery-renamed-${token}`);
    await settings.getByRole('button', { name: t('Save catalogue') }).click();
    await expect(settings).toBeHidden();
    const renamedCard = galleryCard(page, renamed);
    await expect(renamedCard).toContainText(`/gallery-renamed-${token}`);
    expect((await getDesign(request, created.id)).revision).toBe(2);

    await renamedCard.getByRole('button', { name: t('Duplicate'), exact: true }).click();
    const copy = galleryCard(page, `${renamed} (copy)`);
    await expect(copy).toBeVisible();
    await expect(copy).toContainText(t('Inactive'));
    await expect(copy).toContainText(`/gallery-renamed-${token}-copy-`);

    await renamedCard.getByRole('button', { name: t('Activate'), exact: true }).click();
    await expect(renamedCard.getByRole('button', { name: t('Deactivate'), exact: true })).toBeVisible();
    await expect(renamedCard.locator('.card-heading')).toContainText(t('Active'));
    const active = await getDesign(request, created.id);
    expect(active.snapshot.catalogue?.isActive).toBe(true);
    expect(active.revision).toBe(3);

    await renamedCard.getByRole('button', { name: t('Remove'), exact: true }).click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: t('Remove catalogue') })
      .click();
    await expect(renamedCard).toBeHidden();
    await expect(copy).toBeVisible();

    const archived = (await listDesigns(request, true)).find((item) => item.id === created.id);
    expect(archived?.archived).toBe(true);
    expect((await revisions(request, created.id)).map((item) => item.revision)).toEqual(
      expect.arrayContaining([1, 2, 3]),
    );
    await page.screenshot({ path: test.info().outputPath('gallery-after-archive.png') });
  });

  test('web-only, PDF-only and combined catalogues open the matching designer', async ({ page, request, t }) => {
    const token = runToken();
    const kinds = [
      { name: `Web only ${token}`, web: true, pdf: false },
      { name: `PDF only ${token}`, web: false, pdf: true },
      { name: `Combined ${token}`, web: true, pdf: true },
    ];
    await page.goto('/catalogues');
    await page.getByRole('searchbox', { name: t('Find a catalogue') }).fill(token);
    for (const kind of kinds) {
      await page.getByRole('button', { name: t('New catalogue'), exact: true }).click();
      const dialog = page.getByRole('dialog', { name: t('New catalogue') });
      await dialog.getByRole('textbox', { name: t('Name'), exact: true }).fill(kind.name);
      await dialog.getByRole('checkbox', { name: t('Web catalogue') }).setChecked(kind.web);
      await dialog.getByRole('checkbox', { name: t('PDF catalogue') }).setChecked(kind.pdf);
      await dialog.getByRole('button', { name: t('Save catalogue') }).click();
      await expect(dialog).toBeHidden();
      await expect(galleryCard(page, kind.name)).toBeVisible();
    }

    const saved = await listDesigns(request);
    for (const kind of kinds) {
      const card = galleryCard(page, kind.name);
      const summary = saved.find((item) => item.title === kind.name)!;
      expect(summary.catalogue?.outputs).toEqual({ web: kind.web, pdf: kind.pdf });
      // A PDF output starts with a cover page; a web-only catalogue has no fixed pages.
      expect(summary.catalogue?.pdfPageCount).toBe(kind.pdf ? 1 : 0);
      await expect(card.getByRole('button', { name: t('Web designer'), exact: true })).toHaveCount(kind.web ? 1 : 0);
      await expect(card.getByRole('button', { name: t('PDF designer'), exact: true })).toHaveCount(kind.pdf ? 1 : 0);

      if (kind.web) {
        await card.getByRole('button', { name: t('Web designer'), exact: true }).click();
        await expect(page).toHaveURL(new RegExp(`/catalogues/${summary.id}/web$`));
        await expect(page.getByRole('heading', { level: 1, name: t('Web designer') })).toBeVisible();
        await expect(page.getByRole('link', { name: t('PDF designer'), exact: true })).toHaveCount(kind.pdf ? 1 : 0);
        await page.goBack();
        await page.getByRole('searchbox', { name: t('Find a catalogue') }).fill(token);
      }
      if (kind.pdf) {
        await galleryCard(page, kind.name)
          .getByRole('button', { name: t('PDF designer'), exact: true })
          .click();
        await expect(page).toHaveURL(new RegExp(`/catalogues/${summary.id}/pdf$`));
        await expect(page.getByRole('heading', { level: 1, name: t('PDF designer') })).toBeVisible();
        await expect(page.getByRole('link', { name: t('Web designer'), exact: true })).toHaveCount(kind.web ? 1 : 0);
        await expect(page.locator('.page-rail li')).toHaveCount(1);
        await page.goBack();
        await page.getByRole('searchbox', { name: t('Find a catalogue') }).fill(token);
      }
    }
  });

  test('the PDF designer of a web-only catalogue says that PDF output is disabled', async ({ page, request, t }) => {
    const token = runToken();
    await page.goto('/catalogues');
    await page.getByRole('button', { name: t('New catalogue'), exact: true }).click();
    const dialog = page.getByRole('dialog', { name: t('New catalogue') });
    await dialog.getByRole('textbox', { name: t('Name'), exact: true }).fill(`Web address ${token}`);
    await dialog.getByRole('checkbox', { name: t('PDF catalogue') }).setChecked(false);
    await dialog.getByRole('button', { name: t('Save catalogue') }).click();
    await expect(dialog).toBeHidden();
    const webOnly = (await listDesigns(request)).find((item) => item.title === `Web address ${token}`)!;

    await openDesigner(page, webOnly.id, 'pdf', t('PDF designer'));
    await expect(page.getByText(t('PDF output is disabled. Enable it in catalogue settings.'))).toBeVisible();
  });
});
