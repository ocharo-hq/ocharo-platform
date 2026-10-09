import { ref } from 'vue';
import type { AccessControls } from './UseAccess';
import { createSourcingPreferencesStorage } from '@/integration/workspace/SourcingPreferencesStorage';
import type { SourcingPreferences } from '@/domain/workspace/SourcingPreferences';
import { notifyError } from '@/application/shared/UseFeedback';

/** Personal presentation changes are acknowledged only within the originating authenticated scope. */
export function useSourcingPreferences(access: AccessControls, organizationId: () => string) {
  const scope = { userId: access.session.value?.user?.id ?? '', organizationId: organizationId() };
  const storage = createSourcingPreferencesStorage(scope);
  const preferences = ref(storage.read());
  function canChange(): boolean {
    return storage.supported && access.session.value?.authenticated === true && !access.invalidated?.value && !access.checking?.value &&
      !access.busy.value && !access.dirty.value && access.session.value.user?.id === scope.userId && organizationId() === scope.organizationId;
  }
  function save(value: SourcingPreferences): boolean {
    if (!canChange()) return false;
    if (!storage.save(value)) {
      notifyError('View settings could not be saved. Browser storage may be unavailable.');
      return false;
    }
    preferences.value = storage.read();
    return true;
  }
  return { preferences, supported: storage.supported, canChange, save };
}
