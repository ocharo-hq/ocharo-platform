import { mount, flushPromises } from '@vue/test-utils';
import { nextTick } from 'vue';
import { createMemoryHistory, createRouter } from 'vue-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DraftView } from '../src/domain/brand/BrandModels';

const api = vi.hoisted(() => ({
  draft: vi.fn(),
  releases: vi.fn(),
  appearances: vi.fn(),
  consumers: vi.fn(),
  publicKit: vi.fn(),
  saveIdentity: vi.fn(),
}));
vi.mock('../src/integration/brand', () => ({
  brandApi: api,
  brandUrls: {
    logo: (slot: string) => `/logo/${slot}`,
    media: (sha256: string) => `/media/${sha256}`,
    kit: '/kit',
    tokens: '/tokens',
  },
  describeFailure: () => 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi.',
}));

function draft(name = 'Ocharo', revision = 1): DraftView {
  return {
    revision,
    draft: {
      identity: {
        name,
        spellingRule: null,
        descriptor: { uz: 'Tavsif', ru: null, en: null },
        tagline: { uz: null, ru: null, en: null },
        location: { uz: null, ru: null, en: null },
        palette: [],
        typography: { display: 'Georgia', body: 'Arial', notes: null },
        links: [],
        voiceNotes: null,
      },
      currentLogos: {},
    },
    updatedAt: '2026-10-02T00:00:00Z',
    logos: [],
    latestRelease: null,
    unpublishedChanges: [],
  };
}
const failed = { ok: false, failure: { code: 'transport' } };

beforeEach(() => {
  vi.resetModules();
  vi.useFakeTimers();
  vi.clearAllMocks();
  api.draft.mockResolvedValue({ ok: true, value: draft() });
  api.releases.mockResolvedValue({ ok: true, value: [] });
  api.appearances.mockResolvedValue({ ok: true, value: [] });
  api.consumers.mockResolvedValue({ ok: true, value: [] });
  api.publicKit.mockResolvedValue(failed);
  api.saveIdentity.mockResolvedValue({ ok: true, value: draft() });
});
afterEach(() => {
  vi.useRealTimers();
});

async function settle(read: Promise<void>) {
  await flushPromises();
  await vi.advanceTimersByTimeAsync(400);
  await read;
  await nextTick();
}

describe('Brand read outcomes', () => {
  it('distinguishes a failed first administrative read from a successful empty collection', async () => {
    api.releases.mockResolvedValue(failed);
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    expect(brand.queryState.value).toBe('unavailable');
    expect(brand.draft.value).toBeNull();
    expect(brand.refreshing.value).toBe(false);
    expect(brand.loaded.value).toBe(true);
    expect(brand.error.value).toContain('So‘rov bajarilmadi');
  });

  it('coalesces refreshes and preserves the complete last snapshot if a read fails', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    const first = brand.refresh();
    expect(brand.refresh()).toBe(first);
    await settle(first);
    const lastGood = brand.draft.value;
    expect(brand.queryState.value).toBe('success');
    expect(api.draft).toHaveBeenCalledTimes(1);
    api.draft.mockResolvedValue({ ok: true, value: draft('New server identity', 2) });
    api.consumers.mockResolvedValue(failed);
    const refresh = brand.refresh();
    await flushPromises();
    expect(brand.refreshing.value).toBe(true);
    expect(brand.draft.value).toBe(lastGood);
    await settle(refresh);
    expect(brand.draft.value).toBe(lastGood);
    expect(brand.queryState.value).toBe('success');
  });
});

describe('Brand acknowledged projection', () => {
  it('starts a new read after a save when an older refresh is already running', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    const olderRead = brand.refresh();
    await flushPromises();
    const identity = structuredClone(draft('Saved identity').draft.identity);
    const saved = brand.saveIdentity(identity, draft().revision);
    await flushPromises();
    api.draft.mockResolvedValue({ ok: true, value: draft('Saved identity', 2) });
    await settle(olderRead);
    await vi.advanceTimersByTimeAsync(400);
    expect(await saved).toMatchObject({ revision: draft().revision });
    expect(api.draft).toHaveBeenCalledTimes(3);
    expect(brand.draft.value?.draft.identity.name).toBe('Saved identity');
  });
});

describe('Brand command lock', () => {
  it('blocks overlapping commands and user refresh until the acknowledged read settles', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    const identity = structuredClone(draft('Saved identity').draft.identity);
    const saved = brand.saveIdentity(identity, draft().revision);
    await flushPromises();
    expect(brand.mutating.value).toBe(true);
    expect(brand.refreshing.value).toBe(true);
    expect(brand.busy.value).toBe(true);
    expect(await brand.saveIdentity(identity, draft().revision)).toBeNull();
    expect(api.saveIdentity).toHaveBeenCalledTimes(1);
    const reads = api.draft.mock.calls.length;
    await brand.refresh();
    expect(api.draft).toHaveBeenCalledTimes(reads);
    await vi.advanceTimersByTimeAsync(400);
    expect(await saved).toMatchObject({ revision: draft().revision });
    expect(brand.mutating.value).toBe(false);
    expect(brand.busy.value).toBe(false);
  });
});

describe('Identity query rendering', () => {
  it('keeps its routed editor mounted and refresh in the toolbar through failure and recovery', async () => {
    api.draft.mockResolvedValue(failed);
    const { default: IdentityPage } = await import('../src/presentation/brand/IdentityPage.vue');
    const { default: AppRoot } = await import('../src/bootstrap/AppRoot.vue');
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/identity', component: IdentityPage },
        { path: '/:pathMatch(.*)*', component: IdentityPage },
      ],
    });
    await router.push('/identity');
    await router.isReady();
    const wrapper = mount(AppRoot, {
      global: {
        plugins: [router],
        stubs: {
          BuildIdentity: true,
          AppErrorBoundary: { template: '<div><slot /></div>' },
          FeedbackToastHost: true,
          RouteAnnouncer: true,
        },
      },
    });
    expect(wrapper.findComponent({ name: 'IdentityFormSkeleton' }).exists()).toBe(true);
    expect(wrapper.findAll('h2').map((heading) => heading.text())).toContain('Rasmiy havolalar');
    await settle(brand.refresh());
    expect(wrapper.text()).toContain('Identitet ma’lumotlari hozir mavjud emas.');
    expect(wrapper.text()).not.toContain('Hali identitet ma’lumotlari yo‘q.');
    expect(
      wrapper
        .find('main')
        .findAll('button')
        .filter((button) => button.text().includes('Qayta urinish')),
    ).toHaveLength(0);
    expect(wrapper.find('header').text()).toContain('Yangilash');
    api.draft.mockResolvedValue({ ok: true, value: draft() });
    await settle(brand.refresh());
    const nameInput = wrapper.get('#name').element;
    const refresh = brand.refresh();
    await nextTick();
    expect(wrapper.get('#name').element).toBe(nameInput);
    expect(wrapper.get('#name').isVisible()).toBe(true);
    expect(wrapper.find('main').find('button').attributes('disabled')).toBeDefined();
    expect(wrapper.find('header').text()).toContain('Yangilash');
    await settle(refresh);
    wrapper.unmount();
  });

  it('retains edited fields when refreshed server identity changes', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    const { default: IdentityPage } = await import('../src/presentation/brand/IdentityPage.vue');
    const wrapper = mount(IdentityPage);
    await wrapper.get('#name').setValue('Unsaved local name');
    api.draft.mockResolvedValue({ ok: true, value: draft('Changed server name', 2) });
    await settle(brand.refresh());
    expect((wrapper.get('#name').element as HTMLInputElement).value).toBe('Unsaved local name');
    expect(wrapper.find('button').attributes('disabled')).toBeUndefined();
    wrapper.unmount();
  });

  it('keeps the baseline revision after dirty refresh so intervening server changes conflict', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    const { default: IdentityPage } = await import('../src/presentation/brand/IdentityPage.vue');
    const wrapper = mount(IdentityPage);
    await wrapper.get('#name').setValue('Unsaved local name');
    api.draft.mockResolvedValue({ ok: true, value: draft('Intervening server name', 2) });
    await settle(brand.refresh());
    expect(brand.draft.value?.revision).toBe(2);
    api.saveIdentity.mockResolvedValue({ ok: false, failure: { code: 'http', status: 409 } });
    await wrapper.find('button').trigger('click');
    await flushPromises();
    expect(api.saveIdentity.mock.calls[0]?.[0]).toBe(1);
    expect(api.saveIdentity.mock.calls[0]?.[1].name).toBe('Unsaved local name');
    expect((wrapper.get('#name').element as HTMLInputElement).value).toBe('Unsaved local name');
    expect(brand.error.value).toContain('So‘rov bajarilmadi');
    expect(brand.draft.value?.draft.identity.name).toBe('Intervening server name');
    wrapper.unmount();
  });

  it('acknowledges only the submitted values when editing continues during save', async () => {
    const { useBrand } = await import('../src/application/brand');
    const brand = useBrand();
    await settle(brand.refresh());
    const { default: IdentityPage } = await import('../src/presentation/brand/IdentityPage.vue');
    const wrapper = mount(IdentityPage);
    await wrapper.get('#name').setValue('Submitted name');
    await wrapper.find('button').trigger('click');
    await flushPromises();
    await wrapper.get('#name').setValue('Later unsaved name');
    await vi.advanceTimersByTimeAsync(400);
    await flushPromises();
    expect(api.saveIdentity.mock.calls[0]?.[1].name).toBe('Submitted name');
    expect((wrapper.get('#name').element as HTMLInputElement).value).toBe('Later unsaved name');
    expect(wrapper.find('button').attributes('disabled')).toBeUndefined();
    wrapper.unmount();
  });
});

describe('Brand initial route geometry', () => {
  it('matches the six logo sections, triptych previews and responsive version grids', async () => {
    const { useBrand } = await import('../src/application/brand');
    const { slots } = await import('../src/domain/brand/BrandModels');
    const value = draft();
    value.logos = Array.from({ length: 3 }, (_, index) => ({
      id: `logo-${index}`,
      slot: 'master',
      sha256: `hash-${index}`,
      fileName: `logo-${index}.png`,
      contentType: 'image/png',
      byteLength: 100,
      width: 100,
      height: 100,
      hasPreview: true,
      status: 'approved',
      note: null,
      sourcePath: null,
      uploadedAt: value.updatedAt,
    }));
    value.draft.currentLogos.master = 'logo-0';
    api.draft.mockResolvedValue({ ok: true, value });
    await settle(useBrand().refresh());
    const { default: LogosPage } = await import('../src/presentation/brand/LogosPage.vue');
    const { default: BrandQueryState } = await import('../src/presentation/common/BrandQueryState.vue');
    const loaded = mount(LogosPage);
    const loading = mount(BrandQueryState, { props: { state: 'loading', kind: 'logos' } });
    expect(loading.findAll('section[aria-labelledby^="loading-slot-"]')).toHaveLength(
      loaded.findAll('section.panel').length,
    );
    expect(
      loading
        .findAll('section[aria-labelledby^="loading-slot-"]')
        .map((section) => section.attributes('aria-labelledby')?.replace('loading-slot-', '')),
    ).toEqual(slots);
    const actual = loaded.get('section.panel');
    const placeholder = loading.get('section[aria-labelledby="loading-slot-master"]');
    expect(placeholder.get('.grid-cols-3').element.children).toHaveLength(actual.findAll('[class*="aspect-"]').length);
    expect(placeholder.get('ul').classes()).toEqual(actual.get('ul').classes());
    expect(placeholder.findAll('li')).toHaveLength(actual.findAll('li').length);
    loaded.unmount();
    loading.unmount();
  });

  it('matches the overview consumer grid and ordered release rows', async () => {
    const { useBrand } = await import('../src/application/brand');
    api.consumers.mockResolvedValue({
      ok: true,
      value: Array.from({ length: 3 }, (_, index) => ({
        app: `App ${index}`,
        lastSeenAt: draft().updatedAt,
        lastRelease: 1,
        current: true,
        lastPath: '/',
      })),
    });
    api.releases.mockResolvedValue({
      ok: true,
      value: Array.from({ length: 2 }, (_, index) => ({
        version: index + 1,
        publishedAt: draft().updatedAt,
        note: 'Release',
        changes: ['Identity'],
      })),
    });
    await settle(useBrand().refresh());
    const { default: OverviewPage } = await import('../src/presentation/brand/OverviewPage.vue');
    const { default: BrandQueryState } = await import('../src/presentation/common/BrandQueryState.vue');
    const loaded = mount(OverviewPage);
    const loading = mount(BrandQueryState, { props: { state: 'loading', kind: 'overview' } });
    const consumers = loaded.get('[aria-labelledby="consumers-heading"] ul');
    const releases = loaded.get('[aria-labelledby="releases-heading"] ol');
    const consumerSkeleton = loading.get('[aria-label="Brendni o‘qiyotgan ilovalar"] ul');
    const releaseSkeleton = loading.get('[aria-label="Nashrlar tarixi"] ol');
    expect(consumerSkeleton.classes()).toEqual(consumers.classes());
    expect(consumerSkeleton.findAll('li')).toHaveLength(consumers.findAll('li').length);
    expect(releaseSkeleton.element.tagName).toBe(releases.element.tagName);
    expect(releaseSkeleton.classes()).toEqual(releases.classes());
    expect(releaseSkeleton.findAll('li')).toHaveLength(releases.findAll('li').length);
    expect(loading.find('table').exists()).toBe(false);
    loaded.unmount();
    loading.unmount();
  });

  it('matches full-width appearance rows without inventing an editor form', async () => {
    const { useBrand } = await import('../src/application/brand');
    api.appearances.mockResolvedValue({
      ok: true,
      value: Array.from({ length: 3 }, (_, index) => ({
        id: `appearance-${index}`,
        name: 'Appearance',
        startsAt: '2027-01-01T00:00:00Z',
        endsAt: '2027-01-02T00:00:00Z',
        status: 'scheduled',
        overrides: {},
        accentHex: null,
        note: null,
        linkLabel: null,
        linkUrl: null,
      })),
    });
    await settle(useBrand().refresh());
    const { default: AppearancesPage } = await import('../src/presentation/brand/AppearancesPage.vue');
    const { default: BrandQueryState } = await import('../src/presentation/common/BrandQueryState.vue');
    const loaded = mount(AppearancesPage);
    const loading = mount(BrandQueryState, { props: { state: 'loading', kind: 'appearances' } });
    const actual = loaded.get('ul');
    const placeholder = loading.get('ul');
    expect(placeholder.classes()).toEqual(actual.classes());
    expect(placeholder.findAll('li')).toHaveLength(actual.findAll('li').length);
    expect(placeholder.get('li').classes()).toEqual(actual.get('li').classes());
    expect(loading.find('input').exists()).toBe(false);
    loaded.unmount();
    loading.unmount();
  });

  it('matches the connection table rows and columns with fixed slot labels visible', async () => {
    const { useBrand } = await import('../src/application/brand');
    await settle(useBrand().refresh());
    const { default: ConnectPage } = await import('../src/presentation/brand/ConnectPage.vue');
    const { default: BrandQueryState } = await import('../src/presentation/common/BrandQueryState.vue');
    const loaded = mount(ConnectPage);
    const loading = mount(BrandQueryState, { props: { state: 'loading', kind: 'connect' } });
    const actual = loaded.get('table');
    const placeholder = loading.get('table');
    expect(placeholder.classes()).toEqual(actual.classes());
    expect(placeholder.findAll('tr')).toHaveLength(actual.findAll('tr').length);
    expect(placeholder.findAll('td')).toHaveLength(actual.findAll('td').length);
    expect(placeholder.findAll('tr').map((row) => row.get('td').text())).toEqual(
      actual.findAll('tr').map((row) => row.get('td').text()),
    );
    loaded.unmount();
    loading.unmount();
  });
});
