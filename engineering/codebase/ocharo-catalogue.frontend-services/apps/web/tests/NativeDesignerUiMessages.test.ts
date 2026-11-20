import { describe, expect, it } from 'vitest';
import { nativeDesignerUiMessages } from '../src/domain/shared/NativeDesignerUiMessages';
import { translateUi } from '../src/domain/shared/UiMessages';

/** The interface is English only for now; the uz and ru entries stay in the tables for when the choice returns. */
describe('native catalogue interface messages', () => {
  it('answers the English source for a phrase with no entry, so a new string needs English only', () => {
    expect(translateUi('A custom buyer-facing title', 'ru')).toBe('A custom buyer-facing title');
    expect(translateUi('A custom buyer-facing title', 'en')).toBe('A custom buyer-facing title');
  });

  it('retains every interpolation parameter in every translation the table holds', () => {
    for (const [source, messages] of Object.entries(nativeDesignerUiMessages)) {
      const parameters = (value: string) => [...value.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
      for (const locale of ['uz', 'ru'] as const) {
        expect(messages[locale].trim(), `${source}: ${locale}`).not.toBe('');
        expect(parameters(messages[locale]), `${source}: ${locale}`).toEqual(parameters(source));
      }
    }
    expect(translateUi('Rendering {page} of {total}', 'ru', { page: 2, total: 16 })).toBe('Обработка страницы 2 из 16');
  });

  it('translates dynamic catalogue controls by display names', () => {
    for (const phrase of [
      'Collection title',
      'Header',
      'Footer',
      'Quantity label',
      'Price heading',
      'Show prices',
      'Show quantity',
      'Show retail price',
      'Show wholesale price',
      'All active images',
      'Model images',
      'Product images',
      'Detail images',
      'Fabric images',
      'Price label',
      'Cutout',
      'Reference',
      'Price tier',
    ]) {
      for (const locale of ['uz', 'ru'] as const) expect(translateUi(phrase, locale)).not.toBe(phrase);
    }
  });
});
