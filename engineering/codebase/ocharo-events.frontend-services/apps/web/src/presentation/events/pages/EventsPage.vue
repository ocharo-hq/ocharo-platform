<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue';
import { EventDetail, EventsFeed, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess, EventsWorkspace } from '@/application/events/UseEventsAccess';
import { createEventsPageClient, useEventToday, useEventsNavigation } from '@/application/events/UseEvents';
import {
  eventsPath,
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

import AttentionPills from '../AttentionPills.vue';
import SourcesFreshness from '../SourcesFreshness.vue';

/**
 * Records events where Ocharo could show its products; it never applies, registers or pays. The page renders WoW2
 * Events' own screens from `fe-events`' kit (OE07) over this app's mirror of Events' routes: the organization's tracking
 * feed as image-first cards (`variant="cards"`: the kit's `EventMediaCard`, quick Shortlist and Skip in each card's
 * menu; skipped and rejected hidden until asked, scope chips above, "Add event" publishing at once; the cost stays in
 * the detail) and one event's detail with its tracking form. Under the title, two folded pills
 * (`AttentionPills`) open the nearest application deadlines and "Past, not closed", which asks to close each ended event
 * Ocharo was pursuing (OE12); each shows its count and is gone at zero. "Sources updated {time}" (E24) is the frame's,
 * at the sidebar's foot; below `md`, where there is no sidebar, its icon stands beside the title, from the same read.
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
const attention = ref<{ reload: () => unknown } | null>(null);
const eventHref = (id: string) => eventsPath(organizationId(), { view: 'event', eventId: id });

function open(location: EventsLocation): void {
  void navigation.open(location);
}
function openEvent(id: string): void {
  open({ view: 'event', eventId: id });
}
/** A write in the feed (a quick stage, a new event) can change the nearest deadlines and the past events to close. */
function refreshSide(): void {
  attention.value?.reload();
}
/** The feed reads again with its filters kept: a stage written elsewhere on the page reaches it. */
function refreshFeed(): void {
  feedClient.value = { ...client };
}
/** A tracking save in the detail moves the event's stage in the feed behind it, and can change the pills' lists. */
function refreshAll(): void {
  refreshFeed();
  refreshSide();
}
function setDirty(dirty: boolean): void {
  props.access.setDirty(dirty);
}
</script>

<template>
  <section class="space-y-6">
    <header class="flex items-center gap-3">
      <h1 class="page-title my-0">{{ t('Feed') }}</h1>
      <SourcesFreshness compact class="md:hidden" data-test="sources-freshness-title" />
    </header>
    <!-- The feed stays mounted behind an open event, so its filters survive the round trip. -->
    <div v-show="showsFeed" class="space-y-6" data-test="events-feed">
      <AttentionPills
        ref="attention"
        :client="client"
        :catalog="OcharoEventsCatalog"
        :today="today"
        :stages="workflow.stages"
        :can-track="canTrack"
        :event-href="eventHref"
        @open="openEvent"
        @changed="refreshFeed"
      />
      <EventsFeed
        :client="feedClient"
        :catalog="OcharoEventsCatalog"
        :locale="uiLocale"
        :today="today"
        :kinds="kinds"
        tracking
        variant="cards"
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
