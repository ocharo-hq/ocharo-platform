import type { StorageBroker } from '@wow-two-beta/fe/foundation/storage';
import { isRecoveryScope, validateRecovery, type DesignRecoveryRecord, type DesignRecoveryScope } from '@/domain/workspace/DesignRecovery';

/** The SDK broker contract with tab-local browser storage; never shares mutable recovery between tabs. */
const tabRecoveryBroker: StorageBroker = {
  read<T>(key: string): T | null {
    try { const raw = window.sessionStorage.getItem(key); return raw === null ? null : JSON.parse(raw) as T; } catch { return null; }
  },
  write<T>(key: string, value: T): boolean { try { window.sessionStorage.setItem(key, JSON.stringify(value)); return true; } catch { return false; /* Verified by readback below. */ } },
  remove(key: string): boolean { try { window.sessionStorage.removeItem(key); return true; } catch { return false; /* Verified by readback below. */ } },
};
/** Synchronous writes finish before reload; verified acknowledgements surface quota or blocked storage. */
export function createDesignRecoveryStorage(scope: DesignRecoveryScope, broker: StorageBroker = tabRecoveryBroker) {
  if (!isRecoveryScope(scope)) throw new Error('Local recovery requires an authenticated account and organization.');
  const key = `ocharo.design-recovery.v1:${scope.userId}:${scope.organizationId}:${scope.kind}`;
  const preference = `${key}:enabled`;
  const unavailable = () => new Error('Tab recovery could not be stored. Browser storage may be full or unavailable. Save your design or download its editable file.');
  function probe(): void {
    const probeKey = `${key}:probe:${crypto.randomUUID()}`, token = crypto.randomUUID();
    broker.write(probeKey, token);
    if (broker.read(probeKey) !== token) throw unavailable();
    broker.remove(probeKey);
    if (broker.read(probeKey) !== null) throw unavailable();
  }
  function read(): unknown { return broker.read(key); }
  function enabled(): boolean {
    const value = broker.read<{ schemaVersion?: number; userId?: string; organizationId?: string; kind?: string; enabled?: boolean }>(preference);
    return value?.schemaVersion === 1 && value.userId === scope.userId && value.organizationId === scope.organizationId && value.kind === scope.kind && value.enabled === true;
  }
  function setEnabled(value: boolean): void {
    if (value) broker.write(preference, { schemaVersion: 1, ...scope, enabled: true });
    else { probe(); broker.remove(preference); }
    if (enabled() !== value) throw unavailable();
  }
  function write(record: DesignRecoveryRecord, expectedToken: string | null): boolean {
    const checked = validateRecovery(record, scope), current = read() as Partial<DesignRecoveryRecord> | null;
    if (current !== null && current.token !== expectedToken) return false;
    broker.write(key, checked);
    const persisted = read() as Partial<DesignRecoveryRecord> | null;
    if (persisted?.token !== checked.token || JSON.stringify(persisted) !== JSON.stringify(checked)) throw unavailable();
    return true;
  }
  function remove(expected: unknown): boolean {
    probe();
    if (JSON.stringify(read()) !== JSON.stringify(expected)) return false;
    broker.remove(key);
    if (read() !== null) throw new Error('The tab recovery could not be removed. Browser storage is unavailable.');
    return true;
  }
  return { read, enabled, setEnabled, write, remove, probe };
}
export type DesignRecoveryStorage = ReturnType<typeof createDesignRecoveryStorage>;
