import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { DefaultRetryPolicy } from '@wow-two-beta/ui-vue/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/ui-vue/query';

import FoundationPage from '@/presentation/system/pages/FoundationPage.vue';

/** @internal The current fixture observation, without provider data. */
const waiting = {
  service: 'brand-workspace', stage: 'foundation', persistence: 'PostgreSQL',
  worker: { state: 'waiting', lastSeenAtUtc: null },
};

let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;

/** @internal Mounts the actual SDK query and component implementations. */
function mountPage(): VueWrapper {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  wrapper = mount(FoundationPage, {
    props: { organizationPreview: { planned: true, name: 'Example Brand', description: 'An explicitly planned account.' } },
    global: { plugins: [queryPlugin(client)] },
  });
  return wrapper;
}

afterEach(() => {
  wrapper?.unmount();
  client?.clear();
  vi.unstubAllGlobals();
});

describe('foundation status page', () => {
  it('shows loading before receiving a response, and exposes no business actions', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    const page = mountPage();
    expect(page.get('[role="status"]').text()).toContain('Checking the foundation');
    expect(page.findAll('button')).toHaveLength(1);
    expect(page.get('button').attributes('aria-busy')).toBe('true');
    expect(page.text()).toContain('First organization · planned');
    expect(page.text()).toContain('No brand data, social accounts, or publishing actions are available.');
    expect(page.findAll('a').map((link) => link.attributes('href'))).toEqual(['#main']);
  });

  it('renders the real waiting state without implying publishing readiness', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(waiting)));
    const page = mountPage();
    await flushPromises();
    expect(page.text()).toContain('Reachable');
    expect(page.text()).toContain('Waiting for first heartbeat');
    expect(page.text()).toContain('Configured datastore');
    expect(page.text()).toContain('PostgreSQL');
    expect(page.find('time').exists()).toBe(false);
  });

  it.each([
    ['healthy', 'Heartbeat received'],
    ['stale', 'Heartbeat overdue'],
  ])('renders a %s heartbeat as an observation with its timestamp', async (state, label) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({
      ...waiting, worker: { state, lastSeenAtUtc: '2026-09-26T08:30:00Z' },
    })));
    const page = mountPage();
    await flushPromises();
    expect(page.text()).toContain(label);
    expect(page.get('time').attributes('datetime')).toBe('2026-09-26T08:30:00Z');
  });

  it('shows a safe offline error and recovers through its real check action', async () => {
    const fetch = vi.fn().mockRejectedValueOnce(new Error('private connection details'))
      .mockResolvedValueOnce(Response.json(waiting));
    vi.stubGlobal('fetch', fetch);
    const page = mountPage();
    await flushPromises();
    expect(page.get('[role="alert"]').text()).toContain('could not be reached');
    expect(page.text()).not.toContain('private connection details');
    expect(page.text()).not.toContain('Reachable');
    await page.get('button').trigger('click');
    await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
    expect(page.text()).toContain('Waiting for first heartbeat');
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('replaces a previous healthy snapshot with uncertainty after a failed refresh', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(Response.json({
      ...waiting, worker: { state: 'healthy', lastSeenAtUtc: '2026-09-26T08:30:00Z' },
    })).mockRejectedValueOnce(new TypeError('offline'));
    vi.stubGlobal('fetch', fetch);
    const page = mountPage();
    await flushPromises();
    expect(page.text()).toContain('Heartbeat received');
    await page.get('button').trigger('click');
    await flushPromises();
    expect(page.get('[role="alert"]').text()).toContain('does not confirm the current state');
    expect(page.text()).not.toContain('Heartbeat received');
  });
});
