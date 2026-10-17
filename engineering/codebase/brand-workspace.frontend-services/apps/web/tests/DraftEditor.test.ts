import { feedbackBus } from '@/application/shared/UseFeedback';
import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { Draft } from '@/domain/workspace/Models';
import DraftEditorPage from '@/presentation/workspace/DraftEditorPage.vue';

const orgId = '10000000-0000-0000-0000-000000000001';
const draft: Draft = {
  id: '10000000-0000-0000-0000-000000000002',
  brandId: 'brand',
  title: 'Original title',
  revision: 3,
  status: 'draft',
  updatedAtUtc: '2026-09-26T00:00:00Z',
  caption: 'Original caption',
  language: 'en',
  destination: 'Manual Telegram sharing',
  sourceNotes: 'Known source',
  media: [],
  requestedReviewRevision: null,
  approval: null,
};
const notices = vi.fn();
beforeEach(() => { notices.mockClear(); vi.spyOn(feedbackBus, 'notify').mockImplementation(notices); });
let wrapper: VueWrapper;
afterEach(() => wrapper?.unmount());

/** Mounts the real editor and SDK controls with bounded command seams. */
function mountEditor(value: Draft, role: 'owner' | 'viewer' = 'owner') {
  const workspace = {
    draft: ref(value),
    organization: ref({ id: orgId, name: 'Brand organization', slug: 'brand', role }),
    brands: ref([{ id: 'brand', organizationId: orgId, name: 'Brand', timeZone: 'UTC' }]),
    media: ref([]),
    canEdit: computed(() => role === 'owner'),
    canApprove: computed(() => role === 'owner'),
    saveDraft: vi.fn(),
    transition: vi.fn(),
    createExport: vi.fn(),
  };
  const access = {
    busy: ref(false),
    dirty: ref(false),
    navigate: vi.fn(),
    setDirty: vi.fn(),
    session: ref({ user: { id: 'owner', displayName: 'Test operator' } }),
  };
  wrapper = mount(DraftEditorPage, {
    props: {
      workspace: workspace as unknown as WorkspaceControls,
      access: access as unknown as AccessControls,
    },
  });
  return { workspace, access };
}

function button(label: string) {
  const element = wrapper.findAll('button').find((candidate) => candidate.text() === label);
  if (!element) throw new Error(`Missing button ${label}`);
  return element;
}

describe('revision editing', () => {
  it.each([409, 0])('retains the dirty buffer after a failed save (%s)', async (status) => {
    const { workspace } = mountEditor({ ...draft });
    workspace.saveDraft.mockResolvedValue(
      ResultExtensions.fail(
        ApiFailureFactory.create(status ? 'http' : 'transport', { status, headers: new Headers() }),
      ),
    );
    await wrapper.get('textarea').setValue('My unsaved caption');
    expect(button('Request review').attributes('disabled')).toBeDefined();
    await wrapper.get('form').trigger('submit');
    await flushPromises();
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('My unsaved caption');
    expect(wrapper.text()).toContain('Unsaved changes');
    expect(workspace.saveDraft).toHaveBeenCalledWith(
      expect.objectContaining({ caption: 'My unsaved caption' }),
      3,
      expect.any(AbortSignal),
    );
    expect(notices).toHaveBeenCalledWith(expect.objectContaining({ description: expect.stringContaining(status ? 'This record changed' : 'could not complete') }));
    expect(notices).toHaveBeenCalledTimes(1);
  });

  it('requests review for the exact saved revision', async () => {
    const { workspace } = mountEditor({ ...draft });
    workspace.transition.mockResolvedValue(
      ResultExtensions.ok({ ...draft, status: 'in-review', requestedReviewRevision: 3 }),
    );
    await button('Request review').trigger('click');
    await flushPromises();
    expect(workspace.transition).toHaveBeenCalledWith('request-review', 3, '', expect.any(AbortSignal));
    expect(wrapper.text()).toContain('Review requested for revision 3');
    expect(button('Approve revision 3').exists()).toBe(true);
  });

  it('reuses the export idempotency key on an uncertain retry and offers only its protected download', async () => {
    const { workspace } = mountEditor({
      ...draft,
      status: 'approved',
      approval: {
        id: 'approval',
        revision: 3,
        approvedAtUtc: '2026-09-26T00:00:00Z',
        approvedBy: 'owner',
      },
    });
    const downloadUrl = `/api/organizations/${orgId}/exports/export-id/download`;
    workspace.createExport
      .mockResolvedValueOnce(ResultExtensions.fail(ApiFailureFactory.create('transport')))
      .mockResolvedValueOnce(
        ResultExtensions.ok({
          id: 'export-id',
          draftId: draft.id,
          revision: 3,
          createdAtUtc: '2026-09-26T00:00:00Z',
          downloadUrl,
          packageKind: 'manual-content-package',
          mediaIncluded: true,
          publicationStatus: 'unverified',
        }),
      );
    await button('Prepare ZIP').trigger('click');
    await flushPromises();
    await button('Prepare ZIP').trigger('click');
    await flushPromises();
    expect(workspace.createExport.mock.calls[0]![1]).toBe(workspace.createExport.mock.calls[1]![1]);
    expect(wrapper.get('a').attributes('href')).toBe(downloadUrl);
    expect(wrapper.text()).toContain('Publication remains unverified');
    expect(wrapper.text()).toContain('Test operator (you)');
  });

  it('never offers an export download from another origin', async () => {
    const { workspace } = mountEditor({ ...draft, status: 'approved' });
    workspace.createExport.mockResolvedValue(
      ResultExtensions.ok({ id: 'export-id', revision: 3, downloadUrl: 'https://unexpected.example.test/download' }),
    );
    await button('Prepare ZIP').trigger('click');
    await flushPromises();
    expect(wrapper.find('a').exists()).toBe(false);
    expect(notices).toHaveBeenCalledWith(expect.objectContaining({ description: 'The service returned an unexpected download location.' }));
    expect(wrapper.text()).toContain('Download unavailable');
  });

  it('keeps viewer controls read-only', () => {
    mountEditor({ ...draft }, 'viewer');
    expect(wrapper.findAll('button').some((item) => /Save revision|Request review|Prepare ZIP/.test(item.text()))).toBe(
      false,
    );
    expect(wrapper.get('textarea').attributes('disabled')).toBeDefined();
  });
});
