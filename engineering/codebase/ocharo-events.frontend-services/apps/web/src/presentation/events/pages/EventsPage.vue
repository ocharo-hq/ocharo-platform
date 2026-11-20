<script setup lang="ts">
import { computed, shallowRef } from 'vue';
import { EventDetail, EventsFeed, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess, EventsWorkspace } from '@/application/events/UseEventsAccess';
import { createEventsPageClient, useEventToday, useEventsNavigation } from '@/application/events/UseEvents';
import { usePastToClose } from '@/application/events/UsePastToClose';
import {
  eventsPath,
  ocharoCalendarOptions,
  ocharoClosedStages,
  ocharoKinds,
  ocharoProposal,
  ocharoQuickStages,
  ocharoRegions,
  ocharoWorkflow,
  OcharoEventsCatalog,
  type EventsLocation,
} from '@/domain/events/Events';
import { storedImageSrc } from '@/domain/events/EventImages';

import PastEventsStrip from '../PastEventsStrip.vue';
import SourcesFreshness from '../SourcesFreshness.vue';

/**
 * Records events where Ocharo could show its products; it never applies, registers or pays. The page renders WoW2 Events'
 * own screens from `fe-events`' kit (OE07) over this app's mirror of Events' routes: the organization's tracking feed
 * (`EventsFeed` v2: a facet bar with server counts, a sort, a grid of the kit's `EventMediaCard`s that loads more on
 * scroll, quick Shortlist and Skip in each card's menu; skipped and rejected hidden until asked, scope chips, "Add event"
 * publishing at once) and one event's detail: its facts and sources, with no tracking card until that is redesigned (developer,
 * 2026-11-20; the kit's `EventDetail` shows the card only when `trackable`, so the page leaves it off). The feed's attention
 * pills sit between its bar and its grid: the kit's own application deadlines in the next 14 days, and Ocharo's "past to
 * close" (`attention` + the `#attention` slot), which unfolds `PastEventsStrip` to close each ended event Ocharo was
 * pursuing (OE12); each shows its count and is gone at zero. "Sources updated {time}" (E24) is the frame's, at the
 * sidebar's foot; below `md`, where there is no sidebar, its full text stands as its own muted line under the title,
 * from the same read — a phone has no room for a tooltip, so this copy carries no icon-only, tap-nothing mode.
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
/** Gives the kit the backend route of an image's stored copy; it falls back to the image's source link when that fails. */
const imageSrc = storedImageSrc(organizationId());
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
const proposal = ocharoProposal();
const eventHref = (id: string) => eventsPath(organizationId(), { view: 'event', eventId: id });
/** The key of Ocharo's one attention pill, which the feed hands back to the `#attention` slot. */
const pastKey = 'past';
const past = usePastToClose({
  client: () => client,
  catalog: () => OcharoEventsCatalog,
  today: () => today.value,
  closed: refreshFeed,
});
const { items: pastItems, failure: pastFailure, writing: pastWriting, changedNotice: pastChanged } = past;
/** The pill reads "{count} past to close" and the feed hides it at zero. */
const attention = computed(() => [
  { key: pastKey, label: t('{count} past to close', { count: past.count.value }), count: past.count.value },
]);

function open(location: EventsLocation): void {
  void navigation.open(location);
}
function openEvent(id: string): void {
  open({ view: 'event', eventId: id });
}
/** A write in the feed (a quick stage, a new event) can change the past events to close; the feed reads its own again. */
function refreshSide(): void {
  void past.reload();
}
/** The feed reads again with its filters kept: a stage written elsewhere on the page reaches it. */
function refreshFeed(): void {
  feedClient.value = { ...client };
}
</script>

<template>
  <section class="space-y-6">
    <header>
      <h1 class="page-title my-0">{{ t('Feed') }}</h1>
      <SourcesFreshness class="mt-1 md:hidden" data-test="sources-freshness-title" />
    </header>
    <!-- The feed stays mounted behind an open event, so its filters survive the round trip. -->
    <div v-show="showsFeed" data-test="events-feed">
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
        :can-propose="canTrack"
        :proposal="proposal"
        :attention="attention"
        :event-href="eventHref"
        :image-src="imageSrc"
        @open="openEvent"
        @proposed="refreshSide"
        @tracked="refreshSide"
      >
        <template #attention="{ key }">
          <PastEventsStrip
            v-if="key === pastKey"
            :items="pastItems"
            :failure="pastFailure"
            :writing="pastWriting"
            :changed-notice="pastChanged"
            :stages="workflow.stages"
            :can-track="canTrack"
            :event-href="eventHref"
            @open="openEvent"
            @close="past.close"
            @retry="past.reload"
          />
        </template>
      </EventsFeed>
    </div>
    <div v-if="!showsFeed && current.eventId">
      <button
        type="button"
        class="mb-4 inline-flex h-10 items-center text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50"
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
        :regions="regions"
        :calendar-options="ocharoCalendarOptions(current.eventId)"
        :image-src="imageSrc"
      />
    </div>
  </section>
</template>
