import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { vi } from 'vitest';
import type { Component, Plugin } from 'vue';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { loadOcharoApps, ocharoAppsPlugin } from '../../src/application/shared/UseOcharoApps';
import AppRoot from '../../src/bootstrap/AppRoot.vue';
import { createBrandRouter } from '../../src/bootstrap/BrandRouter';

/** The informational version the stubbed `/api/system/status` reports: a release and the commit it was built from. */
export const ApiVersion = '0.1.4+8f2c1d9e4b7a';

/** Answers every `min-width` media query for a viewport `width` wide; colour-scheme queries read as light. */
export function stubViewport(width: number): void {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: width >= Number(query.match(/min-width:\s*(\d+)px/)?.[1] ?? Infinity),
    media: query,
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
}

export interface BrandAppOptions {
  /** The viewport width; default a 1440px desktop. */
  readonly width?: number;
  /** When given, `/api/runtime-config` serves it and the app menu resolves it the way startup does. */
  readonly runtimeConfig?: Record<string, string>;
  /** The session `/api/sso/session` serves; default SSO disabled. `/api/sso/logout` answers 204. */
  readonly ssoSession?: Record<string, unknown>;
  readonly stubs?: Record<string, Component>;
}

/** Mounts the whole app — the real route table, layout and pages — at `path`. Brand reads are mocked by callers. */
export async function mountBrand(
  path: string,
  options: BrandAppOptions = {},
): Promise<{ wrapper: VueWrapper; router: ReturnType<typeof createBrandRouter> }> {
  stubViewport(options.width ?? 1440);
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input instanceof Request ? input.url : input);
      const pathname = new URL(url, 'https://localhost').pathname;
      if (pathname === '/api/runtime-config') return Promise.resolve(Response.json(options.runtimeConfig ?? {}));
      if (pathname === '/api/system/status')
        return Promise.resolve(Response.json({ service: 'OcharoBrand', status: 'live', version: ApiVersion }));
      if (pathname === '/api/sso/session')
        return Promise.resolve(Response.json({ data: options.ssoSession ?? { enabled: false, authenticated: false } }));
      if (pathname === '/api/sso/logout') return Promise.resolve(new Response(null, { status: 204 }));
      return Promise.resolve(Response.json({ title: 'Not found' }, { status: 404 }));
    }),
  );
  const router = createBrandRouter({ history: RouterHistoryMode.Memory });
  await router.push(path);
  await router.isReady();
  const plugins: Plugin[] = [router];
  if (options.runtimeConfig) plugins.push(ocharoAppsPlugin(await loadOcharoApps()));
  const wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins, stubs: options.stubs ?? {} } });
  await flushPromises();
  return { wrapper, router };
}
