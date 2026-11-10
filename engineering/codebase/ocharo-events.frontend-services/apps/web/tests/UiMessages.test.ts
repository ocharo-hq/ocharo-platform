import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';

import {
  datePrecisions,
  eventFormats,
  eventPriorities,
  eventScopes,
  eventStatuses,
  formatLabels,
  precisionLabels,
  priorityLabels,
  scopeLabels,
  statusLabels,
} from '@/domain/events/Events';
import { failureMessage } from '@/application/events/UseEventsAccess';
import { OcharoAppsProblem } from '@/application/shared/UseOcharoApps';
import { ocharoApps } from '@/domain/shared/OcharoApps';
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

/** The string literals passed straight to `t(…)`, `notifyError(…)` or a form's `problems.push(…)`, unescaped. */
function literals(): Map<string, string> {
  const found = new Map<string, string>();
  const pattern = /\b(?:t|notifyError|problems\.push)\(\s*(?:'((?:\\.|[^'\\])*)'|"((?:\\.|[^"\\])*)")/g;
  for (const file of sources(root)) {
    const text = readFileSync(file, 'utf8');
    for (const match of text.matchAll(pattern)) {
      const raw = match[1] ?? match[2] ?? '';
      found.set(raw.replace(/\\(['"\\])/g, '$1'), file.replace(root, 'src/'));
    }
  }
  return found;
}

describe('interface messages', () => {
  it('translates every literal the app passes to t(…) or notifyError(…)', () => {
    const missing = [...literals()].filter(([key]) => !(key in uiMessages)).map(([key, file]) => `${key} (${file})`);
    expect(missing).toEqual([]);
  });

  it('translates the labels looked up by value: event values, failures, startup problems and the apps menu', () => {
    const labels = [
      ...eventScopes.map((value) => scopeLabels[value]),
      ...eventFormats.map((value) => formatLabels[value]),
      ...eventStatuses.map((value) => statusLabels[value]),
      ...datePrecisions.map((value) => precisionLabels[value]),
      ...eventPriorities.map((value) => priorityLabels[value]),
      ...[400, 401, 403, 404, 409, 500].map((status) =>
        failureMessage(ApiFailureFactory.create('http', { status, headers: new Headers() })),
      ),
      failureMessage(ApiFailureFactory.create('protocol')),
      failureMessage(ApiFailureFactory.create('cancelled')),
      ...Object.values(OcharoAppsProblem),
      'Not checked',
      'Checking…',
      'Unavailable',
      ...ocharoApps(
        Object.fromEntries(['events', 'brand', 'desk'].map((key) => [key, `https://${key}.example.test/`])),
      ).flatMap((app) => [app.label, app.summary]),
    ];
    expect(labels.filter((label) => !(label in uiMessages))).toEqual([]);
  });

  it('keeps every {parameter} of the English key in both translations', () => {
    const parameters = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
    const broken = Object.entries(uiMessages)
      .filter(([key, value]) =>
        [value.uz, value.ru].some((text) => parameters(text).join() !== parameters(value.en ?? key).join()),
      )
      .map(([key]) => key);
    expect(broken).toEqual([]);
  });

  it('answers Uzbek and Russian text distinct from the English source for the frame and the page', () => {
    expect(translateUi('Sign out', 'uz')).toBe('Chiqish');
    expect(translateUi('Sign out', 'ru')).toBe('Выйти');
    expect(translateUi('Sign out', 'en')).toBe('Sign out');
    expect(translateUi('Apply by {date}', 'ru', { date: '15 окт' })).toBe('Заявка до 15 окт');
  });
});
