import { defineComponent, h } from 'vue';
import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ConfigError, staticSource } from '@wow-two-beta/fe/foundation/config';

import { feedbackBus } from '@/application/shared/UseFeedback';
import {
  OcharoAppsProblem,
  loadOcharoApps,
  reportOcharoAppsStartup,
  resolveOcharoAppsConfig,
  runtimeConfigSource,
  useOcharoApps,
} from '@/application/shared/UseOcharoApps';
import { ocharoApps, type OcharoApp } from '@/domain/shared/OcharoApps';

/** The local-run addresses Desk falls back to when nothing configures an app. */
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
};

/** Answers `/api/runtime-config` with `body`, and records every request. */
function serveRuntimeConfig(body: unknown | (() => Promise<Response>)) {
  const fetch = vi.fn().mockImplementation(() =>
    typeof body === 'function' ? (body as () => Promise<Response>)() : Promise.resolve(Response.json(body)));
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

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
      runtimeConfigSource({ OCHARO_CATALOGUE_URL: 'https://catalogue.example.test/catalogues' }),
      staticSource({ VITE_OCHARO_CATALOGUE_URL: 'https://build.example.test/catalogues', VITE_OCHARO_BLOG_URL: 'https://blog.example.test/' }),
    ]);

    expect(config.OCHARO_CATALOGUE_URL).toBe('https://catalogue.example.test/catalogues');
    expect(config.OCHARO_BLOG_URL).toBe('https://blog.example.test/');
    expect(config.OCHARO_DESK_URL).toBe(LocalDefaults.OCHARO_DESK_URL);
  });

  it('rejects a malformed runtime value instead of falling back to the build-time one', () => {
    const resolve = () => resolveOcharoAppsConfig([
      runtimeConfigSource({ OCHARO_CATALOGUE_URL: 'not a link' }),
      staticSource({ VITE_OCHARO_CATALOGUE_URL: 'https://build.example.test/catalogues' }),
    ]);

    expect(resolve).toThrow(ConfigError);
    try {
      resolve();
    } catch (error) {
      expect((error as ConfigError).issues).toEqual([
        expect.objectContaining({ key: 'OCHARO_CATALOGUE_URL', lookupKey: 'VITE_OCHARO_CATALOGUE_URL', reason: 'invalid' }),
      ]);
    }
  });

  it('keeps the menu to plain HTTPS links even when a configured value is a well-formed URL', () => {
    const config = resolveOcharoAppsConfig([
      runtimeConfigSource({ OCHARO_STUDIO_URL: 'http://studio.example.test/', OCHARO_BRAND_URL: 'https://brand.example.test/?token=x' }),
    ]);
    const keys = ocharoApps({ studio: config.OCHARO_STUDIO_URL, brand: config.OCHARO_BRAND_URL, desk: config.OCHARO_DESK_URL }).map((app) => app.key);

    expect(keys).toEqual(['desk']);
  });
});

describe('startup resolution', () => {
  it('reads the public settings once, anonymously, and applies them ahead of the defaults', async () => {
    const fetch = serveRuntimeConfig({ OCHARO_CATALOGUE_URL: 'https://catalogue.example.test/catalogues', UNRELATED: 'ignored' });

    const startup = await loadOcharoApps();

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledWith('/api/runtime-config', expect.objectContaining({ method: 'GET', credentials: 'omit' }));
    expect(startup.problems).toEqual([]);
    expect(startup.addresses).toMatchObject({ catalogue: 'https://catalogue.example.test/catalogues', desk: LocalDefaults.OCHARO_DESK_URL });
    expect(ocharoApps(startup.addresses)).toHaveLength(9);
  });

  it('keeps reading the existing VITE_OCHARO_* build variables behind the served settings', async () => {
    serveRuntimeConfig({ OCHARO_CATALOGUE_URL: 'https://catalogue.example.test/catalogues' });
    vi.stubEnv('VITE_OCHARO_WEAR_URL', 'https://wear.example.test/');
    vi.stubEnv('VITE_OCHARO_CATALOGUE_URL', 'https://build.example.test/catalogues');

    const startup = await loadOcharoApps();

    expect(startup.addresses.wear).toBe('https://wear.example.test/');
    expect(startup.addresses.catalogue).toBe('https://catalogue.example.test/catalogues');
    expect(startup.problems).toEqual([]);
  });

  it('reads the build variables in a component mounted without the startup resolution', () => {
    vi.stubEnv('VITE_OCHARO_STUDIO_URL', 'https://studio.example.test/studio');
    let apps: OcharoApp[] = [];
    mount(defineComponent({ setup() { apps = useOcharoApps(); return () => h('div'); } })).unmount();

    expect(apps.find((app) => app.key === 'studio')?.url).toBe('https://studio.example.test/studio');
    expect(apps.find((app) => app.key === 'desk')?.url).toBe(LocalDefaults.OCHARO_DESK_URL);
  });

  it.each([
    ['offline', () => Promise.reject(new TypeError('offline'))],
    ['server error', () => Promise.resolve(Response.json({ title: 'Unavailable' }, { status: 503 }))],
    ['HTML fallback', () => Promise.resolve(new Response('<html></html>', { headers: { 'Content-Type': 'text/html' } }))],
    ['non-object body', () => Promise.resolve(Response.json(['https://catalogue.example.test/']))],
  ])('continues with the build-time values and reports an unreadable response (%s)', async (_label, respond) => {
    serveRuntimeConfig(respond);
    vi.stubEnv('VITE_OCHARO_CATALOGUE_URL', 'https://build.example.test/catalogues');

    const startup = await loadOcharoApps();

    expect(startup.addresses.catalogue).toBe('https://build.example.test/catalogues');
    expect(startup.problems.map((problem) => problem.message)).toEqual([OcharoAppsProblem.Unavailable]);
  });

  it('leaves out an app whose served value is malformed and reports it, keeping every other address', async () => {
    serveRuntimeConfig({ OCHARO_CATALOGUE_URL: 'not a link', OCHARO_ATLAS_URL: 42, OCHARO_BLOG_URL: 'https://blog.example.test/' });
    vi.stubEnv('VITE_OCHARO_CATALOGUE_URL', 'https://build.example.test/catalogues');

    const startup = await loadOcharoApps();

    expect(startup.addresses.catalogue).toBeUndefined();
    expect(startup.addresses.atlas).toBeUndefined();
    expect(startup.addresses.blog).toBe('https://blog.example.test/');
    expect(startup.addresses.desk).toBe(LocalDefaults.OCHARO_DESK_URL);
    expect(startup.problems.map((problem) => problem.message)).toEqual([OcharoAppsProblem.Rejected]);
    expect(startup.problems[0]?.detail).toContain('VITE_OCHARO_CATALOGUE_URL');
  });

  it('reports each problem once through the app feedback without blocking', () => {
    const notify = vi.spyOn(feedbackBus, 'notify');
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    reportOcharoAppsStartup({ addresses: {}, problems: [{ message: OcharoAppsProblem.Unavailable, detail: 'GET /api/runtime-config failed: timeout.' }] });

    expect(notify).toHaveBeenCalledTimes(1);
    expect(notify).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger', description: OcharoAppsProblem.Unavailable }));
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('timeout'));
  });
});
