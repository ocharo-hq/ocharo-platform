<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { EventCalendarViewer, type EventCalendarViewerEvent } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import {
  FacetKind,
  FacetPlacement,
  type FacetDefinition,
  type FacetSelection,
} from '@wow-two-beta/fe/presentation/forms';
import { eventsFacetBarLabels, eventsText, FailureNotice } from '@wow-two-beta/fe-events/kit';

import { createEventsPageClient } from '@/application/events/UseEvents';
import { useEventsCalendar } from '@/application/events/UseEventsCalendar';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  allScopesValue,
  calendarSelection,
  withCalendarSelection,
  type CalendarFacetKey,
} from '@/domain/events/CalendarFilters';
import { eventsPath } from '@/domain/events/Events';

import EventFilterCard from '../EventFilterCard.vue';

/**
 * The organization's events on a calendar (the fe sdk's `EventCalendarViewer`): each event with a known day as a block
 * on it, coloured by where Ocharo stands with it, over the same tracking feed the feed page reads. An event known only
 * to its month or year has no day and is not drawn. Opening a block goes to the event's page in the feed. The viewer
 * offers every view it has: the fe sdk pinned now has month, week, day and agenda, and its re-pin adds the year.
 *
 * The filter card the feed shares (`EventFilterCard`) sits directly under the top bar, above the viewer, and narrows the
 * events drawn: Region (All, National, Central Asia, International; one choice, as on the feed) and Kind as pills, Stage,
 * City and Source behind "Filters", and a search of the titles, cities and organizers, each choice with how many events
 * it would match and the match pill reading `n of m` for the loaded window. The counts describe the window the calendar
 * has read, not the feed's upcoming range: they are counted here, never taken from the server. None chosen shows every
 * event, those with no country included. The choices stay in the address under the feed's keys
 * (`?regions=national&kinds=runway&q=silk`), so the same address filters both pages; the feed's `when` and `sort` mean
 * nothing here. The viewer keeps its own toolbar (today, previous, next, the title and the view switcher). The card
 * counts only once a read has answered and not failed; before then a count of 0 would mislead.
 */
defineOptions({ name: 'CalendarPage' });
const props = defineProps<{ organizationId: string }>();
const { t, uiLocale } = useUiLocale();
const router = useRouter();
const { view, date, events, filters, setFilters, facets, failure, loading, loaded, reload } = useEventsCalendar(
  createEventsPageClient(props.organizationId),
);

/**
 * Each facet's name, whether it takes one choice or several and where it sits, as the feed's card has them: the region
 * (one choice) and the kind in the bar, the rest behind "Filters".
 */
const facetLayout = computed<
  Readonly<Record<CalendarFacetKey, { label: string; kind: 'single' | 'multi'; placement: FacetPlacement }>>
>(() => ({
  scope: { label: t('Region'), kind: FacetKind.Single, placement: FacetPlacement.Inline },
  kind: { label: t('Kind'), kind: FacetKind.Multi, placement: FacetPlacement.Inline },
  stage: { label: t('Stage'), kind: FacetKind.Multi, placement: FacetPlacement.Popover },
  city: { label: t('City'), kind: FacetKind.Multi, placement: FacetPlacement.Popover },
  source: { label: t('Source'), kind: FacetKind.Multi, placement: FacetPlacement.Popover },
}));
const labels = computed(() => eventsFacetBarLabels(uiLocale.value));

const counted = computed(() => loaded.value && !failure.value);

/**
 * The facets for the card: those with a choice to make, each option with its count once the window is counted; the
 * region leads with an "All" pill that has no count, as the feed's has.
 */
const facetDefinitions = computed<readonly FacetDefinition[]>(() =>
  facets.value.facets
    .filter((facet) => facet.options.length > 0)
    .map((facet) => ({
      key: facet.key,
      ...facetLayout.value[facet.key],
      options: [
        ...(facet.key === 'scope' ? [{ value: allScopesValue, label: eventsText('all', uiLocale.value) }] : []),
        ...facet.options.map(({ value, label, count }) => ({ value, label, ...(counted.value ? { count } : {}) })),
      ],
    })),
);
const selection = computed(() => calendarSelection(filters.value));

function chooseFacets(next: FacetSelection): void {
  setFilters(withCalendarSelection(filters.value, next));
}
function searchFor(search: string): void {
  setFilters({ ...filters.value, search });
}

function openEvent(event: EventCalendarViewerEvent): void {
  void router.push(eventsPath(props.organizationId, { view: 'event', eventId: event.id }));
}
</script>

<template>
  <section class="flex flex-col gap-4">
    <h1 class="sr-only">{{ t('Calendar') }}</h1>
    <EventFilterCard
      :facet-definitions="facetDefinitions"
      :labels="labels"
      :model-value="selection"
      :search="filters.search"
      :match-count="counted ? facets.match : undefined"
      :total-count="counted ? facets.total : undefined"
      @update:model-value="chooseFacets"
      @update:search="searchFor"
    />
    <FailureNotice v-if="failure" :failure="failure" :locale="uiLocale" retry @retry="reload" />
    <div v-if="!loaded && !failure" aria-busy="true" data-test="calendar-loading">
      <span class="sr-only" role="status">{{ t('Loading…') }}</span>
      <SkeletonState class="h-[36rem] w-full" />
    </div>
    <EventCalendarViewer
      v-else
      v-model:view="view"
      v-model:date="date"
      :events="events"
      v-bind="{ 'aria-busy': loading || undefined }"
      data-test="calendar"
      @event-click="openEvent"
    />
  </section>
</template>
