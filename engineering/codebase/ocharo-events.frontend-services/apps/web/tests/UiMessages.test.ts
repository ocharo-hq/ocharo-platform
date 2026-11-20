import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

import { ocharoKinds, ocharoRegions, ocharoWorkflowSpec } from '@/domain/events/Events';
import { translateUi, uiMessages } from '@/domain/shared/UiMessages';

const root = join(process.cwd(), 'src') + '/';

/** Every source file under `src`. */
function sources(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return sources(path);
    return /\.(ts|vue)$/.test(entry.name) ? [path] : [];
  });
}

describe('interface messages', () => {
  it("hands the kit Ocharo's labels with an English text each, the other languages optional", () => {
    const texts = [
      ...ocharoWorkflowSpec().stages.map((stage) => stage.label!),
      ...ocharoKinds().map((kind) => kind.label!),
      ...ocharoRegions().map((region) => region.label),
    ];
    expect(texts.filter((text) => !text.en)).toEqual([]);
  });

  it('keeps a well-formed uz and ru text in every entry the table holds, new phrases needing English only', () => {
    const malformed = Object.entries(uiMessages)
      .filter(([, value]) => !value.uz.trim() || !value.ru.trim() || value.en?.trim() === '')
      .map(([key]) => key);
    expect(malformed).toEqual([]);
  });

  it('answers the English source for a phrase with no entry, in every locale, filling its {parameters}', () => {
    expect('A phrase nobody translated: {count}' in uiMessages).toBe(false);
    for (const locale of ['en', 'uz', 'ru'] as const)
      expect(translateUi('A phrase nobody translated: {count}', locale, { count: 3 })).toBe(
        'A phrase nobody translated: 3',
      );
  });

  it('keeps no "Showcase events" string in the source tree', () => {
    const offenders = sources(root).filter((file) => readFileSync(file, 'utf8').includes('Showcase events'));
    expect(offenders.map((file) => file.replace(root, 'src/'))).toEqual([]);
    expect('Showcase events' in uiMessages).toBe(false);
  });

  it('keeps every {parameter} of the English key in every translation present', () => {
    const parameters = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
    const broken = Object.entries(uiMessages)
      .filter(([key, value]) =>
        [value.uz, value.ru].some((text) => parameters(text).join() !== parameters(value.en ?? key).join()),
      )
      .map(([key]) => key);
    expect(broken).toEqual([]);
  });

  it('keeps the Uzbek and Russian text the tables hold, distinct from the English source, for when the choice returns', () => {
    expect(translateUi('Sign out', 'uz')).toBe('Chiqish');
    expect(translateUi('Sign out', 'ru')).toBe('Выйти');
    expect(translateUi('Sign out', 'en')).toBe('Sign out');
    expect(translateUi('Your account id: {id}', 'ru', { id: 'owner-sub' })).toBe(
      'Идентификатор вашего аккаунта: owner-sub',
    );
  });
});
