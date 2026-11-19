<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue';
import { RouterLink } from 'vue-router';
import { EventDetail, EventsDeadlines, EventsFeed, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess, EventsWorkspace } from '@/application/events/UseEventsAccess';
import { createEventsPageClient, useEventToday, useEventsNavigation } from '@/application/events/UseEvents';
import { deliveryPath } from '@/domain/delivery/TelegramDelivery';
import {
  eventsPath,
  isEventIdentifier,
  ocharoCalendarOptions,
  ocharoClosedStages,
  ocharoCostField,
  ocharoKinds,
  ocharoParticipationField,
  ocharoProposal,
  ocharoQuickStages,
  ocharoRegions,
  ocharoWorkflow,
  OcharoEventsCatalog,
  type EventsLocation,
} from '@/domain/events/Events';

import PastEventsStrip from '../PastEventsStrip.vue';
import SourcesFreshness from '../SourcesFreshness.vue';

/**
 * Records events where Ocharo could show its products; it never applies, registers or pays. The page renders WoW2
 * Events' own screens from `fe-events`' kit (OE07) over this app's mirror of Events' routes: the nearest application
 * deadlines, the organization's tracking feed (skipped and rejected hidden until asked, scope chips, quick Shortlist and
 * Skip, Ocharo's cost on each card, "Add event" publishing at once) and one event's detail with its tracking form.
 * Between the deadlines and the feed, "Past, not closed" asks to close each ended event Ocharo was pursuing (OE12).
 * Under the title, "Sources updated {time}" says when the intake last delivered, as a warning when it is over a day old or
 * a source fails (E24).
 */
defineOptions({ name: 'EventsPage' });
const props = defineProps<{
  access: EventsAccess;
  workspace: EventsWorkspace;
  initialLocation?: EventsLocation | undefined;
}>();
const { t, uiLocale } = useUiLocale();
const organizationId = () => props.workspace.organization.value?.id.toLowerCase() ?? '';
const client = createEventsPageClient(organizationId());
/** The client the feed reads through; a fresh copy makes the feed read again with its filters kept. */
const feedClient = shallowRef<EventsClient>(client);
const feed: EventsLocation = { view: 'feed', eventId: null };
const navigation = useEventsNavigation(props.access, organizationId, props.initialLocation ?? feed);
const current = navigation.current;
const showsFeed = computed(() => current.value.view !== 'event' || !current.value.eventId);
const today = useEventToday();
const canTrack = computed(() => props.workspace.canEdit.value);
const workflow = ocharoWorkflow();
const kinds = ocharoKinds();
const regions = ocharoRegions();
const costField = ocharoCostField();
const participationField = ocharoParticipationField();
const proposal = ocharoProposal();
const deadlines = ref<{ reload: () => unknown } | null>(null);
const past = ref<{ reload: () => unknown } | null>(null);

/** The organization's Telegram delivery settings, linked for its admins only. */
const deliveryHref = computed(() => {
  const id = organizationId();
  return props.workspace.canAdmin?.value && id && isEventIdentifier(id) ? deliveryPath(id) : null;
});
const eventHref = (id: string) => eventsPath(organizationId(), { view: 'event', eventId: id });

function open(location: EventsLocation): void {
  void navigation.open(location);
}
function openEvent(id: string): void {
  open({ view: 'event', eventId: id });
}
/** A write in the feed (a quick stage, a new event) can change the nearest deadlines and the past events to close. */
function refreshSide(): void {
  deadlines.value?.reload();
  past.value?.reload();
}
/** A tracking save in the detail moves the event's stage in the feed behind it too. */
function refreshAll(): void {
  feedClient.value = { ...client };
  refreshSide();
}
/** A past event closed in the strip leaves the strip by itself; its new stage reaches the feed and the deadlines. */
function afterClose(): void {
  feedClient.value = { ...client };
  deadlines.value?.reload();
}
function setDirty(dirty: boolean): void {
  props.access.setDirty(dirty);
}
</script>

<template>
  <section class="space-y-6">
    <header>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <p class="eyebrow">{{ t('Showcase events') }}</p>
        <RouterLink
          v-if="deliveryHref"
          :to="deliveryHref"
          class="text-sm font-medium text-primary underline-offset-4 hover:underline"
          data-test="telegram-delivery-link"
          >{{ t('Telegram delivery') }}</RouterLink
        >
      </div>
      <h1 class="page-title">{{ t('Events') }}</h1>
      <SourcesFreshness :client="client" :catalog="OcharoEventsCatalog" />
      <p class="max-w-2xl text-muted-foreground">
        {{
          t(
            "Every event where Ocharo could show its products — local, regional and international — with dates, deadlines, costs and where Ocharo stands. Events records decisions; it never applies, registers or pays on Ocharo's behalf.",
          )
        }}
      </p>
    </header>
    <!-- The feed stays mounted behind an open event, so its filters survive the round trip. -->
    <div v-show="showsFeed" class="space-y-6" data-test="events-feed">
      <EventsDeadlines
        ref="deadlines"
        :client="client"
        :catalog="OcharoEventsCatalog"
        :locale="uiLocale"
        :today="today"
        :stages="workflow.stages"
        :deadline-kinds="['application']"
        :event-href="eventHref"
        @open="openEvent"
      />
      <PastEventsStrip
        ref="past"
        :client="client"
        :catalog="OcharoEventsCatalog"
        :today="today"
        :stages="workflow.stages"
        :can-track="canTrack"
        :event-href="eventHref"
        @open="openEvent"
        @changed="afterClose"
      />
      <EventsFeed
        :client="feedClient"
        :catalog="OcharoEventsCatalog"
        :locale="uiLocale"
        :today="today"
        :kinds="kinds"
        tracking
        :workflow="workflow"
        :hidden-stages="ocharoClosedStages"
        :regions="regions"
        :quick-stages="ocharoQuickStages"
        :can-track="canTrack"
        :shown-fields="[costField]"
        :can-propose="canTrack"
        :proposal="proposal"
        :event-href="eventHref"
        @open="openEvent"
        @proposed="refreshSide"
        @tracked="refreshSide"
      />
    </div>
    <div v-if="!showsFeed && current.eventId">
      <button
        type="button"
        class="mb-4 text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50"
        :disabled="access.busy.value"
        data-test="back-to-feed"
        @click="open(feed)"
      >
        ← {{ t('All events') }}
      </button>
      <EventDetail
        :key="current.eventId"
        :client="client"
        :catalog="OcharoEventsCatalog"
        :event-id="current.eventId"
        :locale="uiLocale"
        :today="today"
        :kinds="kinds"
        trackable
        :can-track="canTrack"
        :regions="regions"
        :shown-fields="[participationField, costField]"
        :calendar-options="ocharoCalendarOptions(current.eventId)"
        @dirty="setDirty"
        @tracking-saved="refreshAll"
      />
    </div>
  </section>
</template>
