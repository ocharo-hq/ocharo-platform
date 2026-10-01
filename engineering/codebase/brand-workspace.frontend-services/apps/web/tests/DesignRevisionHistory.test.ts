import { ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { DesignDocument } from '@/domain/workspace/EditorModels';
import type { DesignRevisionCopy } from '@/domain/workspace/DesignRevisions';
import { createCatalogueTemplate } from '@/domain/workspace/Templates';
import DesignRevisionHistory from '@/presentation/workspace/DesignRevisionHistory.vue';

const getRevisions = vi.fn(), getRevision = vi.fn();
const busy = ref(false), invalidated = ref(false);
const source: DesignDocument = {
  id: 'saved-design', organizationId: 'org', kind: 'catalogue', title: 'Ocharo edition', revision: 3,
  archived: false, createdAtUtc: '2026-09-29T10:00:00Z', updatedAtUtc: '2026-09-30T10:00:00Z',
  snapshot: createCatalogueTemplate('en'),
};
const metadata = (revision: number) => ({ revision, title: `Edition ${revision}`, archived: false, savedBy: 'Maftuna', savedAtUtc: '2026-09-30T10:00:00Z' });
const historical = (revision: number): DesignDocument => ({ ...source, title: `Edition ${revision}`, revision, snapshot: structuredClone(source.snapshot) });
const access = { api: { getDesignRevisions: getRevisions, getDesignRevision: getRevision }, busy, invalidated } as unknown as AccessControls;
let wrapper: VueWrapper;
afterEach(() => wrapper?.unmount());
beforeEach(() => {
  vi.clearAllMocks(); busy.value = false; invalidated.value = false;
  getRevisions.mockResolvedValue(ResultExtensions.ok([metadata(3), metadata(2), metadata(1)]));
  getRevision.mockImplementation((_org: string, _id: string, revision: number) => Promise.resolve(ResultExtensions.ok(historical(revision))));
});
function setup(canEdit = true): void {
  wrapper = mount(DesignRevisionHistory, { props: { access, organizationId: 'org', document: source, canEdit },
    global: { stubs: { DesignPrintPreview: { props: ['page'], template: '<div data-preview>{{ page.title }}</div>' } } } });
}
function button(label: string) { return wrapper.findAll('button').find(item => item.text() === label)!; }
async function expand(): Promise<void> { await wrapper.get('.history-toggle').trigger('click'); await flushPromises(); }
async function inspect(revision: number): Promise<void> {
  await wrapper.findAll('.revision-choice').find(item => item.text().startsWith(`Revision ${revision}`))!.trigger('click'); await flushPromises();
}
function pending<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done; }); return { promise, resolve }; }

describe('saved revision review and separate unsaved restoration', () => {
  it('loads only after expansion, previews individual historical pages, and never changes the current document', async () => {
    setup(); expect(getRevisions).not.toHaveBeenCalled(); expect(wrapper.find('.history-content').exists()).toBe(false);
    await expand(); expect(getRevisions).toHaveBeenCalledWith('org', source.id, expect.any(AbortSignal), 0, 20);
    expect(wrapper.text()).toContain('Revision 3 · loaded'); await inspect(2);
    expect(getRevision).toHaveBeenCalledWith('org', source.id, 2, expect.any(AbortSignal));
    expect(wrapper.text()).toContain('Revision 2 · EN · 16 pages'); expect(wrapper.text()).toContain('Maftuna');
    await wrapper.get('select').setValue(1); expect(wrapper.get('[data-preview]').text()).toBe(source.snapshot.pages[1]!.title);
    expect(source.revision).toBe(3); expect(wrapper.emitted('restore')).toBeUndefined();
  });
  it('emits a cloned unsaved copy only after an explicit restore click', async () => {
    setup(); await expand(); await inspect(1);
    await button('Restore as new unsaved copy').trigger('click');
    const copy = wrapper.emitted('restore')![0]![0] as DesignRevisionCopy;
    expect(copy).toEqual({ snapshot: source.snapshot, title: 'Edition 1', revision: 1 });
    copy.snapshot.pages[0]!.title = 'Edited copy';
    expect(source.snapshot.pages[0]!.title).not.toBe('Edited copy');
    expect(wrapper.get('[data-preview]').text()).not.toBe('Edited copy');
    expect(Object.keys(access.api)).toEqual(['getDesignRevisions', 'getDesignRevision']);
  });
  it('allows viewers to inspect but prevents restoring, including after permissions change', async () => {
    setup(false); await expand(); await inspect(2);
    expect(button('Restore as new unsaved copy').attributes('disabled')).toBeDefined();
    await button('Restore as new unsaved copy').trigger('click'); expect(wrapper.emitted('restore')).toBeUndefined();
    await wrapper.setProps({ canEdit: true }); expect(button('Restore as new unsaved copy').attributes('disabled')).toBeUndefined();
    busy.value = true; await flushPromises(); expect(button('Restore as new unsaved copy').attributes('disabled')).toBeDefined();
    busy.value = false; await wrapper.setProps({ disabled: true });
    expect(button('Restore as new unsaved copy').attributes('disabled')).toBeDefined();
  });
  it('deduplicates paginated history when another editor saves between requests', async () => {
    getRevisions.mockResolvedValueOnce(ResultExtensions.ok(Array.from({ length: 20 }, (_, index) => metadata(25 - index))))
      .mockResolvedValueOnce(ResultExtensions.ok([metadata(6), metadata(5), metadata(4)]));
    setup(); await expand(); await button('Load older revisions').trigger('click'); await flushPromises();
    expect(getRevisions.mock.calls[1]![3]).toBe(20); expect(wrapper.findAll('.revision-choice')).toHaveLength(22);
    expect(wrapper.findAll('.revision-choice').filter(item => item.text().startsWith('Revision 6'))).toHaveLength(1);
    expect(button('Load older revisions')).toBeUndefined();
  });
  it('aborts old previews and ignores late responses after a different revision is selected', async () => {
    const old = pending<ReturnType<typeof ResultExtensions.ok<DesignDocument>>>();
    getRevision.mockImplementationOnce(() => old.promise);
    setup(); await expand(); await inspect(1); const signal = getRevision.mock.calls[0]![3] as AbortSignal;
    await inspect(2); expect(signal.aborted).toBe(true); old.resolve(ResultExtensions.ok(historical(1))); await flushPromises();
    expect(wrapper.text()).toContain('Revision 2 · EN'); expect(wrapper.text()).not.toContain('Revision 1 · EN');
    await button('Restore as new unsaved copy').trigger('click'); expect((wrapper.emitted('restore')![0]![0] as DesignRevisionCopy).revision).toBe(2);
  });
  it('invalidates pending list and preview requests when organization or document changes', async () => {
    const old = pending<ReturnType<typeof ResultExtensions.ok<DesignDocument>>>();
    getRevision.mockImplementationOnce(() => old.promise); setup(); await expand(); await inspect(1);
    const signal = getRevision.mock.calls[0]![3] as AbortSignal;
    await wrapper.setProps({ organizationId: 'other', document: { ...source, id: 'other-design', organizationId: 'other' } });
    expect(signal.aborted).toBe(true); old.resolve(ResultExtensions.ok(historical(1))); await flushPromises();
    expect(getRevisions).toHaveBeenLastCalledWith('other', 'other-design', expect.any(AbortSignal), 0, 20);
    expect(wrapper.find('[data-preview]').exists()).toBe(false); expect(wrapper.emitted('restore')).toBeUndefined();
  });
  it('cancels hidden work on collapse and loads fresh history when expanded again', async () => {
    const old = pending<ReturnType<typeof ResultExtensions.ok<ReturnType<typeof metadata>[]>>>();
    getRevisions.mockImplementationOnce(() => old.promise); setup(); await expand();
    const signal = getRevisions.mock.calls[0]![2] as AbortSignal;
    await wrapper.get('.history-toggle').trigger('click'); expect(signal.aborted).toBe(true);
    old.resolve(ResultExtensions.ok([metadata(99)])); await flushPromises(); await expand();
    expect(wrapper.text()).not.toContain('Revision 99'); expect(getRevisions).toHaveBeenCalledTimes(2);
  });
  it('aborts requests on disposal and clears history after session invalidation', async () => {
    setup(); await expand(); await inspect(2); invalidated.value = true; await flushPromises();
    expect(wrapper.find('.history-content').exists()).toBe(false); expect(wrapper.emitted('restore')).toBeUndefined();
    invalidated.value = false; await expand();
    const signal = getRevisions.mock.calls.at(-1)![2] as AbortSignal; wrapper.unmount(); expect(signal.aborted).toBe(true);
  });
  it('retains the editor on load failures and offers a retry without emitting restoration', async () => {
    getRevision.mockResolvedValueOnce(ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 404 }))));
    setup(); await expand(); await inspect(2);
    expect(wrapper.get('[role=alert]').text()).toContain('unavailable'); expect(button('Restore as new unsaved copy')).toBeUndefined();
    await button('Retry revision').trigger('click'); await flushPromises(); expect(wrapper.text()).toContain('Revision 2 · EN');
    expect(source.revision).toBe(3); expect(wrapper.emitted('restore')).toBeUndefined();
  });
  it('rejects mismatched revision identity before exposing a restore action', async () => {
    getRevision.mockResolvedValueOnce(ResultExtensions.ok({ ...historical(1), organizationId: 'another-organization' }));
    setup(); await expand(); await inspect(1);
    expect(wrapper.get('[role=alert]').text()).toContain('different revision'); expect(button('Restore as new unsaved copy')).toBeUndefined();
  });
  it('reports rejected requests safely and permits a history retry', async () => {
    getRevisions.mockRejectedValueOnce(new Error('private server details'));
    setup(); await expand(); expect(wrapper.get('[role=alert]').text()).toContain('Unable to load saved revisions');
    expect(wrapper.text()).not.toContain('private server details');
    await button('Refresh history').trigger('click'); await flushPromises(); expect(wrapper.findAll('.revision-choice')).toHaveLength(3);
  });
  it('cannot request revision history for an unsaved design', async () => {
    setup(); await wrapper.setProps({ document: null }); expect(wrapper.get('.history-toggle').attributes('disabled')).toBeDefined();
    await wrapper.get('.history-toggle').trigger('click'); expect(getRevisions).not.toHaveBeenCalled();
  });
});
