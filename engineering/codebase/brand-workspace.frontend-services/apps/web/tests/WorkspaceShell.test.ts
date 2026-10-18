import { defineComponent, h } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import { useAccess } from '@/application/workspace/UseAccess';
import WorkspacePage from '@/presentation/workspace/WorkspacePage.vue';

import { deskRouter } from './helpers/DeskRouter';

const ORG = '11111111-1111-4111-8111-111111111111';
let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;

afterEach(() => {
  wrapper?.unmount();
  client?.clear();
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

/** Mounts the workspace at `path` for a viewport `width` wide, with one organization the operator owns. */
async function renderAt(path: string, width: number) {
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
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input instanceof Request ? input.url : input);
      const organizations = [{ id: ORG, name: 'Ocharo', slug: 'ocharo', role: 'owner' }];
      return Promise.resolve(Response.json({ data: new URL(url, 'https://localhost').pathname === '/api/organizations' ? organizations : [] }));
    }),
  );
  window.history.replaceState(null, '', path);
  const router = await deskRouter();
  const assign = vi.fn();
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  wrapper = mount(
    defineComponent({
      setup() {
        const access = useAccess({ assign, reload: vi.fn(), router });
        access.session.value = {
          authenticated: true,
          user: { id: 'operator', email: 'operator@example.test', displayName: 'Local verification operator' },
          sessionId: 'session',
          csrfToken: 'token',
          setupRequired: false,
        };
        return () => h(ColorModeProvider, { storageKey: 'ocharo.desk.theme.test' }, () => h(WorkspacePage, { access }));
      },
    }),
    { global: { plugins: [queryPlugin(client), router] }, attachTo: document.body },
  );
  await flushPromises();
  return { router, assign };
}

describe('Ocharo frame in Desk', () => {
  it.each([360, 1440])('keeps one reachable main landmark and the suite controls at %ipx', async (width) => {
    await renderAt(`/organizations/${ORG}/brands`, width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.findAll(`a[href="#${main.attributes('id')}"]`)).toHaveLength(1);
    expect(main.attributes('tabindex')).toBe('-1');
    const header = wrapper!.get('header');
    expect(header.find('button[aria-label="Ocharo apps — Desk"]').exists()).toBe(true);
    expect(header.find('button[aria-label="Account menu for Local verification operator"]').exists()).toBe(true);
  });

  it('lists every section in the sidebar and marks the open one', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    const nav = wrapper!.get('nav[aria-label="Desk sections"]');
    const links = nav.findAll('a').map((link) => link.attributes('href'));
    expect(links).toContain(`/organizations/${ORG}/events`);
    expect(links).toContain(`/organizations/${ORG}/sourcing`);
    expect(links).toContain('/system');
    expect(nav.get('[aria-current="page"]').text()).toBe('Brands');
  });

  it('moves between sections without replacing the document', async () => {
    const { router, assign } = await renderAt(`/organizations/${ORG}/brands`, 1440);

    await router.push(`/organizations/${ORG}/media`);
    await flushPromises();

    expect(window.location.pathname).toBe(`/organizations/${ORG}/media`);
    expect(wrapper!.get('nav[aria-label="Desk sections"] [aria-current="page"]').text()).toBe('Media');
    expect(assign).not.toHaveBeenCalled();
  });
});
