import { effectScope } from 'vue';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { useAccess } from '@/application/workspace/UseAccess';
import { useDesignDocument } from '@/application/workspace/DocumentStore';
import { createDesignRecoveryStorage } from '@/integration/workspace/DesignRecoveryStorage';
import { createCardTemplate } from '@/domain/workspace/Templates';
import type { DesignDocument } from '@/domain/workspace/EditorModels';
const user = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', organization = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', document = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const identity = { userId: user, organizationId: organization, kind: 'business-card' as const };
const scopes: ReturnType<typeof effectScope>[] = [];
beforeEach(() => { sessionStorage.clear(); vi.spyOn(window, 'confirm').mockReturnValue(true); });
afterEach(() => { scopes.splice(0).forEach(scope => scope.stop()); vi.restoreAllMocks(); sessionStorage.clear(); });
function setup() {
  const scope = effectScope(); scopes.push(scope);
  return scope.run(() => {
    const access = useAccess({ assign: vi.fn(), reload: vi.fn() });
    access.session.value = { authenticated: true, user: { id: user, email: 'test@example.test', displayName: 'Test' }, sessionId: 'session', csrfToken: 'must-not-persist', setupRequired: false };
    const store = useDesignDocument(access, organization, 'business-card');
    vi.spyOn(access.api, 'getDesigns').mockResolvedValue(ResultExtensions.ok([]));
    return { access, store, scope };
  })!;
}
const saved = (): DesignDocument => ({ id: document, organizationId: organization, kind: 'business-card', title: 'Saved card', revision: 3, archived: false, createdAtUtc: '2026-09-30T00:00:00Z', updatedAtUtc: '2026-09-30T00:00:00Z', snapshot: createCardTemplate() });
describe('design recovery lifecycle', () => {
  it('captures an edit synchronously before session reload and restores only as a new unsaved copy', async () => {
    const first = setup(); vi.spyOn(first.access.api, 'getDesign').mockResolvedValue(ResultExtensions.ok(saved()));
    expect(await first.store.load(document)).toBe(true); first.store.recovery.enable();
    const changed = createCardTemplate(); changed.pages[0]!.background = '#abcdef'; first.store.replace(changed);
    first.store.title.value = 'Unsaved latest title'; first.access.invalidated.value = true; first.scope.stop();
    const persisted = createDesignRecoveryStorage(identity).read(); expect(persisted).toMatchObject({ title: 'Unsaved latest title', source: { documentId: document, revision: 3 } });
    expect(JSON.stringify(persisted)).not.toContain('must-not-persist');
    const second = setup(); expect(second.store.recovery.available.value?.title).toBe('Unsaved latest title');
    second.store.recovery.restore(); expect(second.store.record.value).toBeNull(); expect(second.store.dirty.value).toBe(true); expect(second.store.snapshot.value).toEqual(changed);
    const save = vi.spyOn(second.access.api, 'saveDesign').mockResolvedValue(ResultExtensions.ok({ ...saved(), id: 'dddddddd-dddd-dddd-dddd-dddddddddddd', snapshot: changed }));
    await second.store.save(); expect(save.mock.calls[0]?.[1]).toBeNull(); expect(save.mock.calls[0]?.[2].expectedRevision).toBeNull();
    expect(createDesignRecoveryStorage(identity).read()).toBeNull();
  });
  it('clears an active backup when undo restores the already-saved version', async () => {
    const { store, access } = setup(); vi.spyOn(access.api, 'getDesign').mockResolvedValue(ResultExtensions.ok(saved()));
    await store.load(document); store.recovery.enable();
    const changed = createCardTemplate(); changed.pages[0]!.background = '#abcdef'; store.replace(changed);
    expect(createDesignRecoveryStorage(identity).read()).not.toBeNull(); store.undo();
    expect(store.dirty.value).toBe(false); expect(createDesignRecoveryStorage(identity).read()).toBeNull();
    expect(store.recovery.status.value).toContain('No unsaved edits remain');
  });
  it('preserves old recovery when another saved design is opened, then blocks automatic replacement', async () => {
    const { store, access } = setup(); store.create(createCardTemplate(), 'Unsaved first'); store.recovery.enable();
    vi.spyOn(access.api, 'getDesign').mockResolvedValue(ResultExtensions.ok(saved())); await store.load(document);
    store.title.value = 'Second design edit'; expect(store.recovery.available.value?.title).toBe('Unsaved first');
    expect(createDesignRecoveryStorage(identity).read()).toMatchObject({ title: 'Unsaved first' });
    store.recovery.discard(); expect(store.title.value).toBe('Second design edit'); expect(store.dirty.value).toBe(true);
  });
  it('returns false for cancelled navigation or locked editing without altering input', async () => {
    const { store, access } = setup(); store.create(createCardTemplate(), 'Keep this');
    vi.mocked(window.confirm).mockReturnValue(false); expect(store.create(createCardTemplate(), 'Other')).toBe(false); expect(await store.load(document)).toBe(false);
    store.loading.value = true; expect(store.create(createCardTemplate(), 'Other')).toBe(false);
    const changed = createCardTemplate(); changed.pages[0]!.background = '#abcdef'; store.replace(changed);
    expect(store.title.value).toBe('Keep this'); expect(store.snapshot.value?.pages[0]?.background).not.toBe('#abcdef');
    store.loading.value = false; access.invalidated.value = true; expect(store.create(changed, 'Other')).toBe(false);
  });
});
