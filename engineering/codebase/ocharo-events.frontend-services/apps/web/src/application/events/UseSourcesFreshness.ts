import { computed, inject, provide, type InjectionKey, type Ref, type ShallowRef } from 'vue';
import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';

import { useIdentitySession } from '@/application/common/UseIdentitySession';
import { createEventsPageClient, useIntakeFreshness } from '@/application/events/UseEvents';
import { OcharoEventsCatalog } from '@/domain/events/Events';

/** What every place that shows "Sources updated {time}" reads: the catalog's last freshness read and when it was made. */
export interface SourcesFreshnessState {
  readonly freshness: ShallowRef<IntakeFreshnessModel | null>;
  /** The instant of the last read, epoch milliseconds. */
  readonly now: Ref<number>;
}

const SourcesFreshnessKey: InjectionKey<SourcesFreshnessState> = Symbol('ocharo.events.sources-freshness');

/**
 * Starts the one freshness read (E24) for the session's organization and offers its state to everything below the frame:
 * the sidebar's foot and, on a phone, the feed's title show the same state, so the page polls once, not once for each. A
 * person without `events:read`, or in no organization, makes no read.
 */
export function provideSourcesFreshness(): SourcesFreshnessState {
  const { organization, canRead } = useIdentitySession();
  const client = computed(() => {
    const id = organization.value?.id.toLowerCase();
    return id && canRead.value ? createEventsPageClient(id) : null;
  });
  const state = useIntakeFreshness(() => client.value, OcharoEventsCatalog);
  provide(SourcesFreshnessKey, state);
  return state;
}

/** The state the frame offers; `null` outside the frame, where there is nothing to show. */
export function useSourcesFreshness(): SourcesFreshnessState | null {
  return inject(SourcesFreshnessKey, null);
}

export { SourcesFreshnessKey };
