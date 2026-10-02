import { requestConfirmation, notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed, onScopeDispose, ref, shallowRef, watch } from 'vue';
import { cloneSnapshot, validateSnapshot, type DesignDocument, type DesignKind, type DesignSnapshot, type DesignSummary } from '@/domain/workspace/EditorModels';
import { useDesignRecovery } from './UseDesignRecovery';
import type { DesignRecoverySource } from '@/domain/workspace/DesignRecovery';
import { failureMessage, type AccessControls } from './UseAccess';

/** Owns immutable edits, bounded history, and server-confirmed revision saves. */
export function useDesignDocument(access: AccessControls, organizationId: string, kind: DesignKind) {
  const { t } = useUiLocale();
  const snapshot = shallowRef<DesignSnapshot | null>(null);
  const title = ref('');
  const record = shallowRef<DesignDocument | null>(null);
  // A confirmed write receipt protects retry identity without replacing the editable projection.
  const receipt = shallowRef<{ id: string; revision: number } | null>(null);
  const documents = shallowRef<DesignSummary[]>([]);
  const error = ref('');
  const status = ref('');
  const loading = ref(false);
  const busy = ref(false);
  const baseline = ref('');
  const history = shallowRef<DesignSnapshot[]>([]);
  const future = shallowRef<DesignSnapshot[]>([]);
  const controller = new AbortController();
  const serialize = () => JSON.stringify({ title: title.value, snapshot: snapshot.value });
  let changing = false;
  let recoveryOrigin: DesignRecoverySource | null = null;
  const dirty = computed(() => snapshot.value !== null && serialize() !== baseline.value);
  const recovery = useDesignRecovery(access, organizationId, kind,
    () => snapshot.value && dirty.value ? { title: title.value, snapshot: snapshot.value,
      source: record.value ? { documentId: record.value.id, revision: record.value.revision } : recoveryOrigin } : null,
    async candidate => {
      if (!await create(candidate.snapshot, `${candidate.title || 'Untitled design'} · recovered copy`.slice(0, 160))) return false;
      recoveryOrigin = candidate.source; return true;
    });
  watch([snapshot, title], () => { if (!changing) recovery.capture(); }, { flush: 'sync' });
  watch(dirty, (value) => access.setDirty(value));
  watch(error, value => { if(value) notifyError(value); });
  onScopeDispose(() => { controller.abort(); access.setDirty(false); });
  async function list(): Promise<void> {
    loading.value = true;
    const result = await access.api.getDesigns(organizationId, kind, controller.signal);
    if (result.ok) documents.value = result.value;
    else error.value = failureMessage(result.failure);
    loading.value = false;
  }
  function replace(next: DesignSnapshot): void {
    if (busy.value || loading.value || access.busy.value) return;
    let checked: DesignSnapshot;
    try {
      checked = validateSnapshot(next);
      if (checked.kind !== kind) throw new Error('This file belongs in the other editor.');
    } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Invalid design edit.'; return; }
    if (snapshot.value) history.value = [...history.value.slice(-49), cloneSnapshot(snapshot.value)];
    snapshot.value = checked;
    future.value = [];
    status.value = ''; error.value = '';
  }
  async function create(next: DesignSnapshot, name: string): Promise<boolean> {
    if (busy.value || loading.value || access.busy.value) return false;
    if (dirty.value && !await requestConfirmation(t('Discard unsaved changes and create a new design?'))) return false;
    const checked = validateSnapshot(next);
    if (checked.kind !== kind) { error.value = 'This file belongs in the other editor.'; return false; }
    recovery.preserve(); changing = true; recoveryOrigin = null;
    record.value = null; receipt.value = null;
    snapshot.value = checked;
    title.value = name;
    baseline.value = '';
    history.value = []; future.value = []; error.value = ''; status.value = 'New draft · not saved';
    changing = false; recovery.capture(); return true;
  }
  async function load(id: string): Promise<boolean> {
    if (loading.value || busy.value || access.busy.value) return false;
    if (dirty.value && !await requestConfirmation(t('Discard unsaved changes and open this saved design?'))) return false;
    loading.value = true; error.value = '';
    const result = await access.api.getDesign(organizationId, id, controller.signal);
    if (result.ok && result.value.kind === kind) {
      recovery.preserve(); changing = true; recoveryOrigin = null;
      record.value = result.value; receipt.value = { id: result.value.id, revision: result.value.revision };
      snapshot.value = cloneSnapshot(result.value.snapshot);
      title.value = result.value.title;
      baseline.value = serialize(); history.value = []; future.value = [];
      status.value = t('Saved revision {revision}',{revision:result.value.revision}); changing = false;
    } else if (!result.ok) error.value = failureMessage(result.failure);
    else error.value = 'This design belongs in the other editor.';
    loading.value = false;
    return result.ok && result.value.kind === kind;
  }
  async function save(asCopy = false): Promise<void> {
    if (!snapshot.value || busy.value || loading.value || access.busy.value) return;
    if (!title.value.trim()) { error.value = 'Give your design a name.'; return; }
    const recoveryToken = recovery.checkpoint();
    busy.value = true; error.value = ''; status.value = '';
    const input = { kind, title: title.value.trim(), snapshot: cloneSnapshot(snapshot.value), expectedRevision: asCopy ? null : receipt.value?.revision ?? record.value?.revision ?? null };
    try {
      const result = await access.write((signal) => access.api.saveDesign(organizationId, asCopy ? null : receipt.value?.id ?? record.value?.id ?? null, input, signal), controller.signal);
      if (result.ok) {
        receipt.value = { id: result.value.id, revision: result.value.revision };
        status.value = t('Saved revision {revision}', { revision: result.value.revision });
        loading.value = true;
        const current = await access.api.getDesign(organizationId, result.value.id, controller.signal);
        loading.value = false;
        if (!current.ok) { error.value = 'Saved successfully. The current view could not refresh. Your local edits are preserved.'; await list(); return; }
        if (current.value.kind !== kind) { error.value = 'This design belongs in the other editor.'; return; }
        changing = true; recoveryOrigin = null;
        record.value = current.value; receipt.value = { id: current.value.id, revision: current.value.revision };
        title.value = current.value.title;
        snapshot.value = cloneSnapshot(current.value.snapshot);
        baseline.value = serialize(); status.value = t('Saved revision {revision}',{revision:current.value.revision});
        changing = false; recovery.saved(recoveryToken);
        await list();
      } else error.value = result.failure.status === 409
        ? 'A newer revision exists. Your edits are retained. Save a copy, or open the saved design to reload.'
        : failureMessage(result.failure);
    } finally { busy.value = false; loading.value = false; }
  }
  function undo(): void {
    const previous = history.value.at(-1);
    if (!previous || !snapshot.value || busy.value || loading.value || access.busy.value) return;
    future.value = [...future.value, snapshot.value];
    history.value = history.value.slice(0, -1); snapshot.value = previous;
  }
  function redo(): void {
    const next = future.value.at(-1);
    if (!next || !snapshot.value || busy.value || loading.value || access.busy.value) return;
    history.value = [...history.value, snapshot.value]; future.value = future.value.slice(0, -1); snapshot.value = next;
  }
  return { snapshot, title, record, receipt, documents, error, status, loading, busy, dirty, history, future, recovery, list, replace, create, load, save, undo, redo };
}
