import { effectScope, ref } from 'vue';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { memoryStorageBroker, type StorageBroker } from '@wow-two-beta/ui-vue/foundation/storage';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { useDesignRecovery } from '@/application/workspace/UseDesignRecovery';
import { createDesignRecoveryStorage } from '@/integration/workspace/DesignRecoveryStorage';
import { validateRecovery, type DesignRecoveryInput, type DesignRecoveryRecord, type DesignRecoveryScope } from '@/domain/workspace/DesignRecovery';
import { createCardTemplate } from '@/domain/workspace/Templates';
const scope: DesignRecoveryScope = { userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', organizationId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', kind: 'business-card' };
const input = (): DesignRecoveryInput => ({ title: 'Original card', snapshot: createCardTemplate(), source: { documentId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', revision: 4 } });
const record = (title = 'Old card'): DesignRecoveryRecord => ({ schemaVersion: 1, scope, token: crypto.randomUUID(), savedAt: '2026-09-30T00:00:00Z', ...input(), title });
const scopes: ReturnType<typeof effectScope>[] = [];
let broker: StorageBroker;
beforeEach(() => { broker = memoryStorageBroker(); });
afterEach(() => { scopes.splice(0).forEach(item => item.stop()); vi.restoreAllMocks(); });
function setup(value = input(), options: { authenticated?: boolean; userId?: string; organizationId?: string } = {}) {
  const state = effectScope(); scopes.push(state);
  return state.run(() => {
    const current = ref<DesignRecoveryInput | null>(value), restore = vi.fn(() => true);
    const access = { session: ref({ authenticated: options.authenticated ?? true, user: { id: options.userId ?? scope.userId } }), invalidated: ref(false) } as unknown as AccessControls;
    const factory = vi.fn((identity: DesignRecoveryScope) => createDesignRecoveryStorage(identity, broker));
    const controls = useDesignRecovery(access, options.organizationId ?? scope.organizationId, scope.kind, () => current.value, restore, factory);
    return { state, controls, current, restore, access, factory };
  })!;
}
describe('tab design recovery boundary', () => {
  it('persists no snapshot until explicitly enabled and verifies synchronous durability', () => {
    const { controls } = setup(), storage = createDesignRecoveryStorage(scope, broker);
    controls.capture(); expect(storage.read()).toBeNull(); controls.enable();
    expect(storage.enabled()).toBe(true); expect(storage.read()).toMatchObject(input());
    expect(controls.status.value).toContain('kept in this tab');
  });
  it('keeps independent tab brokers isolated even when their scopes and tokens match', () => {
    const firstTab = createDesignRecoveryStorage(scope, memoryStorageBroker());
    const secondTab = createDesignRecoveryStorage(scope, memoryStorageBroker());
    const original = record(); firstTab.setEnabled(true); secondTab.setEnabled(true);
    firstTab.write(original, null); secondTab.write(original, null);
    const newer = record('Second tab change'); secondTab.write(newer, original.token);
    firstTab.remove(original); expect(firstTab.read()).toBeNull(); expect(secondTab.read()).toEqual(newer);
  });
  it('never touches storage before authenticated identity is available', () => {
    const { controls, factory } = setup(input(), { authenticated: false });
    controls.enable(); controls.capture(); expect(factory).not.toHaveBeenCalled(); expect(controls.supported.value).toBe(false);
  });
  it('separates users and organizations and rejects a foreign record', () => {
    setup().controls.enable();
    expect(setup(input(), { userId: 'dddddddd-dddd-dddd-dddd-dddddddddddd' }).controls.enabled.value).toBe(false);
    expect(setup(input(), { organizationId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee' }).controls.available.value).toBeNull();
    expect(() => validateRecovery({ ...record(), scope: { ...scope, userId: 'dddddddd-dddd-dddd-dddd-dddddddddddd' } }, scope)).toThrow();
  });
  it('validates stored schemas and strips unexpected imported fields rather than retaining tokens', () => {
    const value = record(); Object.assign(value.snapshot, { csrfToken: 'never-store', 0: 'never-store-extra', sessionId: 'private', password: 'private' });
    const checked = validateRecovery(value, scope); expect(JSON.stringify(checked)).not.toContain('never-store');
    expect(() => validateRecovery({ ...value, schemaVersion: 2 }, scope)).toThrow();
    expect(() => validateRecovery({ ...value, source: { documentId: scope.userId, revision: 0 } }, scope)).toThrow();
  });
  it('bounds design size and stores image references rather than uploaded image bytes', () => {
    const value = record(); value.snapshot.pages[0]!.elements[0]!.src = `/api/organizations/${scope.organizationId}/media/cccccccc-cccc-cccc-cccc-cccccccccccc/content`;
    expect(validateRecovery(value, scope).snapshot.pages[0]!.elements[0]!.src).toContain('/media/');
    value.snapshot.pages[0]!.elements = Array.from({ length: 150 }, (_, index) => ({ id: `text-${index}`, type: 'text', label: 'Text', x: 0, y: 0, width: 10, height: 10, text: 'x'.repeat(10000) }));
    expect(() => validateRecovery(value, scope)).toThrow('local recovery limit');
  });
  it('keeps a pending previous design until restore or discard is explicit', () => {
    setup().controls.enable(); const previous = createDesignRecoveryStorage(scope, broker).read();
    const { controls } = setup({ ...input(), title: 'Unrelated new design' });
    expect(controls.available.value?.title).toBe('Original card'); controls.capture();
    expect(createDesignRecoveryStorage(scope, broker).read()).toEqual(previous);
  });
  it('stops keeping new changes without deleting the existing recovery', () => {
    const { controls, current } = setup(); controls.enable(); controls.disable();
    current.value = { ...input(), title: 'New edit' }; controls.capture();
    expect(createDesignRecoveryStorage(scope, broker).read()).toMatchObject({ title: 'Original card' });
    expect(controls.enabled.value).toBe(false); expect(controls.available.value?.title).toBe('Original card');
  });
  it('surfaces write quota failures and retains the previous durable snapshot', () => {
    const { controls, current } = setup(); controls.enable(); const persisted = createDesignRecoveryStorage(scope, broker).read();
    vi.spyOn(broker, 'write').mockImplementation(() => undefined);
    current.value = { ...input(), title: 'Cannot persist' }; controls.capture();
    expect(controls.error.value).toContain('full or unavailable'); expect(createDesignRecoveryStorage(scope, broker).read()).toEqual(persisted);
  });
  it('surfaces unavailable storage while removing a recovery instead of claiming removal', () => {
    const { controls } = setup(); controls.enable(); vi.spyOn(broker, 'write').mockImplementation(() => undefined);
    controls.discard(); expect(controls.error.value).toContain('unavailable'); expect(controls.hasStored.value).toBe(true);
  });
  it('cannot clear a newer controller checkpoint after its own server save completes', () => {
    const { controls } = setup(); controls.enable(); const token = controls.checkpoint();
    const storage = createDesignRecoveryStorage(scope, broker), newer = record('Newer controller edit');
    expect(storage.write(newer, token)).toBe(true); controls.saved(token);
    expect(storage.read()).toEqual(newer); expect(controls.available.value?.title).toBe('Newer controller edit');
  });
  it('clears only its successfully saved checkpoint and never queues a late resurrection', () => {
    const { controls } = setup(); controls.enable(); controls.saved(controls.checkpoint());
    expect(createDesignRecoveryStorage(scope, broker).read()).toBeNull(); expect(controls.hasStored.value).toBe(false);
  });
  it('retains recovery on disposal and blocks writes after session invalidation', () => {
    const { controls, state, current, access } = setup(); controls.enable(); const before = createDesignRecoveryStorage(scope, broker).read();
    access.invalidated.value = true; current.value = { ...input(), title: 'Wrong session' }; controls.capture(); state.stop(); controls.capture();
    expect(createDesignRecoveryStorage(scope, broker).read()).toEqual(before);
  });
  it('reports invalid stored records and discards only that record', () => {
    const storage = createDesignRecoveryStorage(scope, broker); storage.setEnabled(true);
    broker.write(`ocharo.design-recovery.v1:${scope.userId}:${scope.organizationId}:${scope.kind}`, { malformed: true });
    const { controls, current } = setup(); const open = current.value;
    expect(controls.error.value).toContain('invalid'); expect(controls.available.value).toBeNull();
    controls.discard(); expect(storage.read()).toBeNull(); expect(current.value).toEqual(open); expect(storage.enabled()).toBe(true);
  });
});
