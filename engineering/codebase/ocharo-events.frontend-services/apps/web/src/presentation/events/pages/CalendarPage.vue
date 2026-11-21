<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { EventCalendarViewer, type EventCalendarViewerEvent } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { ToggleGroup } from '@wow-two-beta/fe/presentation/forms';
import { FailureNotice } from '@wow-two-beta/fe-events/kit';

import { createEventsPageClient } from '@/application/events/UseEvents';
import { useEventsCalendar } from '@/application/events/UseEventsCalendar';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { eventsPath } from '@/domain/events/Events';

/**
 * The organization's events on a calendar (the fe sdk's `EventCalendarViewer`): each event with a known day as a block
 * on it, coloured by where Ocharo stands with it, over the same tracking feed the feed page reads. An event known only
 * to its month or year has no day and is not drawn. Opening a block goes to the event's page in the feed. The viewer
 * offers every view it has: the fe sdk pinned now has month, week, day and agenda, and its re-pin adds the year.
 *
 * Three scope pills (National, Central Asia, International; the fe sdk's `ToggleGroup`, several at once) sit in the
 * viewer's own toolbar (its `toolbar-extra` slot), narrowing the events to those whose country stands in a chosen
 * scope, each with how many events the loaded window draws in it. None chosen shows every event, those with no
 * country included. The choice stays in the address (`?scope=national,central-asia`). The pills render only once the
 * viewer itself does — on load or failure, never during the bare loading skeleton.
 */
defineOptions({ name: 'CalendarPage' });
const props = defineProps<{ organizationId: string }>();
const { t, uiLocale } = useUiLocale();
const router = useRouter();
const { view, date, events, scopes, scopePills, setScopes, failure, loading, loaded, reload } = useEventsCalendar(
  createEventsPageClient(props.organizationId),
);

/** The pills show their counts once a read has answered and not failed; before then a count of 0 would mislead. */
const pills = computed(() =>
  loaded.value && !failure.value ? scopePills.value : scopePills.value.map(({ value, label }) => ({ value, label })),
);

/** The pills' selection as the keys chosen; the group reports one key or none in single mode, never used here. */
function chooseScopes(value: readonly string[] | string | null): void {
  setScopes(Array.isArray(value) ? value : typeof value === 'string' ? [value] : []);
}

function openEvent(event: EventCalendarViewerEvent): void {
  void router.push(eventsPath(props.organizationId, { view: 'event', eventId: event.id }));
}
</script>

<template>
  <section>
    <h1 class="sr-only">{{ t('Calendar') }}</h1>
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
    >
      <template #toolbar-extra>
        <ToggleGroup
          type="multi"
          variant="pill"
          item-variant="solid"
          item-tone="neutral"
          :model-value="scopes"
          :options="pills"
          :aria-label="t('Scope')"
          data-test="calendar-scopes"
          class="[&>button]:h-10 sm:[&>button]:h-7"
          @update:model-value="chooseScopes"
        />
      </template>
    </EventCalendarViewer>
  </section>
</template>
