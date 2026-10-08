import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { DefaultRetryPolicy } from '@wow-two-beta/ui-vue/foundation/resilience';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { FileUploadPicker } from '@wow-two-beta/ui-vue/presentation/forms';
import { createQueryClient, queryPlugin } from '@wow-two-beta/ui-vue/query';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { ContactRoute, SourcingLocation, SourcingMedia, Supplier, SupplierSummary } from '@/domain/workspace/Sourcing';
import SourcingPage from '@/presentation/workspace/SourcingPage.vue';

const api = vi.hoisted(() => ({
  listSuppliers: vi.fn(), createSupplier: vi.fn(), getSupplier: vi.fn(), updateSupplier: vi.fn(),
  createPerson: vi.fn(), updatePerson: vi.fn(), deletePerson: vi.fn(), createRoute: vi.fn(), updateRoute: vi.fn(), deleteRoute: vi.fn(),
  listMedia: vi.fn(), mediaSummary: vi.fn(), uploadMedia: vi.fn(), updateMedia: vi.fn(), assignMedia: vi.fn(), deleteMedia: vi.fn(),
  contentUrl: vi.fn((_o: string, id: string) => `/content/${id}`), thumbnailUrl: vi.fn((_o: string, id: string) => `/thumbnail/${id}`),
}));
vi.mock('@/integration/workspace/SourcingApi', () => ({ createSourcingApi: () => api }));

const ORG = '11111111-1111-4111-8111-111111111111';
const SUPPLIER = '22222222-2222-4222-8222-222222222222';
const OTHER = '33333333-3333-4333-8333-333333333333';
const PERSON = '44444444-4444-4444-8444-444444444444';
const at = '2026-10-01T00:00:00Z';
const summary = (over: Partial<SupplierSummary>): SupplierSummary => ({
  id: SUPPLIER, key: null, name: 'Sethi Weaves', stage: 'quoted', country: 'IN', city: 'Varanasi',
  primaryRoute: { channel: 'whatsapp', destination: '+91 98765 43210' }, routeCount: 2, personCount: 1, mediaCount: 3,
  coverMediaId: null, revision: 1, updatedAt: at, ...over,
});
const route = (over: Partial<ContactRoute>): ContactRoute => ({
  id: 'route-1', supplierId: SUPPLIER, personId: null, channel: 'whatsapp', destination: '+91 98765 43210', label: null, sourceUrl: null,
  verified: false, isPrimary: false, notes: '', revision: 1, createdAt: at, updatedAt: at, ...over,
});
const supplier = (): Supplier => ({
  id: SUPPLIER, key: 'swi', name: 'Sethi Weaves', stage: 'quoted', country: 'IN', city: 'Varanasi', website: 'https://sethi.example.test', notes: 'Silk brocade',
  people: [{ id: PERSON, supplierId: SUPPLIER, name: 'Navit Sethi', role: 'Owner', notes: '', revision: 1, createdAt: at, updatedAt: at }],
  routes: [
    route({ id: 'route-1', personId: PERSON, verified: true, isPrimary: true, sourceUrl: 'https://source.example.test/listing' }),
    route({ id: 'route-2', channel: 'website', destination: 'javascript:alert(1)' }),
    route({ id: 'route-3', channel: 'email', destination: 'sales@sethi.example.test' }),
  ],
  revision: 4, createdAt: at, updatedAt: at,
});
const media = (index: number, over: Partial<SourcingMedia> = {}): SourcingMedia => ({
  id: `media-${index}`, supplierId: null, sha256: String(index).repeat(64).slice(0, 64), contentType: 'image/jpeg', byteSize: 2048, width: 800, height: 600,
  hasThumbnail: true, capture: 'original', originalName: `photo-${index}.jpg`, caption: '', sourceLabel: 'WhatsApp 2026-09-29', sourceMessageId: null,
  receivedAt: '2026-09-29T11:54:00Z', takenAt: null, hasLocation: false, revision: 1, createdAt: at, updatedAt: at, ...over,
});

let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;
beforeEach(() => {
  useUiLocale().setUiLocale('en');
  window.history.replaceState(null, '', '/');
  api.listSuppliers.mockResolvedValue(ResultExtensions.ok([summary({}), summary({ id: OTHER, name: 'Bukhara Silk', stage: 'lead', country: 'UZ', city: 'Bukhara', primaryRoute: null, mediaCount: 0 })]));
  api.mediaSummary.mockResolvedValue(ResultExtensions.ok({ total: 7, unassigned: 4 }));
  api.getSupplier.mockResolvedValue(ResultExtensions.ok(supplier()));
  api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1), media(2, { hasLocation: true }), media(3, { contentType: 'application/pdf', hasThumbnail: false, originalName: 'price-list.pdf' })]));
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  client?.clear();
});

function render(role: 'owner' | 'editor' | 'viewer', initialLocation: SourcingLocation) {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  const access = {
    busy: ref(false), dirty: ref(false), session: ref({ csrfToken: 'token', sessionId: 'session' }),
    setDirty: vi.fn((value: boolean) => { access.dirty.value = value; }), recheck: vi.fn(),
    write: vi.fn((operation: (signal: AbortSignal) => unknown) => operation(new AbortController().signal)),
  };
  const workspace = { organization: ref({ id: ORG, name: 'Ocharo', slug: 'ocharo', role }), canEdit: computed(() => role !== 'viewer') };
  wrapper = mount(SourcingPage, {
    props: { access: access as unknown as AccessControls, workspace: workspace as unknown as WorkspaceControls, initialLocation },
    global: { plugins: [queryPlugin(client)] },
    attachTo: document.body,
  });
  return { access };
}
const button = (label: string) => wrapper!.findAll('button').find(item => item.text() === label);

describe('sourcing supplier list', () => {
  it('renders suppliers with stage, place, primary channel, media count and the inbox count', async () => {
    render('editor', { view: 'suppliers', supplierId: null });
    await flushPromises();
    const list = wrapper!.get('nav[aria-label="Suppliers"]');
    expect(list.text()).toContain('Sethi Weaves');
    expect(list.text()).toContain('Quoted');
    expect(list.text()).toContain('Varanasi, IN · WhatsApp · 3 media');
    expect(list.text()).toContain('Bukhara Silk');
    expect(list.text()).toContain('4 unsorted files');
    expect(api.listSuppliers).toHaveBeenCalledWith(ORG, { query: '', stage: '', limit: 200, offset: 0 }, expect.any(AbortSignal));
  });

  it('passes the search text and stage filter to the server and opens a supplier', async () => {
    render('editor', { view: 'suppliers', supplierId: null });
    await flushPromises();
    api.listSuppliers.mockResolvedValue(ResultExtensions.ok([summary({ id: OTHER, name: 'Bukhara Silk', stage: 'lead' })]));
    await wrapper!.get('input[type="search"]').setValue('silk');
    await new Promise(resolve => setTimeout(resolve, 300));
    await flushPromises();
    expect(api.listSuppliers).toHaveBeenLastCalledWith(ORG, { query: 'silk', stage: '', limit: 200, offset: 0 }, expect.any(AbortSignal));
    const stage = wrapper!.findAll('select').find(select => select.text().includes('All stages'))!;
    await stage.setValue('lead');
    await flushPromises();
    expect(api.listSuppliers).toHaveBeenLastCalledWith(ORG, { query: 'silk', stage: 'lead', limit: 200, offset: 0 }, expect.any(AbortSignal));
    expect(wrapper!.get('nav[aria-label="Suppliers"]').text()).not.toContain('Sethi Weaves');

    await wrapper!.findAll('nav[aria-label="Suppliers"] button').find(item => item.text().includes('Bukhara Silk'))!.trigger('click');
    await flushPromises();
    expect(window.location.pathname).toBe(`/organizations/${ORG}/sourcing/${OTHER}`);
    expect(api.getSupplier).toHaveBeenCalledWith(ORG, OTHER, expect.any(AbortSignal));
  });
});

describe('sourcing supplier detail', () => {
  it('renders safe route links only and never a script URL', async () => {
    render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    const routes = wrapper!.get('[aria-labelledby="sourcing-routes-title"]');
    const hrefs = routes.findAll('a').map(link => link.attributes('href'));
    expect(hrefs).toContain('https://wa.me/919876543210');
    expect(hrefs).toContain('mailto:sales@sethi.example.test');
    expect(hrefs).toContain('https://source.example.test/listing');
    expect(hrefs.some(href => href?.toLowerCase().startsWith('javascript'))).toBe(false);
    expect(routes.text()).toContain('javascript:alert(1)');
    expect(routes.text()).toContain('Navit Sethi');
    expect(routes.findAll('a[target="_blank"]').every(link => link.attributes('rel') === 'noopener noreferrer')).toBe(true);
    expect(routes.text().indexOf('+91 98765 43210')).toBeLessThan(routes.text().indexOf('sales@sethi.example.test'));
  });

  it('hides every edit control from viewers', async () => {
    render('viewer', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    expect(wrapper!.text()).toContain('Sethi Weaves');
    expect(wrapper!.text()).toContain('photo-1.jpg');
    for (const label of ['Add supplier', 'Edit details', 'Add route', 'Add person', 'Edit', 'Delete', 'Select files']) expect(button(label)).toBeUndefined();
    expect(wrapper!.findComponent(FileUploadPicker).exists()).toBe(false);
    expect(wrapper!.findAll('select').some(select => select.text().includes('Qualified') && !select.text().includes('All stages'))).toBe(false);
  });

  it('shows the edit controls to editors', async () => {
    render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    for (const label of ['Add supplier', 'Edit details', 'Add route', 'Add person', 'Select files']) expect(button(label)).toBeDefined();
    expect(wrapper!.findComponent(FileUploadPicker).exists()).toBe(true);
  });

  it('keeps the edited values and explains a revision conflict', async () => {
    const { access } = render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    await button('Edit details')!.trigger('click');
    const name = wrapper!.get('form input');
    await name.setValue('Sethi Weaves Pvt');
    expect(access.setDirty).toHaveBeenLastCalledWith(true);
    api.updateSupplier.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await wrapper!.get('form').trigger('submit');
    await flushPromises();
    expect(api.updateSupplier).toHaveBeenCalledWith(ORG, SUPPLIER, 4, expect.objectContaining({ name: 'Sethi Weaves Pvt', stage: 'quoted', country: 'IN' }), expect.any(AbortSignal));
    expect(wrapper!.text()).toContain('This record changed. Your edits are still here.');
    expect((wrapper!.get('form input').element as HTMLInputElement).value).toBe('Sethi Weaves Pvt');
    expect(access.dirty.value).toBe(true);
  });
});

describe('sourcing inbox', () => {
  it('uploads dropped files to the inbox and shows duplicates', async () => {
    render('editor', { view: 'inbox', supplierId: null });
    await flushPromises();
    expect(api.listMedia).toHaveBeenCalledWith(ORG, { kind: 'inbox' }, { limit: 100, offset: 0 }, expect.any(AbortSignal));
    api.uploadMedia
      .mockResolvedValueOnce(ResultExtensions.ok({ media: media(9), duplicate: false, nearDuplicateIds: [] }))
      .mockResolvedValueOnce(ResultExtensions.ok({ media: media(1, { supplierId: SUPPLIER }), duplicate: true, nearDuplicateIds: [] }));
    const files = [new File(['a'], 'new.jpg', { type: 'image/jpeg' }), new File(['b'], 'again.jpg', { type: 'image/jpeg' })];
    wrapper!.findComponent(FileUploadPicker).vm.$emit('files-change', files, []);
    await flushPromises();
    expect(api.uploadMedia).toHaveBeenCalledWith(ORG, files[0], { supplierId: null, capture: 'original', caption: '' }, expect.any(AbortSignal));
    const uploads = wrapper!.get('ul[aria-label="Uploads"]');
    expect(uploads.get('[data-status="added"]').text()).toContain('new.jpg');
    expect(uploads.get('[data-status="duplicate"]').text()).toContain('Already in Desk');
    expect(api.listMedia.mock.calls.length).toBeGreaterThan(1);
  });

  it('assigns selected files to a supplier in one request', async () => {
    render('editor', { view: 'inbox', supplierId: null });
    await flushPromises();
    api.assignMedia.mockResolvedValue(ResultExtensions.ok({ updated: 2 }));
    await button('Select files')!.trigger('click');
    const tiles = wrapper!.findAll('.sourcing-tile-button');
    await tiles[0]!.trigger('click');
    await tiles[2]!.trigger('click');
    expect(wrapper!.text()).toContain('2 selected');
    await wrapper!.get('select[aria-label="Destination supplier"]').setValue(OTHER);
    await button('Assign')!.trigger('click');
    await flushPromises();
    expect(api.assignMedia).toHaveBeenCalledWith(ORG, ['media-1', 'media-3'], OTHER, expect.any(AbortSignal));
    expect(wrapper!.text()).toContain('2 files moved to Bukhara Silk.');
  });

  it('opens a lightbox in grid order with keyboard navigation and returns focus', async () => {
    render('editor', { view: 'inbox', supplierId: null });
    await flushPromises();
    expect(wrapper!.findAll('.sourcing-tile-badge')).toHaveLength(1);
    const tile = wrapper!.findAll('.sourcing-tile-button')[1]!;
    (tile.element as HTMLElement).focus();
    await tile.trigger('click');
    await flushPromises();
    const dialog = () => wrapper!.find('[role="dialog"]');
    expect(dialog().exists()).toBe(true);
    expect(document.activeElement).toBe(dialog().element);
    expect(dialog().text()).toContain('photo-2.jpg');
    expect(dialog().text()).toContain('2 of 3');
    expect(dialog().text()).toContain('location (GPS)');
    expect(dialog().get('img').attributes('src')).toBe('/content/media-2');

    await dialog().trigger('keydown', { key: 'ArrowRight' });
    expect(dialog().text()).toContain('3 of 3');
    expect(dialog().find('a[href="/content/media-3"]').text()).toBe('Open PDF');
    await dialog().trigger('keydown', { key: 'ArrowRight' });
    expect(dialog().text()).toContain('3 of 3');
    await dialog().trigger('keydown', { key: 'ArrowLeft' });
    await dialog().trigger('keydown', { key: 'ArrowLeft' });
    expect(dialog().text()).toContain('1 of 3');

    await dialog().trigger('keydown', { key: 'Escape' });
    expect(dialog().exists()).toBe(false);
    expect(document.activeElement).toBe(tile.element);
  });
});
