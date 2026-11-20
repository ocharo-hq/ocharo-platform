import { computed, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Temporal } from 'temporal-polyfill';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';
import { useEventsRequest, type EventsClient, type EventsResult } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  calendarScopePills,
  calendarWindow,
  ocharoCalendarEvents,
  readCalendarScopes,
  writeCalendarScopes,
  type CalendarView,
  type CalendarWindow,
} from '@/domain/events/EventsCalendar';
import { OcharoEventsCatalog, ocharoClosedStages } from '@/domain/events/Events';

/** The events one read asks for at a time; Events serves at most 200. */
const calendarPageSize = 100;
/** The most events the calendar reads for one window. A month or a year of showcase events is far below it; a window with more shows the first 1000 by start. */
export const calendarEventCap = 1000;

/**
 * The calendar's state: the view and focused day (the viewer's `v-model:view` and `v-model:date`, a month view on today
 * by default) and the organization's events for the days that view shows. It reads through the client the feed reads
 * through (`listTracking` of the tracking feed, skipped and rejected left out, undated events not asked for), paging with
 * the cursor until the window is complete or `calendarEventCap` events are in, and reads again when the view or the
 * focused day leaves the days already read. The events read stay on screen while the next window loads.
 *
 * The scope pills (National, Central Asia, International) narrow the events read, in the browser, to those whose country
 * stands in a chosen scope; none chosen shows them all. The choice lives in the address (`?scope=national,central-asia`),
 * replaced in place so the back button does not step through each pill, and `scopePills` counts the loaded window's
 * events per scope whatever is chosen.
 */
export function useEventsCalendar(client: EventsClient, catalog: string = OcharoEventsCatalog) {
  const { uiLocale } = useUiLocale();
  const route = useRoute();
  const router = useRouter();
  const view = shallowRef<CalendarView>('month');
  const date = shallowRef(Temporal.Now.zonedDateTimeISO());
  /** True once the first read has answered, whether it succeeded or failed. */
  const loaded = shallowRef(false);

  const request = useEventsRequest(
    async (signal, window: CalendarWindow): EventsResult<readonly TrackedEventModel[]> => {
      const items: TrackedEventModel[] = [];
      let cursor: string | undefined;
      do {
        const page = await client.listTracking(
          catalog,
          {
            includeUndated: false,
            excludeStages: ocharoClosedStages,
            from: window.from,
            to: window.to,
            limit: calendarPageSize,
            ...(cursor ? { cursor } : {}),
          },
          signal,
        );
        if (!page.ok) return page;
        items.push(...page.value.items);
        cursor = page.value.nextCursor ?? undefined;
      } while (cursor && items.length < calendarEventCap);
      return ResultExtensions.ok(items.slice(0, calendarEventCap));
    },
  );

  const window = computed(() => calendarWindow(view.value, date.value.toPlainDate()));
  async function load(): Promise<void> {
    await request.run(window.value);
    loaded.value = true;
  }
  watch(
    () => `${window.value.from}/${window.value.to}`,
    () => void load(),
    { immediate: true },
  );

  const options = computed(() => ({ locale: uiLocale.value, zone: date.value.timeZoneId }));
  const loadedEvents = computed(() => request.value.value ?? []);

  /**
   * The scopes chosen, in Ocharo's order. They open on the address's `scope` query and follow it when it changes (the
   * back button, a link); a choice shows at once and is written to the address after it, so two quick clicks both count.
   */
  const scopes = shallowRef(readCalendarScopes(route.query.scope));
  watch(
    () => writeCalendarScopes(readCalendarScopes(route.query.scope)),
    (scope) => {
      scopes.value = readCalendarScopes(scope);
    },
  );
  /** Chooses the scopes and replaces the address's `scope` query with them; the other query keys stay. */
  function setScopes(next: readonly string[]): void {
    scopes.value = readCalendarScopes(next);
    void router.replace({ query: { ...route.query, scope: writeCalendarScopes(scopes.value) } });
  }
  /** One pill per scope with the events the loaded window draws in it, whatever is chosen. */
  const scopePills = computed(() => calendarScopePills(loadedEvents.value, options.value));

  /** The blocks to draw: the events read in the chosen scopes, in the browser's time zone and the interface language. */
  const events = computed(() => ocharoCalendarEvents(loadedEvents.value, options.value, scopes.value));

  return {
    view,
    date,
    events,
    scopes,
    scopePills,
    setScopes,
    failure: request.failure,
    loading: request.pending,
    loaded,
    reload: load,
  };
}
