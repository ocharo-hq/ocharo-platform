import { describe, expect, it } from 'vitest';
import { brandFrameUiMessages } from '../src/domain/shared/BrandFrameUiMessages';
import { BrandSections } from '../src/domain/shared/BrandSections';
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

  it('retains every interpolation parameter in every translation the table holds', () => {
    for (const [source, messages] of Object.entries(brandFrameUiMessages)) {
      const parameters = (value: string) => [...value.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
      for (const locale of ['uz', 'ru'] as const) {
        expect(messages[locale].trim(), `${source}: ${locale}`).not.toBe('');
        expect(parameters(messages[locale]), `${source}: ${locale}`).toEqual(parameters(source));
      }
    }
    expect(translateUi('Ocharo apps — {app}', 'ru', { app: 'Бренд' })).toBe('Приложения Ocharo — Бренд');
    expect(translateUi('Release {release} · {count} unpublished changes', 'uz', { release: 4, count: 0 })).toBe(
      '4-nashr · 0 ta nashr qilinmagan o‘zgarish',
    );
  });

  it('names the Editorial app by its own key, as the other app names, not as a writing style', () => {
    const [editorial] = ocharoApps({ editorial: 'https://example.test/' });
    expect((['en', 'uz', 'ru'] as const).map((locale) => translateUi(editorial!.label, locale))).toEqual([
      'Editorial',
      'Tahririyat',
      'Редакция',
    ]);
    expect(translateUi(editorial!.summary, 'uz')).toBe('Blog hikoyalari');
    expect(uiMessages.Editorial).toBeUndefined();
  });

  it('keeps the Uzbek words the sidebar already used for Brand’s own sections', () => {
    expect(BrandSections.map((section) => translateUi(section.label, 'uz'))).toEqual([
      'Umumiy',
      'Identitet',
      'Logotiplar',
      'Tadbirlar',
      'Ulanish',
    ]);
  });
});
