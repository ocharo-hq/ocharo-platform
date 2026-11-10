import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { vi } from 'vitest';
import type { Component, Plugin } from 'vue';
import { queryPlugin } from '@wow-two-beta/fe/query';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { loadOcharoApps, ocharoAppsPlugin } from '../../src/application/shared/UseOcharoApps';
import AppRoot from '../../src/bootstrap/AppRoot.vue';
import { createEventsQueryClient } from '../../src/bootstrap/EventsQueryClient';
import { createEventsRouter } from '../../src/bootstrap/EventsRouter';

/** The organization of the stubbed session. */
export const OrganizationId = 'b8c672a6-c2e4-4516-82ad-5b178e71ace1';

/** The session `/api/identity/session` serves by default: an editor of Ocharo Atelier, who reads and tracks events. */
export const EditorSession = {
  isAuthenticated: true,
  person: {
    sub: 'editor-sub',
    name: 'Dilnoza Karimova',
    picture: null,
    email: 'dilnoza@example.test',
    phoneNumber: null,
  },
  organization: { id: OrganizationId, name: 'Ocharo Atelier', role: 'editor', slug: 'ocharo-atelier' },
  permissions: ['events:read', 'events:track'],
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

export interface EventsHostOptions {
  /** When given, `/api/runtime-config` serves it and the app menu resolves it the way startup does. */
  readonly runtimeConfig?: Record<string, string>;
  /** The session `/api/identity/session` serves; default {@link EditorSession}. */
  readonly identitySession?: Record<string, unknown>;
}

/**
 * Stubs the host's own routes (runtime settings, status, the session and sign-out) and the events routes, which answer
 * an empty feed and an empty summary.
 */
export function stubEventsHost(options: EventsHostOptions = {}): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input instanceof Request ? input.url : input);
      const pathname = new URL(url, 'https://localhost').pathname;
      if (pathname === '/api/runtime-config') return Promise.resolve(Response.json(options.runtimeConfig ?? {}));
      if (pathname === '/api/system/status')
        return Promise.resolve(Response.json({ service: 'OcharoEvents', status: 'live', version: ApiVersion }));
      if (pathname === '/api/identity/session')
        return Promise.resolve(Response.json({ data: options.identitySession ?? EditorSession }));
      if (pathname === '/api/identity/sign-out')
        return Promise.resolve(Response.json({ data: { endSessionUrl: EndSessionUrl } }));
      if (/^\/api\/organizations\/[^/]+\/events\/summary$/.test(pathname))
        return Promise.resolve(Response.json({ data: { byStatus: {}, upcomingDeadlines: [] } }));
      if (/^\/api\/organizations\/[^/]+\/events$/.test(pathname)) return Promise.resolve(Response.json({ data: [] }));
      return Promise.resolve(Response.json({ title: 'Not found' }, { status: 404 }));
    }),
  );
}

export interface EventsAppOptions extends EventsHostOptions {
  /** The viewport width; default a 1440px desktop. */
  readonly width?: number;
  readonly stubs?: Record<string, Component>;
}

/**
 * Mounts the whole app — the root with its session provider, the real route table, layout and pages — then opens
 * `path` once the session settles, as startup does.
 */
export async function mountEvents(
  path: string,
  options: EventsAppOptions = {},
): Promise<{ wrapper: VueWrapper; router: ReturnType<typeof createEventsRouter> }> {
  stubViewport(options.width ?? 1440);
  stubEventsHost(options);
  const router = createEventsRouter({ history: RouterHistoryMode.Memory });
  const plugins: Plugin[] = [router, queryPlugin(createEventsQueryClient())];
  if (options.runtimeConfig) plugins.push(ocharoAppsPlugin(await loadOcharoApps()));
  const wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins, stubs: options.stubs ?? {} } });
  await router.replace(path);
  await router.isReady();
  await flushPromises();
  return { wrapper, router };
}
