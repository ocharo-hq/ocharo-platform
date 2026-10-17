import { chooseDeskValue } from './helpers/DeskSelect';
import { defineComponent, h, ref, computed } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import {confirmation,resolveConfirmation,feedbackBus} from '@/application/shared/UseFeedback';
import { useAccess } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { createWorkspaceApi } from '@/integration/workspace/WorkspaceApi';
import type { DesignDocument } from '@/domain/workspace/EditorModels';
import DesignEditorPage from '@/presentation/workspace/DesignEditorPage.vue';

const organizationId = '11111111-1111-4111-8111-111111111111';
const documentId = '22222222-2222-4222-8222-222222222222';
const doc: DesignDocument = {
  id: documentId, organizationId, title: 'Original English catalogue', kind: 'catalogue', revision: 1, archived: false,
  createdAtUtc: '2026-09-29T12:00:00Z', updatedAtUtc: '2026-09-29T12:00:00Z',
  snapshot: { schemaVersion: 1, kind: 'catalogue', templateId: 'fixture', locale: 'en', pages: ['cover', 'detail'].map(id => ({
    id, title: id === 'cover' ? 'Cover' : 'Garment details', width: 210, height: 297, background: '#ffffff',
    elements: [{id: 'heading', type: 'text', label: 'Page heading', x: 20, y: 20, width: 140, height: 20, text: 'Garment', fontSize: 5}],
  })) },
};
let wrapper: VueWrapper | undefined;
afterEach(() => { wrapper?.unmount(); vi.restoreAllMocks(); window.history.replaceState({}, '', '/'); });
async function render(document = documentId, page = 'detail', missing = false) {
  let getDesign!: ReturnType<typeof vi.fn<ReturnType<typeof createWorkspaceApi>['getDesign']>>;
  wrapper = mount(defineComponent({setup() {
    const access = useAccess({assign: vi.fn(), reload: vi.fn()});
    access.session.value = {authenticated: true, user: {id: 'operator', email: 'review@example.test', displayName: 'Review'}, sessionId: 's', csrfToken: 'c', setupRequired: false};
    vi.spyOn(access.api, 'getDesigns').mockResolvedValue(ResultExtensions.ok([doc]));
    getDesign = vi.fn<ReturnType<typeof createWorkspaceApi>['getDesign']>().mockResolvedValue(missing ? ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, {status: 404}))) : ResultExtensions.ok(doc));
    access.api.getDesign = getDesign;
    const workspace = {organization: ref({id: organizationId, role: 'owner'}), canEdit: computed(() => true)} as unknown as WorkspaceControls;
    return () => h(DesignEditorPage, {access, workspace, kind: 'catalogue', initialDocumentId: document || null, initialPageId: page || null});
  }}), {global: {stubs: {DesignCanvas: true, DesignPrintPreview: true, CataloguePublication: true, CatalogueSettingsPanel: true, DesignRecovery: true, DesignRevisionHistory: true, CatalogueTools: true, CardTools: true}}});
  await flushPromises();
  return {getDesign};
}

describe('saved catalogue admin entry', () => {
  it('opens a saved document and requested page under its organization', async () => {
    const {getDesign} = await render();
    expect(getDesign.mock.calls[0]?.slice(0, 2)).toEqual([organizationId, documentId]);
    expect(wrapper!.get<HTMLInputElement>('input[aria-label="Design name"]').element.value).toBe(doc.title);
    expect(wrapper!.get('.stage-toolbar').text()).toContain('Garment details');
    expect(window.location.search).toContain('page=detail');
    await chooseDeskValue(wrapper!, 'Select page element', 'heading');
    expect(wrapper!.get('.inspector-heading').text()).toContain('Page heading');
  });
  it('shows searchable saved designs before creating anything', async () => {
    await render('', '');
    const library = wrapper!.get('[aria-label="Saved designs"]');
    expect(library.text()).toContain(doc.title);
    await library.get('input[type="search"]').setValue('missing');
    expect(library.text()).toContain('No saved designs match');
    await library.get('input[type="search"]').setValue('English');
    const link = library.get('a.saved-design');
    expect(link.attributes('href')).toContain(`design=${documentId}`);
    await link.trigger('click'); await flushPromises();
    expect(wrapper!.get<HTMLInputElement>('input[aria-label="Design name"]').element.value).toBe(doc.title);
  });
  it('retains edits when switching to another saved document is declined', async () => {
    const {getDesign} = await render();
    await wrapper!.get('input[aria-label="Design name"]').setValue('Unsaved title');
    const toggle = wrapper!.findAll('button').find(button => button.text().includes('Saved designs'))!;
    await toggle.trigger('click');
    await wrapper!.get('[aria-label="Saved designs"] a.saved-design').trigger('click');expect(confirmation.value).not.toBeNull();resolveConfirmation(false);await flushPromises();
    expect(getDesign).toHaveBeenCalledTimes(1);
    expect(wrapper!.get<HTMLInputElement>('input[aria-label="Design name"]').element.value).toBe('Unsaved title');
  });
  it('reports missing pages and unavailable records without creating substitute designs', async () => {
    await render(documentId, 'removed-page');
    expect(wrapper!.text()).toContain('This page is no longer');
    expect(wrapper!.get('.stage-toolbar').text()).toContain('Cover');
    wrapper!.unmount(); wrapper = undefined;
    const toast=vi.spyOn(feedbackBus,'notify');
    await render(documentId, 'detail', true);
    expect(toast).toHaveBeenCalled();
    expect(wrapper!.text()).toContain('This catalogue is unavailable.');
    expect(wrapper!.find('input[aria-label="Design name"]').exists()).toBe(false);
  });
});
