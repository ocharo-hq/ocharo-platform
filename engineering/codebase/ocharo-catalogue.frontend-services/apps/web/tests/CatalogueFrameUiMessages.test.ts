/// <reference types="node" />
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { OcharoAppsProblem } from '../src/application/shared/UseOcharoApps';
import { catalogueFrameUiMessages } from '../src/domain/shared/CatalogueFrameUiMessages';
import { CatalogueGroupLabels, CatalogueSections } from '../src/domain/shared/CatalogueSections';
import { ocharoApps } from '../src/domain/shared/OcharoApps';
import { translateUi, uiMessages } from '../src/domain/shared/UiMessages';

describe('Ocharo frame interface languages', () => {
  it('covers every literal phrase and route label of the frame and its shared controls', () => {
    const root = resolve(import.meta.dirname, '../src');
    const missing = new Set<string>();
    for (const directory of [
      'bootstrap',
      'presentation/frame',
      'presentation/common',
      'application/shared',
      'application/common',
    ]) {
      for (const filename of readdirSync(resolve(root, directory))) {
        if (!/\.(vue|ts)$/.test(filename)) continue;
        const source = readFileSync(resolve(root, directory, filename), 'utf8');
        for (const match of source.matchAll(/\b(?:t|place)\(\s*(['"])(.*?)\1/gs)) {
          const phrase = match[2];
          if (phrase && !uiMessages[phrase]) missing.add(phrase);
        }
      }
    }
    expect([...missing]).toEqual([]);
  });

  it('translates every app, section, group and startup message the frame shows', () => {
    const everyApp = ocharoApps(
      Object.fromEntries(
        ['desk', 'catalogue', 'studio', 'marketing', 'editorial', 'brand', 'atlas', 'wear', 'blog'].map((key) => [
          key,
          'https://example.test/',
        ]),
      ),
    );
    const phrases = [
      ...everyApp.flatMap((app) => [app.label, app.summary]),
      ...CatalogueSections.map((section) => section.label),
      ...Object.values(CatalogueGroupLabels).filter((label): label is string => Boolean(label)),
      ...Object.values(OcharoAppsProblem),
    ];
    expect(everyApp).toHaveLength(9);
    expect(phrases.filter((phrase) => !uiMessages[phrase])).toEqual([]);
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

  it('retains every interpolation parameter in both interface translations', () => {
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
