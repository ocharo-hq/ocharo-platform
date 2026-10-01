import { computed, onScopeDispose, ref, shallowRef } from 'vue';
import type { DesignKind } from '@/domain/workspace/EditorModels';
import { isRecoveryScope, validateRecovery, type DesignRecoveryInput, type DesignRecoveryRecord, type DesignRecoveryScope } from '@/domain/workspace/DesignRecovery';
import { createDesignRecoveryStorage, type DesignRecoveryStorage } from '@/integration/workspace/DesignRecoveryStorage';
import type { AccessControls } from './UseAccess';

/** Opt-in, account-scoped recovery of unsaved input; restoration never overwrites its source document. */
export function useDesignRecovery(access: AccessControls, organizationId: string, kind: DesignKind,
  current: () => DesignRecoveryInput | null, restoreCopy: (value: DesignRecoveryRecord) => boolean | Promise<boolean>,
  factory: (scope: DesignRecoveryScope) => DesignRecoveryStorage = createDesignRecoveryStorage) {
  const scope: DesignRecoveryScope = { userId: access.session.value?.user?.id ?? '', organizationId, kind };
  const authorized = () => access.session.value?.authenticated === true && access.session.value.user?.id === scope.userId && !access.invalidated.value;
  const supported = computed(() => isRecoveryScope(scope) && authorized());
  const enabled = ref(false), available = shallowRef<DesignRecoveryRecord | null>(null), hasStored = ref(false);
  const status = ref('Off in this tab'), error = ref('');
  let storage: DesignRecoveryStorage | undefined, observed: unknown = null, ownedToken: string | null = null, pending = false, disposed = false;
  function refresh(): void {
    if (!storage || !supported.value || disposed) return;
    observed = storage.read(); hasStored.value = observed !== null;
    pending = hasStored.value; ownedToken = null; available.value = null;
    if (observed !== null) available.value = validateRecovery(observed, scope);
    status.value = pending ? 'A recovery is waiting in this tab. Restore or discard it before keeping new edits.' : enabled.value ? 'Ready to keep unsaved changes in this tab' : 'Off in this tab';
  }
  function fail(cause: unknown): void { error.value = cause instanceof Error ? cause.message : 'Tab recovery is unavailable. Download the editable file to keep your changes.'; status.value = 'Tab recovery needs attention'; }
  if (supported.value) {
    storage = factory(scope);
    try { enabled.value = storage.enabled(); refresh(); } catch (cause) { fail(cause); }
  }
  function capture(): void {
    if (!storage || !enabled.value || !supported.value || pending || disposed) return;
    try {
      if (!storage.enabled()) { storage.probe(); enabled.value = false; refresh(); return; }
      const input = current();
      if (!input) {
        if (ownedToken) { saved(ownedToken); if (!hasStored.value) status.value = 'No unsaved edits remain. Tab recovery cleared.'; }
        return;
      }
      const record = validateRecovery({ schemaVersion: 1, scope, token: crypto.randomUUID(), savedAt: new Date().toISOString(), ...input }, scope);
      if (!storage.write(record, ownedToken)) { refresh(); return; }
      ownedToken = record.token; observed = record; hasStored.value = true; error.value = ''; status.value = 'Unsaved changes kept in this tab';
    } catch (cause) { fail(cause); }
  }
  function enable(): void {
    if (!storage || !supported.value || disposed) return;
    try { storage.setEnabled(true); enabled.value = true; error.value = ''; if (!ownedToken) refresh(); capture(); }
    catch (cause) { fail(cause); }
  }
  function disable(): void {
    if (!storage || !supported.value || disposed) return;
    try { storage.setEnabled(false); enabled.value = false; error.value = ''; refresh(); }
    catch (cause) { fail(cause); }
  }
  function discard(): void {
    if (!storage || !supported.value || disposed) return;
    try {
      // Synchronous comparison and removal share this tab; a newer controller checkpoint stays intact.
      if (!storage.remove(observed)) { refresh(); return; }
      observed = null; ownedToken = null; pending = false; available.value = null; hasStored.value = false; error.value = '';
      status.value = 'Tab recovery removed. The open design is unchanged.';
    } catch (cause) { fail(cause); }
  }
  async function restore(): Promise<void> {
    if (!storage || !supported.value || !available.value || disposed) return;
    try {
      if (JSON.stringify(storage.read()) !== JSON.stringify(observed)) { refresh(); return; }
      const candidate = validateRecovery(available.value, scope);
      if (!await restoreCopy(candidate) || disposed) return;
      ownedToken = candidate.token; pending = false; available.value = null; error.value = '';
      status.value = 'Recovered as a new unsaved copy. Save it to keep a server revision.';
      capture();
    } catch (cause) { fail(cause); }
  }
  function saved(token: string | null): void {
    if (!storage || !supported.value || !token || disposed) return;
    try {
      const latest = storage.read() as Partial<DesignRecoveryRecord> | null;
      if (latest?.token !== token) { refresh(); return; }
      if (!storage.remove(latest)) { refresh(); return; }
      ownedToken = null; observed = null; pending = false; available.value = null; hasStored.value = false; error.value = '';
      status.value = enabled.value ? 'Saved design confirmed. Tab recovery cleared.' : 'Off in this tab';
    } catch (cause) { fail(cause); }
  }
  function preserve(): void {
    if (!storage || !supported.value || disposed || !ownedToken) return;
    try { refresh(); } catch (cause) { fail(cause); }
  }
  onScopeDispose(() => { disposed = true; });
  return { supported, enabled, available, hasStored, status, error, enable, disable, discard, restore, capture, saved, preserve, checkpoint: () => ownedToken };
}
export type DesignRecoveryControls = ReturnType<typeof useDesignRecovery>;
