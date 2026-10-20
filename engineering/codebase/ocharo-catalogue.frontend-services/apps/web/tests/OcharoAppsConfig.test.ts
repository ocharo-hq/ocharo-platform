import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfigError, staticSource } from '@wow-two-beta/fe/foundation/config';

import { feedbackBus } from '../src/application/common/UseFeedback';
import {
  OcharoAppsProblem,
  buildTimeConfigSource,
  loadOcharoApps,
  reportOcharoAppsStartup,
  resolveOcharoAppsConfig,
  runtimeConfigSource,
} from '../src/application/shared/UseOcharoApps';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { ocharoApps } from '../src/domain/shared/OcharoApps';
import { fetchBrandKit } from '../src/integration/brand';

/** The local-run addresses Catalogue falls back to when nothing configures an app; the app keys match Desk's. */
const LocalDefaults = {
  OCHARO_DESK_URL: 'https://localhost:8264/',
  OCHARO_CATALOGUE_URL: 'https://localhost:8282/catalogues',
  OCHARO_STUDIO_URL: 'https://localhost:8262/studio',
  OCHARO_MARKETING_URL: 'https://localhost:8358/',
  OCHARO_EDITORIAL_URL: 'https://localhost:8284/studio/',
  OCHARO_BRAND_URL: 'https://localhost:8288/',
  OCHARO_ATLAS_URL: 'https://localhost:8284/atlas/',
  OCHARO_WEAR_URL: 'https://localhost:8284/wear/',
  OCHARO_BLOG_URL: 'https://localhost:8284/',
  OCHARO_BRAND_API_URL: 'https://localhost:8286/',
};

/** Answers `/api/runtime-config` with `body`, and records every request. */
function serveRuntimeConfig(body: unknown | (() => Promise<Response>)) {
  const fetch = vi
    .fn()
    .mockImplementation(() =>
      typeof body === 'function' ? (body as () => Promise<Response>)() : Promise.resolve(Response.json(body)),
    );
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

beforeEach(() => useUiLocale().setUiLocale('en'));
afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});

describe('Ocharo app addresses', () => {
  it('uses the local-run defaults when no source configures an app', () => {
    expect({ ...resolveOcharoAppsConfig([]) }).toEqual(LocalDefaults);
  });

  it('lets a runtime value override the build-time value of the same key', () => {
    const config = resolveOcharoAppsConfig([
      runtimeConfigSource({ OCHARO_DESK_URL: 'https://desk.example.test/' }),
      staticSource({
        VITE_OCHARO_DESK_URL: 'https://build.example.test/',
        VITE_OCHARO_BLOG_URL: 'https://blog.example.test/',
      }),
    ]);

    expect(config.OCHARO_DESK_URL).toBe('https://desk.example.test/');
    expect(config.OCHARO_BLOG_URL).toBe('https://blog.example.test/');
    expect(config.OCHARO_CATALOGUE_URL).toBe(LocalDefaults.OCHARO_CATALOGUE_URL);
  });

  it('keeps reading the VITE_OCHARO_* build variables', () => {
    vi.stubEnv('VITE_OCHARO_WEAR_URL', 'https://wear.example.test/');

    expect(resolveOcharoAppsConfig([buildTimeConfigSource()]).OCHARO_WEAR_URL).toBe('https://wear.example.test/');
  });

  it('rejects a malformed runtime value instead of falling back to the build-time one', () => {
    const resolve = () =>
      resolveOcharoAppsConfig([
        runtimeConfigSource({ OCHARO_DESK_URL: 'not a link' }),
        staticSource({ VITE_OCHARO_DESK_URL: 'https://build.example.test/' }),
      ]);

    expect(resolve).toThrow(ConfigError);
    try {
      resolve();
    } catch (error) {
      expect((error as ConfigError).issues).toEqual([
        expect.objectContaining({ key: 'OCHARO_DESK_URL', lookupKey: 'VITE_OCHARO_DESK_URL', reason: 'invalid' }),
      ]);
    }
  });

  it('keeps the menu to plain HTTPS links even when a configured value is a well-formed URL', () => {
    const config = resolveOcharoAppsConfig([
      runtimeConfigSource({
        OCHARO_STUDIO_URL: 'http://studio.example.test/',
        OCHARO_BRAND_URL: 'https://brand.example.test/?token=x',
      }),
    ]);
    const keys = ocharoApps({
      studio: config.OCHARO_STUDIO_URL,
      brand: config.OCHARO_BRAND_URL,
      desk: config.OCHARO_DESK_URL,
    }).map((app) => app.key);

    expect(keys).toEqual(['desk']);
  });

  it('lists the same apps, labels and summaries as Desk, in Desk’s order', () => {
    const apps = ocharoApps({ desk: LocalDefaults.OCHARO_DESK_URL, catalogue: LocalDefaults.OCHARO_CATALOGUE_URL });

    expect(apps).toEqual([
      expect.objectContaining({ key: 'desk', label: 'Desk', summary: 'Events, sourcing and products' }),
      expect.objectContaining({ key: 'catalogue', label: 'Catalogue', summary: 'Products, prices and editions' }),
    ]);
  });
});

describe('Brand addresses', () => {
  it('keeps the Brand API the print sheet reads apart from the Brand app the menu opens', async () => {
    const startup = await (serveRuntimeConfig({}), loadOcharoApps());

    expect(startup.addresses.brand).toBe('https://localhost:8288/');
    expect(startup.brandApiUrl).toBe('https://localhost:8286/');
  });

  it('reads the Brand API root from its own build variable, not the Brand app one', () => {
    vi.stubEnv('VITE_OCHARO_BRAND_URL', 'https://brand-app.example.test/');
    vi.stubEnv('VITE_OCHARO_BRAND_API_URL', 'https://brand-api.example.test/');

    const config = resolveOcharoAppsConfig([buildTimeConfigSource()]);

    expect(config.OCHARO_BRAND_URL).toBe('https://brand-app.example.test/');
    expect(config.OCHARO_BRAND_API_URL).toBe('https://brand-api.example.test/');
  });

  it('fetches the published kit from the Brand API root and resolves its logo files there', async () => {
    const fetch = vi.fn().mockResolvedValue(
      Response.json({
        name: 'Ocharo',
        release: 3,
        descriptors: { uz: null, ru: null, en: null },
        links: [],
        logos: { 'on-light': { url: '/api/public/brand/logo/on-light', fileUrl: '/media/logo.svg' } },
      }),
    );

    const kit = await fetchBrandKit('https://brand-api.example.test/', fetch);

    expect(String(fetch.mock.calls[0]?.[0])).toBe('https://brand-api.example.test/api/public/brand?app=catalogue');
    expect(kit?.logos['on-light']?.fileUrl).toBe('https://brand-api.example.test/media/logo.svg');
    expect(await fetchBrandKit(null, fetch)).toBeNull();
    expect(fetch).toHaveBeenCalledTimes(1);
  });
});

describe('startup resolution', () => {
  it('reads the public settings once, anonymously, and applies them ahead of the defaults', async () => {
    const fetch = serveRuntimeConfig({ OCHARO_DESK_URL: 'https://desk.example.test/', UNRELATED: 'ignored' });

    const startup = await loadOcharoApps();

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledWith(
      '/api/runtime-config',
      expect.objectContaining({ method: 'GET', credentials: 'omit' }),
    );
    expect(startup.problems).toEqual([]);
    expect(startup.addresses).toMatchObject({
      desk: 'https://desk.example.test/',
      catalogue: LocalDefaults.OCHARO_CATALOGUE_URL,
    });
    expect(ocharoApps(startup.addresses)).toHaveLength(9);
  });

  it.each([
    ['offline', () => Promise.reject(new TypeError('offline'))],
    ['server error', () => Promise.resolve(Response.json({ title: 'Unavailable' }, { status: 503 }))],
    [
      'HTML fallback',
      () => Promise.resolve(new Response('<html></html>', { headers: { 'Content-Type': 'text/html' } })),
    ],
    ['non-object body', () => Promise.resolve(Response.json(['https://desk.example.test/']))],
  ])('continues with the build-time values and reports an unreadable response (%s)', async (_label, respond) => {
    serveRuntimeConfig(respond);
    vi.stubEnv('VITE_OCHARO_DESK_URL', 'https://build.example.test/');

    const startup = await loadOcharoApps();

    expect(startup.addresses.desk).toBe('https://build.example.test/');
    expect(startup.problems.map((problem) => problem.message)).toEqual([OcharoAppsProblem.Unavailable]);
  });

  it('leaves out an app whose served value is malformed and reports it, keeping every other address', async () => {
    serveRuntimeConfig({
      OCHARO_DESK_URL: 'not a link',
      OCHARO_ATLAS_URL: 42,
      OCHARO_BLOG_URL: 'https://blog.example.test/',
    });
    vi.stubEnv('VITE_OCHARO_DESK_URL', 'https://build.example.test/');

    const startup = await loadOcharoApps();

    expect(startup.addresses.desk).toBeUndefined();
    expect(startup.addresses.atlas).toBeUndefined();
    expect(startup.addresses.blog).toBe('https://blog.example.test/');
    expect(startup.addresses.catalogue).toBe(LocalDefaults.OCHARO_CATALOGUE_URL);
    expect(startup.brandApiUrl).toBe(LocalDefaults.OCHARO_BRAND_API_URL);
    expect(startup.problems.map((problem) => problem.message)).toEqual([OcharoAppsProblem.Rejected]);
    expect(startup.problems[0]?.detail).toContain('VITE_OCHARO_DESK_URL');
  });

  it('reports each problem once through the app feedback without blocking', () => {
    const notify = vi.spyOn(feedbackBus, 'notify');
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    reportOcharoAppsStartup({
      addresses: {},
      brandApiUrl: null,
      problems: [{ message: OcharoAppsProblem.Unavailable, detail: 'GET /api/runtime-config failed: timeout.' }],
    });

    expect(notify).toHaveBeenCalledTimes(1);
    expect(notify).toHaveBeenCalledWith(
      expect.objectContaining({ tone: 'danger', description: OcharoAppsProblem.Unavailable }),
    );
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('timeout'));
  });
});
