import { ref, type Ref } from 'vue';

import { localStorageStorageBroker, type StorageBroker } from '@wow-two-beta/fe/foundation/storage';

/** Where this browser remembers the Overview view. @internal */
const OverviewViewKey = 'brand-workspace.overview-view.v1';

/**
 * Remembers only the aesthetic Overview view in this browser, never organization or business data. A blocked or full
 * storage still changes the view for this visit; `select` then reports that the choice was not remembered.
 */
export function useOverviewView<T extends string>(views: readonly T[], initial: T, broker: StorageBroker = localStorageStorageBroker) {
  const stored = broker.read<unknown>(OverviewViewKey);
  const view = ref(views.find((item) => item === stored) ?? initial) as Ref<T>;
  /** Shows `next` and returns whether this browser remembered it. */
  function select(next: T): boolean {
    view.value = next;
    return broker.write(OverviewViewKey, next);
  }
  return { view, select };
}
