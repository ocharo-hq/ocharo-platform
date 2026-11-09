import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { nextTick } from 'vue';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DraftView } from '../src/domain/brand/BrandModels';
import { slots } from '../src/domain/brand/BrandModels';
import AppearancesPage from '../src/presentation/brand/AppearancesPage.vue';
import ConnectPage from '../src/presentation/brand/ConnectPage.vue';
import IdentityPage from '../src/presentation/brand/IdentityPage.vue';
import LogosPage from '../src/presentation/brand/LogosPage.vue';
import OverviewPage from '../src/presentation/brand/OverviewPage.vue';
import BrandQueryState from '../src/presentation/common/BrandQueryState.vue';
import { mountBrand, mountInBrand } from './helpers/BrandApp';
import { settleBrandReads } from './helpers/BrandFixtures';

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
        descriptor: { uz: 'Tavsif' },
        tagline: {},
        location: {},
        palette: [],
        typography: { display: 'Georgia', body: 'Arial' },
        links: [],
      },
      currentLogos: {},
    },
    updatedAt: '2026-10-02T00:00:00Z',
    logos: [],
    unpublishedChanges: [],
  };
}
const failed = { ok: false, failure: { code: 'transport', status: 0 } };
const mounted: VueWrapper[] = [];

/** Mounts `content` under the session and the query client, as the frame does, and keeps it for the clean-up. */
async function host(content?: Parameters<typeof mountInBrand>[0]) {
  const result = await mountInBrand(content);
  mounted.push(result.wrapper);
  return result;
}

beforeEach(() => {
  vi.clearAllMocks();
  api.draft.mockResolvedValue({ ok: true, value: draft() });
  api.releases.mockResolvedValue({ ok: true, value: [] });
  api.appearances.mockResolvedValue({ ok: true, value: [] });
  api.consumers.mockResolvedValue({ ok: true, value: [] });
  api.publicKit.mockResolvedValue(failed);
  api.saveIdentity.mockResolvedValue({ ok: true, value: draft() });
});
afterEach(() => {
  for (const wrapper of mounted.splice(0)) wrapper.unmount();
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('Brand read outcomes', () => {
  it('distinguishes a failed first administrative read from a successful empty collection', async () => {
    api.releases.mockResolvedValue(failed);
    const { brand } = await host();
    await flushPromises();
    expect(brand.queryState.value).toBe('unavailable');
    expect(brand.draft.value).toBeNull();
    expect(brand.refreshing.value).toBe(false);
    expect(brand.error.value).toContain('So‘rov bajarilmadi');
  });

  it('coalesces refreshes and preserves the complete last snapshot if a read fails', async () => {
    const { brand } = await host();
    await flushPromises();
    expect(api.draft).toHaveBeenCalledTimes(1);
    const lastGood = brand.draft.value;
    expect(brand.queryState.value).toBe('success');
    api.draft.mockResolvedValue({ ok: true, value: draft('New server identity', 2) });
    api.consumers.mockResolvedValue(failed);
    const refresh = brand.refresh();
    expect(brand.refresh()).toBe(refresh);
    await flushPromises();
    expect(brand.refreshing.value).toBe(true);
    expect(brand.draft.value).toBe(lastGood);
    await refresh;
    expect(api.draft).toHaveBeenCalledTimes(2);
    expect(brand.draft.value).toBe(lastGood);
    expect(brand.queryState.value).toBe('success');
    expect(brand.refreshing.value).toBe(false);
  });

  it('keeps the published kit of the last read when only the kit fails', async () => {
    api.publicKit.mockResolvedValue({ ok: true, value: { name: 'Ocharo', release: 2, logos: {} } });
    const { brand } = await host();
    await flushPromises();
    expect(brand.kit.value?.release).toBe(2);
    api.publicKit.mockResolvedValue(failed);
    await brand.refresh();
    expect(brand.kit.value?.release).toBe(2);
  });
});

describe('Brand acknowledged projection', () => {
  it('starts a new read after a save when an older refresh is already running', async () => {
    const { brand } = await host();
    await flushPromises();
    const olderRead = brand.refresh();
    await flushPromises();
    const identity = structuredClone(draft('Saved identity').draft.identity);
    const saved = brand.saveIdentity(identity, draft().revision);
    api.draft.mockResolvedValue({ ok: true, value: draft('Saved identity', 2) });
    await olderRead;
    expect(await saved).toMatchObject({ revision: draft().revision });
    expect(api.draft).toHaveBeenCalledTimes(3);
    expect(brand.draft.value?.draft.identity.name).toBe('Saved identity');
  });
});

describe('Brand command lock', () => {
  it('blocks overlapping commands and user refresh until the acknowledged read settles', async () => {
    const { brand } = await host();
    await flushPromises();
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
    expect(await saved).toMatchObject({ revision: draft().revision });
    expect(brand.mutating.value).toBe(false);
    expect(brand.busy.value).toBe(false);
  });
});

describe('Brand session lifetime', () => {
  it('drops the cached reads when the session ends, and reads them again for the next one', async () => {
    const { brand } = await host();
    await flushPromises();
    expect(brand.draft.value).not.toBeNull();
    const { identityBridge } = await import('../src/integration/common/IdentitySession');
    api.draft.mockReturnValue(new Promise(() => {}));
    identityBridge.expire();
    await flushPromises();
    expect(brand.draft.value).toBeNull();
    expect(brand.queryState.value).toBe('loading');
  });
});

describe('Identity query rendering', () => {
  it('keeps its routed editor mounted and refresh in the frame bar through failure and recovery', async () => {
    // The frame's first read stays open until the test answers it, so the loading editor can be inspected.
    let answerFirstRead!: (value: unknown) => void;
    api.draft.mockResolvedValue(failed).mockReturnValueOnce(
      new Promise((resolve) => {
        answerFirstRead = resolve;
      }),
    );
    const { wrapper } = await mountBrand('/identity');
    mounted.push(wrapper);
    expect(wrapper.findComponent({ name: 'IdentityFormSkeleton' }).exists()).toBe(true);
    expect(wrapper.findAll('h2').map((heading) => heading.text())).toContain('Rasmiy havolalar');
    answerFirstRead(failed);
    await flushPromises();
    expect(wrapper.text()).toContain('Identitet ma’lumotlari hozir mavjud emas.');
    expect(wrapper.text()).not.toContain('Hali identitet ma’lumotlari yo‘q.');
    expect(
      wrapper
        .find('main')
        .findAll('button')
        .filter((button) => button.text().includes('Qayta urinish')),
    ).toHaveLength(0);
    const refresh = () =>
      wrapper
        .get('header')
        .findAll('button')
        .find((button) => button.text().includes('Yangilash'))!;
    expect(refresh().exists()).toBe(true);
    api.draft.mockResolvedValue({ ok: true, value: draft() });
    await refresh().trigger('click');
    await settleBrandReads();
    const nameInput = wrapper.get('input[name="name"]').element;
    await refresh().trigger('click');
    await nextTick();
    expect(wrapper.get('input[name="name"]').element).toBe(nameInput);
    expect(wrapper.get('input[name="name"]').isVisible()).toBe(true);
    expect(wrapper.find('main').find('button').attributes('disabled')).toBeDefined();
    expect(refresh().exists()).toBe(true);
    await settleBrandReads();
  });

  it('retains edited fields when refreshed server identity changes', async () => {
    const { wrapper, brand } = await host(IdentityPage);
    await flushPromises();
    await wrapper.get('input[name="name"]').setValue('Unsaved local name');
    api.draft.mockResolvedValue({ ok: true, value: draft('Changed server name', 2) });
    await brand.refresh();
    expect((wrapper.get('input[name="name"]').element as HTMLInputElement).value).toBe('Unsaved local name');
    expect(wrapper.find('button').attributes('disabled')).toBeUndefined();
  });

  it('keeps the baseline revision after dirty refresh so intervening server changes conflict', async () => {
    const { wrapper, brand } = await host(IdentityPage);
    await flushPromises();
    await wrapper.get('input[name="name"]').setValue('Unsaved local name');
    api.draft.mockResolvedValue({ ok: true, value: draft('Intervening server name', 2) });
    await brand.refresh();
    expect(brand.draft.value?.revision).toBe(2);
    api.saveIdentity.mockResolvedValue({ ok: false, failure: { code: 'http', status: 409 } });
    await wrapper.find('button').trigger('click');
    await flushPromises();
    expect(api.saveIdentity.mock.calls[0]?.[0]).toBe(1);
    expect(api.saveIdentity.mock.calls[0]?.[1].name).toBe('Unsaved local name');
    expect((wrapper.get('input[name="name"]').element as HTMLInputElement).value).toBe('Unsaved local name');
    expect(brand.error.value).toContain('So‘rov bajarilmadi');
    expect(brand.draft.value?.draft.identity.name).toBe('Intervening server name');
  });

  it('acknowledges only the submitted values when editing continues during save', async () => {
    const { wrapper } = await host(IdentityPage);
    await flushPromises();
    await wrapper.get('input[name="name"]').setValue('Submitted name');
    await wrapper.find('button').trigger('click');
    await flushPromises();
    await wrapper.get('input[name="name"]').setValue('Later unsaved name');
    await settleBrandReads();
    expect(api.saveIdentity.mock.calls[0]?.[1].name).toBe('Submitted name');
    expect((wrapper.get('input[name="name"]').element as HTMLInputElement).value).toBe('Later unsaved name');
    expect(wrapper.find('button').attributes('disabled')).toBeUndefined();
  });
});

describe('Brand initial route geometry', () => {
  /** The page under the brand store with its reads settled, beside its first-read placeholder. */
  async function pair(
    page: Parameters<typeof mountInBrand>[0],
    kind: 'overview' | 'logos' | 'appearances' | 'connect',
  ) {
    const { wrapper } = await host(page);
    await flushPromises();
    const loading = mount(BrandQueryState, { props: { state: 'loading', kind } });
    mounted.push(loading);
    return { loaded: wrapper, loading };
  }

  it('matches the six logo sections, triptych previews and responsive version grids', async () => {
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
      uploadedAt: value.updatedAt,
    }));
    value.draft.currentLogos.master = 'logo-0';
    api.draft.mockResolvedValue({ ok: true, value });
    const { loaded, loading } = await pair(LogosPage, 'logos');
    const sections = loaded.findAll('[aria-labelledby^="slot-"]');
    const placeholders = loading.findAll('[aria-labelledby^="loading-slot-"]');
    expect(placeholders).toHaveLength(sections.length);
    expect(placeholders.map((section) => section.attributes('aria-labelledby')?.replace('loading-slot-', ''))).toEqual(
      slots,
    );
    expect(placeholders[0]!.classes()).toEqual(sections[0]!.classes());
    const actual = loaded.get('[aria-labelledby="slot-master"]');
    const placeholder = loading.get('[aria-labelledby="loading-slot-master"]');
    expect(placeholder.get('.grid-cols-3').element.children).toHaveLength(actual.findAll('[class*="aspect-"]').length);
    expect(placeholder.get('ul').classes()).toEqual(actual.get('ul').classes());
    expect(placeholder.findAll('li')).toHaveLength(actual.findAll('li').length);
  });

  it('matches the overview consumer grid and ordered release rows', async () => {
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
    const { loaded, loading } = await pair(OverviewPage, 'overview');
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
  });

  it('matches full-width appearance rows without inventing an editor form', async () => {
    api.appearances.mockResolvedValue({
      ok: true,
      value: Array.from({ length: 3 }, (_, index) => ({
        id: `appearance-${index}`,
        name: 'Appearance',
        startsAt: '2027-01-01T00:00:00Z',
        endsAt: '2027-01-02T00:00:00Z',
        status: 'scheduled',
        overrides: {},
      })),
    });
    const { loaded, loading } = await pair(AppearancesPage, 'appearances');
    const actual = loaded.get('ul');
    const placeholder = loading.get('ul');
    expect(placeholder.classes()).toEqual(actual.classes());
    expect(placeholder.findAll(':scope > li')).toHaveLength(actual.findAll(':scope > li').length);
    expect(placeholder.get('li > *').classes()).toEqual(actual.get('li > *').classes());
    expect(loading.find('input').exists()).toBe(false);
  });

  it('matches the connection table rows and columns with fixed slot labels visible', async () => {
    const { loaded, loading } = await pair(ConnectPage, 'connect');
    const actual = loaded.get('table');
    const placeholder = loading.get('table');
    expect(placeholder.classes()).toEqual(actual.classes());
    expect(placeholder.findAll('tr')).toHaveLength(actual.findAll('tr').length);
    expect(placeholder.findAll('td')).toHaveLength(actual.findAll('td').length);
    expect(placeholder.findAll('tr').map((row) => row.get('td').text())).toEqual(
      actual.findAll('tr').map((row) => row.get('td').text()),
    );
  });
});
