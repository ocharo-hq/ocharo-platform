/// <reference types="node" />
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { nativeDesignerUiMessages } from '../src/domain/shared/NativeDesignerUiMessages';
import { translateUi, uiMessages } from '../src/domain/shared/UiMessages';

describe('native catalogue interface languages', () => {
  it('covers literal designer and public interface phrases without changing authored copy', () => {
    const root = resolve(import.meta.dirname, '../src');
    const missing = new Set<string>();
    for (const directory of [
      'presentation/design',
      'presentation/public',
      'application/design',
      'application/public',
    ]) {
      for (const filename of readdirSync(resolve(root, directory))) {
        if (!/\.(vue|ts)$/.test(filename)) continue;
        const source = readFileSync(resolve(root, directory, filename), 'utf8');
        for (const match of source.matchAll(/\bt\(\s*(['"])(.*?)\1/gs)) {
          const phrase = match[2];
          if (phrase && !uiMessages[phrase]) missing.add(phrase);
        }
      }
    }
    expect([...missing]).toEqual([]);
    expect(translateUi('A custom buyer-facing title', 'ru')).toBe('A custom buyer-facing title');
  });

  it('retains every interpolation parameter in both interface translations', () => {
    for (const [source, messages] of Object.entries(nativeDesignerUiMessages)) {
      const parameters = (value: string) => [...value.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
      for (const locale of ['uz', 'ru'] as const) {
        expect(messages[locale].trim(), `${source}: ${locale}`).not.toBe('');
        expect(messages[locale], `${source}: ${locale}`).not.toBe(source);
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
