import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { vi } from 'vitest';
import type { Component, Plugin } from 'vue';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { loadOcharoApps, ocharoAppsPlugin } from '../../src/application/shared/UseOcharoApps';
import AppRoot from '../../src/bootstrap/AppRoot.vue';
import { createCatalogueRouter, isPublicReaderPath } from '../../src/bootstrap/CatalogueRouter';
import { OwnerSessionData } from './IdentitySessions';

/** The Identity end-session address the stubbed `/api/identity/sign-out` answers. */
export const EndSessionUrl = 'https://identity.example.test/connect/endsession?client_id=catalogue';

/** The informational version the stubbed `/api/system/status` reports: a release and the commit it was built from. */
export const ApiVersion = '0.1.4+8f2c1d9e4b7a';

/** Replaces only the popup select interaction; the real shell, pages and SDK overlays stay. */
export const SelectStub = {
  props: ['id', 'modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :id="id" :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\',$event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};

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

export interface CatalogueHostOptions {
  /** When given, `/api/runtime-config` serves it and the app menu resolves it the way startup does. */
  readonly runtimeConfig?: Record<string, string>;
  /** The session `/api/identity/session` serves; default an owner's. `/api/identity/sign-out` answers
   * {@link EndSessionUrl}. */
  readonly session?: Record<string, unknown>;
}

/** Stubs the host's own routes: runtime settings, status, the session and sign-out. Catalogue reads are mocked by callers. */
export function stubCatalogueHost(options: CatalogueHostOptions = {}): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input instanceof Request ? input.url : input);
      const pathname = new URL(url, 'https://localhost').pathname;
      if (pathname === '/api/runtime-config') return Promise.resolve(Response.json(options.runtimeConfig ?? {}));
      if (pathname === '/api/system/status')
        return Promise.resolve(Response.json({ service: 'OcharoCatalogue', status: 'live', version: ApiVersion }));
      if (pathname === '/api/identity/session')
        return Promise.resolve(Response.json({ data: options.session ?? OwnerSessionData }));
      if (pathname === '/api/identity/sign-out')
        return Promise.resolve(Response.json({ data: { endSessionUrl: EndSessionUrl } }));
      return Promise.resolve(Response.json({ title: 'Not found' }, { status: 404 }));
    }),
  );
}

export interface CatalogueAppOptions extends CatalogueHostOptions {
  /** The viewport width; default a 1440px desktop. */
  readonly width?: number;
  readonly stubs?: Record<string, Component>;
}

/**
 * Mounts the whole app — the root with its session provider, the real route table, layouts and pages — then opens
 * `path` once mounted, as startup does: the frame's guard waits for the session. Catalogue reads are mocked by callers.
 */
export async function mountCatalogue(
  path: string,
  options: CatalogueAppOptions = {},
): Promise<{ wrapper: VueWrapper; router: ReturnType<typeof createCatalogueRouter> }> {
  stubViewport(options.width ?? 1440);
  stubCatalogueHost(options);
  const router = createCatalogueRouter({ history: RouterHistoryMode.Memory });
  const plugins: Plugin[] = [router];
  if (options.runtimeConfig) plugins.push(ocharoAppsPlugin(await loadOcharoApps()));
  const wrapper = mount(AppRoot, {
    attachTo: document.body,
    props: { opensOnReader: isPublicReaderPath(path) },
    global: { plugins, stubs: options.stubs ?? {} },
  });
  await router.replace(path);
  await router.isReady();
  await flushPromises();
  return { wrapper, router };
}
