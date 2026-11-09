// @vitest-environment node
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { describe, expect, it } from 'vitest';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';
import { BootStageCount, SplashLogo } from '../src/bootstrap/BootSplash';
import { ThemeStorageKey } from '../src/bootstrap/theme';

const script = colorModeScript({ storageKey: ThemeStorageKey });
const splash = readFileSync(new URL('../public/boot-splash.css', import.meta.url), 'utf8');
const stylesheet = readFileSync(new URL('../src/bootstrap/index.css', import.meta.url), 'utf8');
const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8');
/** The document with its tags on one line each, so attribute checks ignore the formatter's wrapping. */
const flatHtml = html.replace(/\s*\n\s*/g, ' ').replace(/\s+\/>/g, ' />');

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

/** A PNG's pixel size and whether it carries an alpha channel, read from its header. */
function png(path: string) {
  const bytes = readFileSync(new URL(`../public/${path}`, import.meta.url));
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20), alpha: bytes[25] === 6 };
}

describe('Brand document icons', () => {
  it('links the Ocharo symbol as the tab icon, reversed for a dark tab strip, and the app tile as the touch icon', () => {
    expect(flatHtml).toContain('<link rel="icon" type="image/png" sizes="32x32" href="%BASE_URL%favicon.png" />');
    expect(flatHtml).toContain(
      '<link rel="icon" type="image/png" sizes="32x32" href="%BASE_URL%favicon-dark.png" media="(prefers-color-scheme: dark)" />',
    );
    expect(flatHtml).toContain('<link rel="apple-touch-icon" href="%BASE_URL%apple-touch-icon.png" />');
    expect(png('favicon.png')).toMatchObject({ width: 32, height: 32 });
    expect(png('favicon-dark.png')).toMatchObject({ width: 32, height: 32 });
    expect(png('apple-touch-icon.png')).toEqual({ width: 180, height: 180, alpha: false });
  });
});

describe('Brand splash twin', () => {
  it('paints the splash’s logo and bar inside the mount node, styled only by the early stylesheet', () => {
    const root = flatHtml.slice(flatHtml.indexOf('<div id="root">'), flatHtml.indexOf('<script type="module"'));
    expect(root).toContain('class="boot-splash" role="status"');
    expect(root).toContain('src="%BASE_URL%brand/ocharo-wordmark-ink.png"');
    expect(root).toContain('src="%BASE_URL%brand/ocharo-wordmark-reverse.png"');
    expect(root).toContain('width="240" height="90"');
    expect(root).toContain('class="boot-splash-bar"');
    expect(png('brand/ocharo-wordmark-ink.png')).toMatchObject({ width: 480, height: 180 });
    expect(png('brand/ocharo-wordmark-reverse.png')).toMatchObject({ width: 480, height: 180 });
  });

  it('keeps the twin’s logo, bar and colours equal to the mounted SDK splash in both colour modes', () => {
    expect(SplashLogo).toMatchObject({ width: 240, height: 90 });
    expect(SplashLogo.ink.endsWith('brand/ocharo-wordmark-ink.png')).toBe(true);
    expect(SplashLogo.reverse.endsWith('brand/ocharo-wordmark-reverse.png')).toBe(true);
    // The SDK SplashScreen: page colour, a 12rem small bar (0.25rem) on the muted track with the primary fill, at the
    // first of the boot stages.
    expect(splash).toMatch(/\.boot-splash \{[^}]*background: #f5f6fa;/);
    expect(splash).toMatch(/html\.dark \.boot-splash \{\s*background: #141416;/);
    expect(splash).toMatch(/\.boot-splash-bar \{[^}]*width: 12rem;[^}]*height: 0\.25rem;[^}]*background: #eef0f6;/);
    expect(splash).toMatch(/html\.dark \.boot-splash-bar \{\s*background: #262629;/);
    expect(splash).toMatch(/\.boot-splash-fill \{[^}]*width: 33\.3333%;[^}]*background: #8b2459;/);
    expect(splash).toMatch(/html\.dark \.boot-splash-fill \{\s*background: #c24783;/);
    expect(1 / BootStageCount).toBeCloseTo(0.333333, 5);
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
