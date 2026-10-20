// @vitest-environment node
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { describe, expect, it } from 'vitest';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';
import { ThemeStorageKey } from '../src/bootstrap/theme';

const script = colorModeScript({ storageKey: ThemeStorageKey });
const stylesheet = readFileSync(new URL('../public/boot-splash.css', import.meta.url), 'utf8');
const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8');

/** Runs the generated pre-paint script against a stored value and the device's colour preference. */
function paint(getItem: () => string | null, systemDark: boolean) {
  const classes = new Set<string>();
  const style: { colorScheme?: string } = {};
  runInNewContext(script, {
    localStorage: { getItem },
    matchMedia: () => ({ matches: systemDark }),
    document: {
      documentElement: {
        style,
        classList: {
          toggle: (name: string, enabled: boolean) => (enabled ? classes.add(name) : classes.delete(name)),
        },
      },
    },
  });
  return { classes: [...classes], colorScheme: style.colorScheme };
}

describe('Catalogue first paint', () => {
  it.each([
    ['dark', false, 'dark'],
    ['dark', true, 'dark'],
    ['light', true, 'light'],
    ['system', true, 'dark'],
    ['system', false, 'light'],
    [null, true, 'dark'],
    ['invalid', false, 'light'],
  ])('honors the stored %s with system dark %s before the app module', (saved, systemDark, expected) => {
    const result = paint(() => saved, systemDark);
    expect(result.classes).toEqual(expected === 'dark' ? ['dark'] : []);
    expect(result.colorScheme).toBe(expected);
  });

  it('reads the key earlier Catalogue builds stored the appearance under', () => {
    expect(ThemeStorageKey).toBe('ocharo.catalogue.theme');
    expect(script).toContain(JSON.stringify(ThemeStorageKey));
  });

  it('loads the SDK theme, the pre-paint script and the splash colours before the application, with no inline code', () => {
    const application = html.indexOf('type="module"');
    expect(html).toMatch(/<html[^>]*class="theme-ocharo"/);
    expect(html.indexOf('src="%BASE_URL%theme.js"')).toBeGreaterThan(-1);
    expect(html.indexOf('src="%BASE_URL%theme.js"')).toBeLessThan(application);
    expect(html.indexOf('href="%BASE_URL%boot-splash.css"')).toBeLessThan(application);
    expect(html).not.toMatch(/<style|<script(?![^>]*src=)/);
    expect(stylesheet).toContain('background: #f5f6fa');
    expect(stylesheet).toContain('background: #141416');
  });

  it('leaves the page to the provider when browser storage is unavailable', () => {
    const result = paint(() => {
      throw new Error('Storage denied');
    }, true);
    expect(result.classes).toEqual([]);
  });
});
