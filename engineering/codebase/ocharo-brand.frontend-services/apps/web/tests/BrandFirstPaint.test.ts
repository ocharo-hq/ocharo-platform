// @vitest-environment node
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { describe, expect, it } from 'vitest';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';
import { ThemeStorageKey } from '../src/bootstrap/theme';

const script = colorModeScript({ storageKey: ThemeStorageKey });
const splash = readFileSync(new URL('../public/boot-splash.css', import.meta.url), 'utf8');
const stylesheet = readFileSync(new URL('../src/bootstrap/index.css', import.meta.url), 'utf8');
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

describe('Brand first paint', () => {
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

  it('reads Brand’s own key, which no earlier build wrote', () => {
    expect(ThemeStorageKey).toBe('ocharo.brand.theme');
    expect(script).toContain(JSON.stringify(ThemeStorageKey));
  });

  it('loads the SDK theme, the pre-paint script and the splash colours before the application, with no inline code', () => {
    const application = html.indexOf('type="module"');
    expect(html).toMatch(/<html[^>]*class="theme-ocharo"/);
    expect(html.indexOf('src="%BASE_URL%theme.js"')).toBeGreaterThan(-1);
    expect(html.indexOf('src="%BASE_URL%theme.js"')).toBeLessThan(application);
    expect(html.indexOf('href="%BASE_URL%boot-splash.css"')).toBeLessThan(application);
    expect(html).not.toMatch(/<style|<script(?![^>]*src=)/);
    expect(splash).toContain('background: #f5f6fa');
    expect(splash).toContain('background: #141416');
  });

  it('leaves the page to the provider when browser storage is unavailable', () => {
    const result = paint(() => {
      throw new Error('Storage denied');
    }, true);
    expect(result.classes).toEqual([]);
  });
});

describe('Brand artwork colours', () => {
  it('takes every interface colour from the SDK theme, with no app palette left', () => {
    expect(stylesheet).toContain("@import '@wow-two-beta/fe/themes.css';");
    expect(stylesheet).not.toMatch(/--color-(background|foreground|primary|card|border|muted)\s*:/);
  });

  it('defines the logo proof surfaces once, as fixed tones that no dark rule redefines', () => {
    const tokens = {
      '--color-artwork-paper': '#f8f7f3',
      '--color-artwork-ink': '#20211e',
      '--color-artwork-check': '#e6e2d8',
      '--color-artwork-check-alt': '#fffdf8',
    };
    for (const [token, colour] of Object.entries(tokens)) {
      expect(stylesheet.match(new RegExp(`${token}\\s*:`, 'g'))).toHaveLength(1);
      expect(stylesheet).toContain(`${token}: ${colour};`);
    }
    const dark = stylesheet.slice(
      stylesheet.indexOf('.dark {'),
      stylesheet.indexOf('}', stylesheet.indexOf('.dark {')),
    );
    expect(dark).not.toContain('artwork');
  });
});
