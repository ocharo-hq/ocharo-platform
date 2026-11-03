import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { vi } from 'vitest';
import type { Component, Plugin } from 'vue';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { loadOcharoApps, ocharoAppsPlugin } from '../../src/application/shared/UseOcharoApps';
import { identitySession } from '../../src/integration/common/IdentitySessionClient';
import AppRoot from '../../src/bootstrap/AppRoot.vue';
import { createBrandRouter } from '../../src/bootstrap/BrandRouter';

/** The session `/api/identity/session` serves by default: the organization's owner, who edits and approves. */
export const OwnerSession = {
  isAuthenticated: true,
  person: { sub: 'owner-sub', name: null, picture: null, email: null, phoneNumber: null },
  organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', name: 'Ocharo', role: 'owner', slug: 'ocharo' },
  permissions: ['brand:edit', 'brand:approve'],
  csrfToken: 'csrf-token',
  expiresAt: '2026-11-03T20:00:00Z',
};

/** Where the stubbed `/api/identity/sign-out` sends the browser next. */
export const EndSessionUrl = 'https://identity.example.test/connect/endsession';

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
  /** The session `/api/identity/session` serves; default {@link OwnerSession}. `/api/identity/sign-out` answers
   *  {@link EndSessionUrl}. */
  readonly identitySession?: Record<string, unknown>;
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
      if (pathname === '/api/identity/session')
        return Promise.resolve(Response.json({ data: options.identitySession ?? OwnerSession }));
      if (pathname === '/api/identity/sign-out')
        return Promise.resolve(Response.json({ data: { endSessionUrl: EndSessionUrl } }));
      return Promise.resolve(Response.json({ title: 'Not found' }, { status: 404 }));
    }),
  );
  // Startup resolves the session before mounting; so does every test.
  await identitySession.initialize(true);
  const router = createBrandRouter({ history: RouterHistoryMode.Memory });
  await router.push(path);
  await router.isReady();
  const plugins: Plugin[] = [router];
  if (options.runtimeConfig) plugins.push(ocharoAppsPlugin(await loadOcharoApps()));
  const wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins, stubs: options.stubs ?? {} } });
  await flushPromises();
  return { wrapper, router };
}
