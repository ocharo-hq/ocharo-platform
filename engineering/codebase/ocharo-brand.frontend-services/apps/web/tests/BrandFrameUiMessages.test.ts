/// <reference types="node" />
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { OcharoAppsProblem } from '../src/application/shared/UseOcharoApps';
import { brandFrameUiMessages } from '../src/domain/shared/BrandFrameUiMessages';
import { BrandGroupLabels, BrandSections } from '../src/domain/shared/BrandSections';
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
      ...BrandSections.map((section) => section.label),
      ...Object.values(BrandGroupLabels).filter((label): label is string => Boolean(label)),
      ...Object.values(OcharoAppsProblem),
    ];
    expect(everyApp).toHaveLength(9);
    expect(phrases.filter((phrase) => !uiMessages[phrase])).toEqual([]);
  });

  it('retains every interpolation parameter in both interface translations', () => {
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
