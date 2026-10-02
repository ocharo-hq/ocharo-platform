import { effectScope } from 'vue';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { useAccess } from '@/application/workspace/UseAccess';
import { useDesignDocument } from '@/application/workspace/DocumentStore';
import { createCardTemplate } from '@/domain/workspace/Templates';
import type { DesignDocument } from '@/domain/workspace/EditorModels';

const scopes: ReturnType<typeof effectScope>[] = [];
afterEach(() => { scopes.splice(0).forEach(scope => scope.stop()); vi.restoreAllMocks(); });
function setup() {
  const scope = effectScope(); scopes.push(scope);
  return scope.run(() => {
    const access = useAccess({assign:vi.fn(),reload:vi.fn()});
    const store = useDesignDocument(access, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'business-card');
    vi.spyOn(access.api, 'getDesigns').mockResolvedValue(ResultExtensions.ok([]));
    return {access,store};
  })!;
}
function saved(snapshot: DesignDocument['snapshot']): DesignDocument {
  return {id:'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',organizationId:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',kind:'business-card',title:'Card',snapshot,revision:1,archived:false,createdAtUtc:'2026-09-27T00:00:00Z',updatedAtUtc:'2026-09-27T00:00:00Z'};
}
describe('design save lifecycle', () => {
  it('clears dirty only after server confirmation and uses the saved revision on later writes', async () => {
    const {access,store}=setup(), seed=createCardTemplate();
    const save=vi.spyOn(access.api,'saveDesign').mockResolvedValue(ResultExtensions.ok(saved(seed)));
    const read=vi.spyOn(access.api,'getDesign').mockResolvedValue(ResultExtensions.ok(saved(seed)));
    store.create(seed,'Card'); expect(store.dirty.value).toBe(true);
    await store.save(); expect(store.dirty.value).toBe(false); expect(store.record.value?.revision).toBe(1);
    const next=createCardTemplate(); next.pages[0]!.background='#aaaaaa'; store.replace(next);
    save.mockResolvedValue(ResultExtensions.ok({...saved(next),revision:2}));
    read.mockResolvedValue(ResultExtensions.ok({...saved(next),revision:2}));
    await store.save(); expect(save.mock.calls[1]?.[1]).toBe(saved(seed).id);
    expect(save.mock.calls[1]?.[2].expectedRevision).toBe(1); expect(store.record.value?.revision).toBe(2);
  });
  it('retains unsaved edits after a conflicting save and permits a distinct copy', async () => {
    const {access,store}=setup(), seed=createCardTemplate();
    vi.spyOn(access.api,'getDesign').mockResolvedValue(ResultExtensions.ok(saved(seed)));
    await store.load(saved(seed).id);
    const next=createCardTemplate(); next.pages[0]!.background='#abcdef'; store.replace(next);
    const save=vi.spyOn(access.api,'saveDesign').mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:409}))));
    await store.save(); expect(store.dirty.value).toBe(true); expect(store.snapshot.value).toEqual(next);
    expect(store.error.value).toContain('newer revision'); expect(store.record.value?.revision).toBe(1);
    save.mockResolvedValue(ResultExtensions.ok({...saved(next),id:'cccccccc-cccc-cccc-cccc-cccccccccccc'}));
    vi.mocked(access.api.getDesign).mockResolvedValue(ResultExtensions.ok({...saved(next),id:'cccccccc-cccc-cccc-cccc-cccccccccccc'}));
    await store.save(true); expect(save.mock.calls[1]?.[1]).toBeNull(); expect(save.mock.calls[1]?.[2].expectedRevision).toBeNull();
    expect(store.dirty.value).toBe(false);
  });
  it('undo and redo restore a complete geometry transaction', () => {
    const {store}=setup(), seed=createCardTemplate(); store.create(seed,'Card');
    const next=createCardTemplate(); next.pages[0]!.elements[0]!.x=12;
    store.replace(next); store.undo(); expect(store.snapshot.value).toEqual(seed);
    store.redo(); expect(store.snapshot.value).toEqual(next);
  });
});


describe('independent saved-design projection', () => {
  it('keeps edits and grid unchanged while the write is pending, then waits for the GET projection', async () => {
    const {access,store}=setup(), seed=createCardTemplate();
    await store.create(seed,'Card');
    let acknowledge!: (value: ReturnType<typeof ResultExtensions.ok<DesignDocument>>) => void;
    let project!: (value: ReturnType<typeof ResultExtensions.ok<DesignDocument>>) => void;
    vi.spyOn(access.api,'saveDesign').mockImplementation(() => new Promise(resolve => { acknowledge=resolve; }));
    const read=vi.spyOn(access.api,'getDesign').mockImplementation(() => new Promise(resolve => { project=resolve; }));
    const pending=store.save(); await Promise.resolve();
    expect(store.busy.value).toBe(true); expect(store.loading.value).toBe(false);
    expect(read).not.toHaveBeenCalled(); expect(store.record.value).toBeNull(); expect(store.dirty.value).toBe(true);
    const writePayload=createCardTemplate(); writePayload.pages[0]!.background='#111111';
    acknowledge(ResultExtensions.ok(saved(writePayload)));
    await Promise.resolve(); await Promise.resolve(); await Promise.resolve();
    expect(read).toHaveBeenCalledTimes(1); expect(store.loading.value).toBe(true);
    expect(store.record.value).toBeNull(); expect(store.snapshot.value).toEqual(seed);
    const authoritative=createCardTemplate(); authoritative.pages[0]!.background='#222222';
    project(ResultExtensions.ok(saved(authoritative))); await pending;
    expect(store.snapshot.value).toEqual(authoritative); expect(store.dirty.value).toBe(false);
  });
  it('retains confirmed identity and the local draft when the read fails, then retries an update rather than creating twice', async () => {
    const {access,store}=setup(), seed=createCardTemplate(); await store.create(seed,'Card');
    const save=vi.spyOn(access.api,'saveDesign').mockResolvedValue(ResultExtensions.ok(saved(seed)));
    vi.spyOn(access.api,'getDesign').mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:503}))));
    await store.save(); expect(store.receipt.value).toEqual({id:saved(seed).id,revision:1});
    expect(store.snapshot.value).toEqual(seed); expect(store.dirty.value).toBe(true); expect(store.record.value).toBeNull();
    expect(store.error.value).toContain('Saved successfully');
    save.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:503}))));
    await store.save(); expect(save.mock.calls[1]?.[1]).toBe(saved(seed).id);
    expect(save.mock.calls[1]?.[2].expectedRevision).toBe(1); expect(store.snapshot.value).toEqual(seed);
  });
});
