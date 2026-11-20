import { describe, expect, it } from 'vitest';
import { catalogueFrameUiMessages } from '../src/domain/shared/CatalogueFrameUiMessages';
import { ocharoApps } from '../src/domain/shared/OcharoApps';
import { translateUi, uiMessages } from '../src/domain/shared/UiMessages';

/** The interface is English only for now; the uz and ru entries stay in the tables for when the choice returns. */
describe('Ocharo frame interface messages', () => {
  it('answers the English source for a phrase with no entry, in every locale, so a new string needs English only', () => {
    expect('A phrase nobody translated: {count}' in uiMessages).toBe(false);
    for (const locale of ['en', 'uz', 'ru'] as const)
      expect(translateUi('A phrase nobody translated: {count}', locale, { count: 3 })).toBe(
        'A phrase nobody translated: 3',
      );
  });

  it('names the Editorial app apart from the Editorial designer theme', () => {
    const [editorial] = ocharoApps({ editorial: 'https://example.test/' });
    const locales = ['en', 'uz', 'ru'] as const;
    expect(locales.map((locale) => translateUi(editorial!.label, locale))).toEqual([
      'Editorial',
      'Tahririyat',
      'Редакция',
    ]);
    expect(locales.map((locale) => translateUi(editorial!.summary, locale))).toEqual([
      'Blog stories',
      'Blog hikoyalari',
      'Истории блога',
    ]);
    expect(locales.map((locale) => translateUi('Editorial', locale))).toEqual([
      'Editorial',
      'Jurnal uslubi',
      'Журнальный стиль',
    ]);
  });

  it('retains every interpolation parameter in every translation the table holds', () => {
    for (const [source, messages] of Object.entries(catalogueFrameUiMessages)) {
      const parameters = (value: string) => [...value.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
      for (const locale of ['uz', 'ru'] as const) {
        expect(messages[locale].trim(), `${source}: ${locale}`).not.toBe('');
        expect(parameters(messages[locale]), `${source}: ${locale}`).toEqual(parameters(source));
      }
    }
    expect(translateUi('Ocharo apps — {app}', 'ru', { app: 'Каталог' })).toBe('Приложения Ocharo — Каталог');
  });
});
