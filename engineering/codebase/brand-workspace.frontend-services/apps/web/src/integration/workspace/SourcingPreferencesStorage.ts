import { localStorageStorageBroker, type StorageBroker } from '@wow-two-beta/ui-vue/foundation/storage';
import { defaultSourcingPreferences, isSourcingPreferences, type SourcingPreferences, type SourcingPreferenceScope } from '@/domain/workspace/SourcingPreferences';
import { isSourcingIdentifier } from '@/domain/workspace/Sourcing';

/** Account and organization scoped browser preferences, acknowledged only after exact readback. */
export function createSourcingPreferencesStorage(scope: SourcingPreferenceScope, broker: StorageBroker = localStorageStorageBroker) {
  const supported = isSourcingIdentifier(scope.userId) && isSourcingIdentifier(scope.organizationId);
  const key = `ocharo.sourcing-view.v1:${scope.userId}:${scope.organizationId}`;
  function read(): SourcingPreferences {
    const record = supported ? broker.read<{ schemaVersion?: number; userId?: string; organizationId?: string; preferences?: unknown }>(key) : null;
    return record?.schemaVersion === 1 && record.userId === scope.userId && record.organizationId === scope.organizationId && isSourcingPreferences(record.preferences)
      ? { start: record.preferences.start, order: record.preferences.order, visible: [...record.preferences.visible] } : defaultSourcingPreferences();
  }
  function save(preferences: SourcingPreferences): boolean {
    if (!supported || !isSourcingPreferences(preferences)) return false;
    // Pick known fields explicitly: no supplier data or arbitrary payload enters preference storage.
    const record = { schemaVersion: 1, ...scope, preferences: { start: preferences.start, order: preferences.order, visible: [...preferences.visible] }, token: crypto.randomUUID() };
    broker.write(key, record);
    return JSON.stringify(broker.read(key)) === JSON.stringify(record);
  }
  return { supported, read, save };
}
