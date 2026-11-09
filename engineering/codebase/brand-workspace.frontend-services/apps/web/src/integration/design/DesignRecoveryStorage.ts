import { ResultExtensions, type AppError, type Result } from '@wow-two-beta/fe/foundation/results';
import type { StorageBroker } from '@wow-two-beta/fe/foundation/storage';

import { isRecoveryScope, validateRecovery, type DesignRecoveryRecord, type DesignRecoveryScope } from '@/domain/design/DesignRecovery';

/** Why tab recovery could not store, read back or remove; `message` is the display-safe English source. */
export type DesignRecoveryFailure = AppError;

/** The SDK broker contract with tab-local browser storage; never shares mutable recovery between tabs. */
const tabRecoveryBroker: StorageBroker = {
  read<T>(key: string): T | null {
    try { const raw = window.sessionStorage.getItem(key); return raw === null ? null : JSON.parse(raw) as T; } catch { return null; }
  },
  write<T>(key: string, value: T): boolean { try { window.sessionStorage.setItem(key, JSON.stringify(value)); return true; } catch { return false; /* Verified by readback below. */ } },
  remove(key: string): boolean { try { window.sessionStorage.removeItem(key); return true; } catch { return false; /* Verified by readback below. */ } },
};

/** @internal A failed recovery operation with its display-safe reason. */
function recoveryFailure(message: string): Result<never, DesignRecoveryFailure> {
  return ResultExtensions.fail({ type: 'unavailable', message });
}

/**
 * Synchronous writes finish before reload; verified acknowledgements surface quota or blocked storage as a failed
 * `Result`. A scope without an authenticated account and organization reads nothing and refuses every write.
 */
export function createDesignRecoveryStorage(scope: DesignRecoveryScope, broker: StorageBroker = tabRecoveryBroker) {
  const supported = isRecoveryScope(scope);
  const key = `ocharo.design-recovery.v1:${scope.userId}:${scope.organizationId}:${scope.kind}`;
  const preference = `${key}:enabled`;
  const unsupported = () => recoveryFailure('Local recovery requires an authenticated account and organization.');
  const unavailable = () => recoveryFailure('Tab recovery could not be stored. Browser storage may be full or unavailable. Save your design or download its editable file.');
  function probe(): Result<void, DesignRecoveryFailure> {
    if (!supported) return unsupported();
    const probeKey = `${key}:probe:${crypto.randomUUID()}`, token = crypto.randomUUID();
    broker.write(probeKey, token);
    if (broker.read(probeKey) !== token) return unavailable();
    broker.remove(probeKey);
    return broker.read(probeKey) === null ? ResultExtensions.ok(undefined) : unavailable();
  }
  function read(): unknown { return supported ? broker.read(key) : null; }
  function enabled(): boolean {
    if (!supported) return false;
    const value = broker.read<{ schemaVersion?: number; userId?: string; organizationId?: string; kind?: string; enabled?: boolean }>(preference);
    return value?.schemaVersion === 1 && value.userId === scope.userId && value.organizationId === scope.organizationId && value.kind === scope.kind && value.enabled === true;
  }
  function setEnabled(value: boolean): Result<void, DesignRecoveryFailure> {
    if (!supported) return unsupported();
    if (value) broker.write(preference, { schemaVersion: 1, ...scope, enabled: true });
    else {
      const probed = probe();
      if (!probed.ok) return probed;
      broker.remove(preference);
    }
    return enabled() === value ? ResultExtensions.ok(undefined) : unavailable();
  }
  /** Stores `record` unless another controller replaced the expected one; `false` reports that conflict. */
  function write(record: DesignRecoveryRecord, expectedToken: string | null): Result<boolean, DesignRecoveryFailure> {
    if (!supported) return unsupported();
    const checked = validateRecovery(record, scope), current = read() as Partial<DesignRecoveryRecord> | null;
    if (current !== null && current.token !== expectedToken) return ResultExtensions.ok(false);
    broker.write(key, checked);
    const persisted = read() as Partial<DesignRecoveryRecord> | null;
    if (persisted?.token !== checked.token || JSON.stringify(persisted) !== JSON.stringify(checked)) return unavailable();
    return ResultExtensions.ok(true);
  }
  /** Removes the recovery only while it is still `expected`; `false` reports that it changed. */
  function remove(expected: unknown): Result<boolean, DesignRecoveryFailure> {
    const probed = probe();
    if (!probed.ok) return probed;
    if (JSON.stringify(read()) !== JSON.stringify(expected)) return ResultExtensions.ok(false);
    broker.remove(key);
    return read() === null
      ? ResultExtensions.ok(true)
      : recoveryFailure('The tab recovery could not be removed. Browser storage is unavailable.');
  }
  return { read, enabled, setEnabled, write, remove, probe };
}
export type DesignRecoveryStorage = ReturnType<typeof createDesignRecoveryStorage>;
