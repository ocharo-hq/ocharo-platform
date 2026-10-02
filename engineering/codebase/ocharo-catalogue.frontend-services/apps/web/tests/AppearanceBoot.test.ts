// @vitest-environment node
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { describe, expect, it } from 'vitest';
const script = readFileSync(new URL('../public/appearance.js', import.meta.url), 'utf8');
const stylesheet = readFileSync(new URL('../public/appearance.css', import.meta.url), 'utf8');
const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8');
describe('Catalogue first paint', () => {
  it.each([
    ['dark', false, 'dark'],
    ['dark', true, 'dark'],
    ['light', true, 'light'],
    ['system', true, 'dark'],
    ['system', false, 'light'],
    [null, true, 'dark'],
    ['invalid', false, 'light'],
  ])('honors saved %s with system dark %s before the app module', (saved, systemDark, expected) => {
    const classes = new Set<string>();
    runInNewContext(script, {
      localStorage: { getItem: () => saved },
      window: { matchMedia: () => ({ matches: systemDark }) },
      document: {
        documentElement: {
          classList: {
            toggle: (name: string, enabled: boolean) => (enabled ? classes.add(name) : classes.delete(name)),
          },
        },
      },
    });
    expect([...classes]).toEqual([expected]);
  });
  it('keeps approved backgrounds on html, body and root without inline script or styles', () => {
    const application = html.indexOf('type="module"');
    expect(html.indexOf('href="/appearance.css"')).toBeLessThan(application);
    expect(html.indexOf('src="/appearance.js"')).toBeLessThan(application);
    expect(html).not.toMatch(/<style|<script(?![^>]*src=)/);
    expect(stylesheet).toContain('background: #f5f6fa');
    expect(stylesheet).toContain('background: #141416');
    expect(stylesheet).toMatch(/body,\s*#root/);
  });
  it('works when browser storage is unavailable', () => {
    const classes = new Set<string>();
    runInNewContext(script, {
      localStorage: {
        getItem: () => {
          throw new Error('Storage denied');
        },
      },
      window: { matchMedia: () => ({ matches: true }) },
      document: {
        documentElement: {
          classList: {
            toggle: (name: string, enabled: boolean) => (enabled ? classes.add(name) : classes.delete(name)),
          },
        },
      },
    });
    expect([...classes]).toEqual(['dark']);
  });
});
