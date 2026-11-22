import { computed, shallowRef, watch } from 'vue';
import { Temporal } from 'temporal-polyfill';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';
import { useEventsRequest, type EventsClient, type EventsResult } from '@wow-two-beta/fe-events/kit';

import { useAddressFilters } from '@/application/events/UseAddressFilters';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  calendarFacetCounts,
  filterCalendarEvents,
  readCalendarFilters,
  writeCalendarFilters,
} from '@/domain/events/CalendarFilters';
import { sharedFilterQueryKeys } from '@/domain/events/EventFilterQuery';
import { calendarWindow, type CalendarView, type CalendarWindow } from '@/domain/events/EventsCalendar';
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
 * The filters (region, kind, stage, city, source and a search text; `CalendarFilters`) narrow the events read, in the
 * browser, to those that match; none chosen shows them all. The choice lives in the address under the keys the feed's
 * filters use, so one address means the same filters on both pages
 * (`?regions=national&kinds=runway&kinds=market&stages=shortlisted&cities=tashkent&sources=afisha&q=silk`), replaced in
 * place so the back button does not step through each click, and `facets` counts the loaded window for the filter card:
 * each facet's choices counted with every other facet's choices applied but not its own, the events matching, and the
 * events the window draws. The feed's counts come from the server for its upcoming range; these describe this window,
 * and the two are never mixed.
 */
export function useEventsCalendar(client: EventsClient, catalog: string = OcharoEventsCatalog) {
  const { uiLocale } = useUiLocale();
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

  /** The filters chosen, held in the address (`useAddressFilters`). */
  const { filters, setFilters } = useAddressFilters({
    keys: sharedFilterQueryKeys,
    read: readCalendarFilters,
    write: writeCalendarFilters,
  });
  /** The filter card's numbers for the loaded window, whatever is chosen. */
  const facets = computed(() => calendarFacetCounts(loadedEvents.value, options.value, filters.value));

  /** The blocks to draw: the events read that match the filters, in the browser's time zone and the interface language. */
  const events = computed(() => filterCalendarEvents(loadedEvents.value, options.value, filters.value));

  return {
    view,
    date,
    events,
    filters,
    setFilters,
    facets,
    failure: request.failure,
    loading: request.pending,
    loaded,
    reload: load,
  };
}
