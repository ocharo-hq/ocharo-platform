import { deskSelect, chooseDeskValue } from './helpers/DeskSelect';
import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { DefaultRetryPolicy } from '@wow-two-beta/ui-vue/foundation/resilience';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { FileUploadPicker } from '@wow-two-beta/ui-vue/presentation/forms';
import { createQueryClient, queryPlugin } from '@wow-two-beta/ui-vue/query';

import { confirmation, feedbackBus, resolveConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { ContactRoute, SourcingLocation, SourcingMedia, Supplier, SupplierSummary } from '@/domain/workspace/Sourcing';
import { createSourcingPreferencesStorage } from '@/integration/workspace/SourcingPreferencesStorage';
import SourcingViewSettings from '@/presentation/workspace/SourcingViewSettings.vue';
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
  localStorage.clear();
  useUiLocale().setUiLocale('en');
  window.history.replaceState(null, '', '/');
  api.listSuppliers.mockResolvedValue(ResultExtensions.ok([summary({}), summary({ id: OTHER, name: 'Bukhara Silk', stage: 'lead', country: 'UZ', city: 'Bukhara', primaryRoute: null, mediaCount: 0 })]));
  api.mediaSummary.mockResolvedValue(ResultExtensions.ok({ total: 7, unassigned: 4 }));
  api.getSupplier.mockResolvedValue(ResultExtensions.ok(supplier()));
  api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1), media(2, { hasLocation: true }), media(3, { contentType: 'application/pdf', hasThumbnail: false, originalName: 'price-list.pdf' })]));
});
afterEach(() => {
  resolveConfirmation(false);
  wrapper?.unmount();
  wrapper = undefined;
  client?.clear();
});

function render(role: 'owner' | 'editor' | 'viewer', initialLocation: SourcingLocation) {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  const access = {
    busy: ref(false), dirty: ref(false), invalidated: ref(false), checking: ref(false), session: ref({ csrfToken: 'token', sessionId: 'session', authenticated: true, user: { id: PERSON } }),
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
    await chooseDeskValue(wrapper!, 'Stage', 'lead');
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
    expect(wrapper!.findComponent({ name: 'SourcingSupplierDetail' }).findAllComponents({ name: 'DeskSelect' }).some(select => select.props('label') === 'Stage')).toBe(false);
  });

  it('retains the committed stage throughout a pending and rejected update', async () => {
    render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    const detail = wrapper!.findComponent({ name: 'SourcingSupplierDetail' });
    const picker = deskSelect(detail, 'Stage');
    const committed = picker.props('modelValue');
    let finish!: (value: unknown) => void;
    api.updateSupplier.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    await chooseDeskValue(detail, 'Stage', 'qualified');
    expect(api.updateSupplier).toHaveBeenCalledTimes(1);
    expect(picker.props('modelValue')).toBe(committed);
    expect(picker.props('disabled')).toBe(true);
    finish(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await flushPromises();
    expect(picker.props('modelValue')).toBe(committed);
    expect(picker.props('disabled')).toBe(false);
    expect(detail.text()).toContain('This record changed. Reload the latest version before trying again.');
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
    await chooseDeskValue(wrapper!, 'Destination supplier', OTHER);
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


describe('sourcing collection outcomes', () => {
  it('loads supplier rows and media tiles without replacing their toolbars', async () => {
    let finishSuppliers!: (value: ReturnType<typeof ResultExtensions.ok<SupplierSummary[]>>) => void;
    let finishMedia!: (value: ReturnType<typeof ResultExtensions.ok<SourcingMedia[]>>) => void;
    api.listSuppliers.mockImplementation(() => new Promise(resolve => { finishSuppliers = resolve; }));
    api.listMedia.mockImplementation(() => new Promise(resolve => { finishMedia = resolve; }));
    render('viewer', { view: 'inbox', supplierId: null });
    await flushPromises();
    expect(wrapper!.findAll('nav[aria-label="Suppliers"] div.sourcing-row')).toHaveLength(4);
    expect(wrapper!.get('.sourcing-gallery').findAll(':scope > div')).toHaveLength(6);
    expect(wrapper!.findAll('[aria-busy="true"] [role="status"]').every(item => item.classes().includes('sr-only'))).toBe(true);
    expect(button('Refresh suppliers')).toBeDefined();
    expect(button('Refresh media')).toBeDefined();
    expect(wrapper!.text()).not.toContain('0 files shown');
    expect(wrapper!.text()).not.toContain('The inbox is empty.');
    finishSuppliers(ResultExtensions.ok([]));
    finishMedia(ResultExtensions.ok([]));
    await flushPromises();
    const labels = wrapper!.findAll('.empty-state');
    expect(labels.map(label => label.text())).toEqual(['No suppliers yet.', 'The inbox is empty.']);
    expect(labels.every(label => !label.classes().includes('editor-prompt'))).toBe(true);
  });

  it('toasts initial failures and keeps refresh separate from empty results', async () => {
    const notice = vi.spyOn(feedbackBus, 'notify');
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 500, headers: new Headers() }));
    api.listSuppliers.mockResolvedValue(failure);
    api.listMedia.mockResolvedValue(failure);
    render('viewer', { view: 'inbox', supplierId: null });
    await flushPromises();
    expect(notice).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    expect(wrapper!.text()).not.toContain('unavailable');
    expect(wrapper!.text()).not.toContain('No suppliers yet.');
    expect(wrapper!.text()).not.toContain('The inbox is empty.');
    expect(wrapper!.find('[aria-busy="true"]').exists()).toBe(false);
    api.listSuppliers.mockResolvedValue(ResultExtensions.ok([summary({})]));
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1)]));
    await button('Refresh suppliers')!.trigger('click');
    await button('Refresh media')!.trigger('click');
    await flushPromises();
    expect(wrapper!.text()).toContain('Sethi Weaves');
    expect(wrapper!.text()).toContain('photo-1.jpg');
    notice.mockRestore();
  });

  it('retains a confirmed empty collection when refresh fails', async () => {
    api.listSuppliers.mockResolvedValue(ResultExtensions.ok([]));
    api.listMedia.mockResolvedValue(ResultExtensions.ok([]));
    render('viewer', { view: 'inbox', supplierId: null });
    await flushPromises();
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() }));
    api.listSuppliers.mockResolvedValue(failure);
    api.listMedia.mockResolvedValue(failure);
    await button('Refresh suppliers')!.trigger('click');
    await button('Refresh media')!.trigger('click');
    await flushPromises();
    expect(wrapper!.findAll('.empty-state').map(label => label.text())).toEqual(['No suppliers yet.', 'The inbox is empty.']);
  });

  it('preserves loaded suppliers and media after a failed refetch', async () => {
    render('viewer', { view: 'inbox', supplierId: null });
    await flushPromises();
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() }));
    api.listSuppliers.mockResolvedValue(failure);
    api.listMedia.mockResolvedValue(failure);
    await button('Refresh suppliers')!.trigger('click');
    await button('Refresh media')!.trigger('click');
    await flushPromises();
    expect(wrapper!.text()).toContain('Sethi Weaves');
    expect(wrapper!.findAll('.sourcing-tile')).toHaveLength(3);
    expect(wrapper!.find('.empty-state').exists()).toBe(false);
    expect(wrapper!.text()).not.toContain('unavailable');
  });
});


describe('personal sourcing views', () => {
  it('opens the material gallery and follows a supplier association', async () => {
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1, { supplierId: SUPPLIER }), media(2, { supplierId: '55555555-5555-4555-8555-555555555555' }), media(3)]));
    render('viewer', { view: 'suppliers', supplierId: null });
    await flushPromises();
    await button('Materials')!.trigger('click');
    await flushPromises();
    expect(window.location.pathname).toBe(`/organizations/${ORG}/sourcing/materials`);
    expect(api.listMedia).toHaveBeenCalledWith(ORG, { kind: 'all' }, { limit: 100, offset: 0 }, expect.any(AbortSignal));
    expect(button('Open supplier')).toBeDefined();
    expect(wrapper!.text()).toContain('Unassigned');
    await button('Sethi Weaves')!.trigger('click');
    await flushPromises();
    expect(window.location.pathname).toBe(`/organizations/${ORG}/sourcing/${SUPPLIER}`);
    expect(wrapper!.get('[aria-labelledby="sourcing-routes-title"]').text()).toContain('Contacts');
  });
  it('restores default material entry but preserves explicit supplier routes', async () => {
    const store = createSourcingPreferencesStorage({ userId: PERSON, organizationId: ORG });
    expect(store.save({ start: 'materials', order: 'contacts-first', visible: ['contacts'] })).toBe(true);
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1, { supplierId: SUPPLIER })]));
    window.history.replaceState(null, '', `/organizations/${ORG}/sourcing`);
    render('viewer', { view: 'suppliers', supplierId: null });
    await flushPromises();
    expect(wrapper!.find('#sourcing-materials-title').exists()).toBe(true);
    expect(window.location.pathname).toBe(`/organizations/${ORG}/sourcing/materials`);
    await button('Sethi Weaves')!.trigger('click');
    await flushPromises();
    expect(wrapper!.find('#sourcing-supplier-title').exists()).toBe(true);
    window.history.replaceState(null, '', `/organizations/${ORG}/sourcing/materials`);
    window.dispatchEvent(new PopStateEvent('popstate'));
    await flushPromises();
    expect(wrapper!.find('#sourcing-materials-title').exists()).toBe(true);
    wrapper!.unmount(); client!.clear();
    render('viewer', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    expect(wrapper!.find('#sourcing-supplier-title').exists()).toBe(true);
    expect(wrapper!.find('#sourcing-routes-title').exists()).toBe(true);
    expect(wrapper!.find('#sourcing-media-title').exists()).toBe(false);
    expect(wrapper!.find('#sourcing-people-title').exists()).toBe(false);
  });
  it('saves view settings separately from supplier records and guards dirty forms', async () => {
    const { access } = render('viewer', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    access.dirty.value = true;
    await button('View settings')!.trigger('click');
    expect(wrapper!.findComponent(SourcingViewSettings).exists()).toBe(false);
    access.dirty.value = false;
    await button('View settings')!.trigger('click');
    await flushPromises();
    const settings = wrapper!.getComponent(SourcingViewSettings);
    const updated = { start: 'suppliers', order: 'contacts-first', visible: ['contacts'] } as const;
    access.invalidated.value = true;
    expect(settings.props('save')({ ...updated, visible: [...updated.visible] })).toBe(false);
    access.invalidated.value = false;
    access.session.value.user.id = OTHER;
    expect(settings.props('save')({ ...updated, visible: [...updated.visible] })).toBe(false);
    access.session.value.user.id = PERSON;
    expect(settings.props('save')({ ...updated, visible: [...updated.visible] })).toBe(true);
    await flushPromises();
    expect(wrapper!.find('#sourcing-media-title').exists()).toBe(false);
    expect(wrapper!.find('#sourcing-supplier-title').exists()).toBe(true);
    expect(api.updateSupplier).not.toHaveBeenCalled();
  });
});


describe('sourcing pagination and editing recovery', () => {
  const supplierKey = ['session', ORG, 'sourcing', 'supplier', SUPPLIER];
  const inboxKey = ['session', ORG, 'sourcing', 'media', 'inbox', null];
  const failure = (status: number) => ResultExtensions.fail(ApiFailureFactory.create('http', { status, headers: new Headers() }));
  const dialog = () => wrapper!.get('.sourcing-lightbox');
  const dialogButton = (label: string) => dialog().findAll('button').find(item => item.text() === label)!;
  async function editMedia() {
    render('editor', { view: 'inbox', supplierId: null });
    await flushPromises();
    await wrapper!.findAll('.sourcing-tile-button')[1]!.trigger('click');
    await flushPromises();
    await dialogButton('Edit details').trigger('click');
    await dialog().get('textarea').setValue('My caption');
  }

  it('loads suppliers beyond the first page and resets pagination for a new filter', async () => {
    const first = Array.from({ length: 200 }, (_, index) => summary({ id: `supplier-${index}`, name: `Supplier ${index}` }));
    api.listSuppliers.mockImplementation((_org, filter) => Promise.resolve(ResultExtensions.ok(filter.stage === 'lead'
      ? [summary({ name: 'Filtered supplier' })]
      : filter.offset === 0 ? first : [summary({ name: 'Last supplier' })])));
    render('viewer', { view: 'suppliers', supplierId: null });
    await flushPromises();
    expect(button('Load more suppliers')).toBeDefined();
    await button('Load more suppliers')!.trigger('click');
    await flushPromises();
    expect(api.listSuppliers).toHaveBeenLastCalledWith(ORG, { query: '', stage: '', limit: 200, offset: 200 }, expect.any(AbortSignal));
    expect(wrapper!.get('nav[aria-label="Suppliers"]').text()).toContain('Last supplier');
    expect(button('Load more suppliers')).toBeUndefined();
    await chooseDeskValue(wrapper!, 'Stage', 'lead');
    await flushPromises();
    expect(api.listSuppliers).toHaveBeenLastCalledWith(ORG, { query: '', stage: 'lead', limit: 200, offset: 0 }, expect.any(AbortSignal));
    expect(wrapper!.text()).toContain('Filtered supplier');
    expect(wrapper!.text()).not.toContain('Last supplier');
  });

  it('deduplicates adjacent supplier pages without changing server offsets', async () => {
    api.listSuppliers.mockImplementation((_org, filter) => Promise.resolve(ResultExtensions.ok(
      filter.offset < 400 ? Array.from({ length: 200 }, (_, index) => summary({
        id: `supplier-${index + (filter.offset === 200 ? 199 : 0)}`, name: `Supplier ${index + filter.offset}`,
      })) : [],
    )));
    render('viewer', { view: 'suppliers', supplierId: null });
    await flushPromises();
    await button('Load more suppliers')!.trigger('click');
    await flushPromises();
    // 399 unique suppliers plus the Inbox entry. The boundary duplicate uses its newer fields.
    expect(wrapper!.findAll('nav[aria-label="Suppliers"] button.sourcing-row')).toHaveLength(400);
    expect(wrapper!.text()).not.toContain('Supplier 199');
    expect(wrapper!.text()).toContain('Supplier 200');
    await button('Load more suppliers')!.trigger('click');
    await flushPromises();
    expect(api.listSuppliers).toHaveBeenLastCalledWith(ORG, { query: '', stage: '', limit: 200, offset: 400 }, expect.any(AbortSignal));
    expect(button('Load more suppliers')).toBeUndefined();
  });

  it('deduplicates adjacent media pages while requesting the next raw offset', async () => {
    api.listMedia.mockImplementation((_org, _filter, page) => Promise.resolve(ResultExtensions.ok(
      page.offset < 200 ? Array.from({ length: 100 }, (_, index) => media(index + (page.offset === 100 ? 99 : 0))) : [],
    )));
    render('viewer', { view: 'inbox', supplierId: null });
    await flushPromises();
    await button('Load more')!.trigger('click');
    await flushPromises();
    expect(wrapper!.findAll('.sourcing-tile')).toHaveLength(199);
    await button('Load more')!.trigger('click');
    await flushPromises();
    expect(api.listMedia).toHaveBeenLastCalledWith(ORG, { kind: 'inbox' }, { limit: 100, offset: 200 }, expect.any(AbortSignal));
    expect(button('Load more')).toBeUndefined();
  });

  it('retains supplier drafts and their original revision through refresh and failed conflict reload', async () => {
    render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    await button('Edit details')!.trigger('click');
    await wrapper!.get('form input').setValue('My supplier edit');
    client!.setQueryData(supplierKey, { ...supplier(), name: 'Changed elsewhere', revision: 5 });
    await flushPromises();
    api.updateSupplier.mockResolvedValue(failure(409));
    await wrapper!.get('form').trigger('submit');
    await flushPromises();
    expect(api.updateSupplier).toHaveBeenLastCalledWith(ORG, SUPPLIER, 4, expect.objectContaining({ name: 'My supplier edit' }), expect.any(AbortSignal));
    api.getSupplier.mockResolvedValue(failure(503));
    await button('Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((wrapper!.get('form input').element as HTMLInputElement).value).toBe('My supplier edit');
    expect(wrapper!.get('form').text()).toContain('This record changed.');
    api.getSupplier.mockResolvedValue(ResultExtensions.ok({ ...supplier(), name: 'Latest supplier', revision: 6 }));
    await button('Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((wrapper!.get('form input').element as HTMLInputElement).value).toBe('Latest supplier');
    await wrapper!.get('form').trigger('submit');
    await flushPromises();
    expect(api.updateSupplier).toHaveBeenLastCalledWith(ORG, SUPPLIER, 6, expect.any(Object), expect.any(AbortSignal));
  });

  it.each(['person', 'route'] as const)('uses the edited %s revision after supplier data changes', async kind => {
    render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    const section = wrapper!.get(`[aria-labelledby="sourcing-${kind === 'person' ? 'people' : 'routes'}-title"]`);
    await section.findAll('button').find(item => item.text() === 'Edit')!.trigger('click');
    await section.get('form input.sourcing-input').setValue(kind === 'person' ? 'My person edit' : '+91 99999 88888');
    const latest = supplier();
    latest.people[0]!.revision = 2;
    latest.routes[0]!.revision = 2;
    client!.setQueryData(supplierKey, latest);
    await flushPromises();
    const update = kind === 'person' ? api.updatePerson : api.updateRoute;
    update.mockResolvedValue(failure(409));
    await section.get('form').trigger('submit');
    await flushPromises();
    expect(update).toHaveBeenLastCalledWith(ORG, kind === 'person' ? PERSON : 'route-1', 1, expect.any(Object), expect.any(AbortSignal));
    api.getSupplier.mockResolvedValue(failure(503));
    await section.findAll('button').find(item => item.text() === 'Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((section.get('form input.sourcing-input').element as HTMLInputElement).value).toBe(kind === 'person' ? 'My person edit' : '+91 99999 88888');
  });

  it.each(['person', 'route'] as const)('retains a dirty %s after a remote removal and a successful missing-record reload', async kind => {
    const { access } = render('editor', { view: 'supplier', supplierId: SUPPLIER });
    await flushPromises();
    const section = wrapper!.get(`[aria-labelledby="sourcing-${kind === 'person' ? 'people' : 'routes'}-title"]`);
    await section.findAll('button').find(item => item.text() === 'Edit')!.trigger('click');
    const draft = kind === 'person' ? 'Retained person draft' : '+91 99999 88888';
    await section.get('form input.sourcing-input').setValue(draft);
    const removed = supplier();
    if (kind === 'person') removed.people = [];
    else removed.routes = [];
    client!.setQueryData(supplierKey, removed);
    await flushPromises();
    expect((section.get('form input.sourcing-input').element as HTMLInputElement).value).toBe(draft);
    expect(section.text()).toContain('This item is no longer in this view.');
    expect(access.dirty.value).toBe(true);
    api.getSupplier.mockResolvedValue(ResultExtensions.ok(removed));
    await section.findAll('button').find(item => item.text() === 'Reload latest')!.trigger('click');
    await flushPromises();
    expect((section.get('form input.sourcing-input').element as HTMLInputElement).value).toBe(draft);
    const update = kind === 'person' ? api.updatePerson : api.updateRoute;
    update.mockResolvedValue(failure(404));
    const notice = vi.spyOn(feedbackBus, 'notify');
    await section.get('form').trigger('submit');
    await flushPromises();
    expect(update).toHaveBeenLastCalledWith(ORG, kind === 'person' ? PERSON : 'route-1', 1, expect.any(Object), expect.any(AbortSignal));
    expect(notice).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    expect((section.get('form input.sourcing-input').element as HTMLInputElement).value).toBe(draft);
    expect(access.dirty.value).toBe(true);
    notice.mockRestore();
    await section.findAll('button').find(item => item.text() === 'Cancel')!.trigger('click');
    expect(section.find('form').exists()).toBe(false);
    expect(access.dirty.value).toBe(false);
  });

  it('retains a media draft after its file leaves the gallery and cannot be found by reload', async () => {
    await editMedia();
    client!.setQueryData(inboxKey, { pages: [[media(1), media(3)]], pageParams: [0] });
    await flushPromises();
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    expect(dialog().text()).toContain('This item is no longer in this view.');
    expect(wrapper!.findAll('.sourcing-tile')).toHaveLength(2);
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1), media(3)]));
    await dialogButton('Reload latest').trigger('click');
    await flushPromises();
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    api.updateMedia.mockResolvedValue(failure(404));
    const notice = vi.spyOn(feedbackBus, 'notify');
    await dialog().get('form').trigger('submit');
    await flushPromises();
    expect(notice).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    expect(api.updateMedia).toHaveBeenLastCalledWith(ORG, 'media-2', 1, expect.objectContaining({ caption: 'My caption' }), expect.any(AbortSignal));
    notice.mockRestore();
    await dialog().get('button[aria-label="Close"]').trigger('click');
    resolveConfirmation(true);
    await flushPromises();
    expect(wrapper!.find('.sourcing-lightbox').exists()).toBe(false);
  });

  it.each([true, false])('closes the viewer only after a confirmed own delete (success=%s)', async succeeded => {
    render('editor', { view: 'inbox', supplierId: null });
    await flushPromises();
    await wrapper!.findAll('.sourcing-tile-button')[1]!.trigger('click');
    await flushPromises();
    api.deleteMedia.mockResolvedValue(succeeded ? ResultExtensions.ok(undefined) : failure(404));
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1), media(3)]));
    await dialogButton('Delete').trigger('click');
    expect(confirmation.value?.confirmLabel).toBe('Delete');
    resolveConfirmation(true);
    await flushPromises();
    expect(api.deleteMedia).toHaveBeenLastCalledWith(ORG, 'media-2', 1, expect.any(AbortSignal));
    expect(wrapper!.find('.sourcing-lightbox').exists()).toBe(!succeeded);
    if (succeeded) expect(wrapper!.findAll('.sourcing-tile')).toHaveLength(2);
    else expect(dialog().get('img').attributes('src')).toBe('/content/media-2');
  });

  it('asks before discarding media edits and preserves them when dismissal is declined', async () => {
    await editMedia();
    await dialog().get('button[aria-label="Close"]').trigger('click');
    expect(confirmation.value?.message).toBe('Discard unsaved changes?');
    resolveConfirmation(false);
    await flushPromises();
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    await dialog().trigger('keydown', { key: 'Escape' });
    expect(confirmation.value?.message).toBe('Discard unsaved changes?');
    resolveConfirmation(true);
    await flushPromises();
    expect(dialog().find('textarea').exists()).toBe(false);
    expect(dialog().text()).not.toContain('My caption');
    await dialog().get('button[aria-label="Close"]').trigger('click');
    await flushPromises();
    expect(wrapper!.find('.sourcing-lightbox').exists()).toBe(false);
  });

  it('keeps the viewed file and edit revision when refreshed media are inserted ahead of it', async () => {
    await editMedia();
    client!.setQueryData(inboxKey, { pages: [[media(9), media(1), media(2, { caption: 'Remote caption', revision: 2 }), media(3)]], pageParams: [0] });
    await flushPromises();
    expect(dialog().get('img').attributes('src')).toBe('/content/media-2');
    expect(dialog().text()).toContain('3 of 4');
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    api.updateMedia.mockResolvedValue(failure(409));
    await dialog().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateMedia).toHaveBeenLastCalledWith(ORG, 'media-2', 1, expect.objectContaining({ caption: 'My caption' }), expect.any(AbortSignal));
  });

  it('preserves media drafts after failed conflict reload and adopts the revision only after success', async () => {
    await editMedia();
    api.updateMedia.mockResolvedValue(failure(409));
    await dialog().get('form').trigger('submit');
    await flushPromises();
    api.listMedia.mockResolvedValue(failure(503));
    await dialogButton('Discard my edits and reload').trigger('click');
    await flushPromises();
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('My caption');
    api.listMedia.mockResolvedValue(ResultExtensions.ok([media(1), media(2, { caption: 'Latest caption', revision: 3 })]));
    await dialogButton('Discard my edits and reload').trigger('click');
    await flushPromises();
    expect((dialog().get('textarea').element as HTMLTextAreaElement).value).toBe('Latest caption');
    await dialog().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateMedia).toHaveBeenLastCalledWith(ORG, 'media-2', 3, expect.objectContaining({ caption: 'Latest caption' }), expect.any(AbortSignal));
  });
});
