import { defineComponent, h } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import { useAccess } from '@/application/workspace/UseAccess';
import WorkspacePage from '@/presentation/workspace/WorkspacePage.vue';

let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;

afterEach(() => {
  wrapper?.unmount();
  client?.clear();
  vi.unstubAllGlobals();
});

describe('published SDK workspace shell composition', () => {
  it.each([360, 1440])('keeps a sidebar-free shell and one reachable main landmark at %ipx', async (width) => {
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
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: [] })));
    client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
    wrapper = mount(
      defineComponent({
        setup() {
          const access = useAccess({ assign: vi.fn(), reload: vi.fn() });
          access.session.value = {
            authenticated: true,
            user: { id: 'operator', email: 'operator@example.test', displayName: 'Local verification operator' },
            sessionId: 'session',
            csrfToken: 'token',
            setupRequired: false,
          };
          return () => h(WorkspacePage, { access });
        },
      }),
      { global: { plugins: [queryPlugin(client)] } },
    );
    await flushPromises();

    const main = wrapper.get('main');
    expect(wrapper.findAll('main')).toHaveLength(1);
    const skipLinks = wrapper.findAll(`a[href="#${main.attributes('id')}"]`);
    expect(skipLinks).toHaveLength(1);
    expect(main.attributes('tabindex')).toBe('-1');
    const shell = wrapper.element as HTMLElement;
    // The SDK reserves a sidebar column only when a sidebar is composed; the content column stays floored.
    expect(shell.style.gridTemplate).toMatch(/\/ minmax\(0(px)?, 1fr\)$/);
    expect(wrapper.find('aside').exists()).toBe(false);

    // The SDK merges consumer classes; its fixed-height default must not survive a wrapping header.
    const header = wrapper.get('header');
    expect(header.classes()).toContain('h-auto');
    expect(header.classes()).not.toContain('h-14');
    expect(header.text()).toContain('Ocharo workspace');
    expect(header.text()).toContain('Local verification operator');
  });
});
