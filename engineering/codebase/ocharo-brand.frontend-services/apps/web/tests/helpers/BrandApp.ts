import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { vi } from 'vitest';
import { h, type Component, type Plugin } from 'vue';
import { AuthProvider } from '@wow-two-beta/fe/auth';
import { queryPlugin } from '@wow-two-beta/fe/query';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { useBrand } from '../../src/application/brand';
import { loadOcharoApps, ocharoAppsPlugin } from '../../src/application/shared/UseOcharoApps';
import AppRoot from '../../src/bootstrap/AppRoot.vue';
import { createBrandQueryClient } from '../../src/bootstrap/BrandQueryClient';
import { createBrandRouter } from '../../src/bootstrap/BrandRouter';
import { identityBridge, identityStrategy } from '../../src/integration/common/IdentitySession';
import BrandProbe from './BrandProbe.vue';
import SettledSession from './SettledSession.vue';

/** The session `/api/identity/session` serves by default: the organization's owner, who edits and approves. */
export const OwnerSession = {
  isAuthenticated: true,
  person: { sub: 'owner-sub', name: null, picture: null, email: null, phoneNumber: null },
  organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', name: 'Ocharo', role: 'owner', slug: 'ocharo' },
  permissions: ['brand:edit', 'brand:approve'],
  csrfToken: 'csrf-token',
  expiresAt: '2099-01-01T00:00:00Z',
};

/** The session route's answer without a session. */
export const SignedOutSession = { isAuthenticated: false };

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

export interface BrandHostOptions {
  /** When given, `/api/runtime-config` serves it and the app menu resolves it the way startup does. */
  readonly runtimeConfig?: Record<string, string>;
  /** The session `/api/identity/session` serves; default {@link OwnerSession}. `/api/identity/sign-out` answers
   *  {@link EndSessionUrl}. */
  readonly identitySession?: Record<string, unknown>;
}

/** Stubs the host's own routes: runtime settings, status, the session and sign-out. Brand reads are mocked by callers. */
export function stubBrandHost(options: BrandHostOptions = {}): void {
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
}

export interface BrandAppOptions extends BrandHostOptions {
  /** The viewport width; default a 1440px desktop. */
  readonly width?: number;
  readonly stubs?: Record<string, Component>;
}

/**
 * Mounts the whole app — the root with its session provider, the real route table, layout and pages — then opens
 * `path` once the session settles, as startup does. Brand reads are mocked by callers.
 */
export async function mountBrand(
  path: string,
  options: BrandAppOptions = {},
): Promise<{ wrapper: VueWrapper; router: ReturnType<typeof createBrandRouter> }> {
  stubViewport(options.width ?? 1440);
  stubBrandHost(options);
  const router = createBrandRouter({ history: RouterHistoryMode.Memory });
  const plugins: Plugin[] = [router, queryPlugin(createBrandQueryClient())];
  if (options.runtimeConfig) plugins.push(ocharoAppsPlugin(await loadOcharoApps()));
  const wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins, stubs: options.stubs ?? {} } });
  await router.replace(path);
  await router.isReady();
  await flushPromises();
  return { wrapper, router };
}

/**
 * Mounts `content` (or nothing) under the session provider and the session-bound query client, as the frame does,
 * beside a probe that hands back the shared brand store. Brand reads are mocked by callers.
 */
export async function mountInBrand(
  content?: Component,
  options: BrandHostOptions = {},
): Promise<{ wrapper: VueWrapper; brand: ReturnType<typeof useBrand> }> {
  stubBrandHost(options);
  let brand: ReturnType<typeof useBrand> | undefined;
  const receive = (store: ReturnType<typeof useBrand>) => {
    brand = store;
  };
  const router = createBrandRouter({ history: RouterHistoryMode.Memory });
  const wrapper = mount(AuthProvider as unknown as Component, {
    attachTo: document.body,
    props: { strategy: identityStrategy, bridge: identityBridge },
    slots: { default: () => h(SettledSession, null, () => [h(BrandProbe, { receive }), content ? h(content) : null]) },
    global: { plugins: [router, queryPlugin(createBrandQueryClient())] },
  });
  await flushPromises();
  if (!brand) throw new Error('The session did not settle.');
  return { wrapper, brand };
}
