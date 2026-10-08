<script setup lang="ts">
import { computed } from 'vue';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { useEvents, useEventsNavigation } from '@/application/workspace/UseEvents';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { EventsLocation } from '@/domain/workspace/Events';

import EventDetail from './EventDetail.vue';
import EventsFeed from './EventsFeed.vue';
import './Events.css';

/** Records events where Ocharo could show its products. It never applies, registers or pays. */
defineOptions({ name: 'EventsPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; initialLocation?: EventsLocation | undefined }>();
const { t } = useUiLocale();
const events = useEvents(props.access, props.workspace);
const feed: EventsLocation = { view: 'feed', eventId: null };
const navigation = useEventsNavigation(props.access, events.organizationId, props.initialLocation ?? feed);
const current = navigation.current;
const showsFeed = computed(() => current.value.view !== 'event' || !current.value.eventId);
function open(location: EventsLocation): void { void navigation.open(location); }
async function deleted(id: string): Promise<void> {
  await navigation.open(feed);
  events.forgetEvent(id);
}
</script>

<template>
  <section class="space-y-6">
    <header>
      <p class="eyebrow">{{ t("Showcase events") }}</p>
      <h1 class="page-title">{{ t("Events") }}</h1>
      <p class="max-w-2xl text-muted-foreground">{{ t("Every event where Ocharo could show its products — local, regional and international — with dates, deadlines, costs and where Ocharo stands. Desk records decisions; it never applies, registers or pays on Ocharo's behalf.") }}</p>
    </header>
    <!-- The feed stays mounted behind an open event, so its filters survive the round trip. -->
    <EventsFeed v-show="showsFeed" :access="access" :events="events" :active="showsFeed" @open="open" />
    <div v-if="!showsFeed && current.eventId">
      <button type="button" class="mb-4 text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50" :disabled="access.busy.value" @click="open(feed)">← {{ t("All events") }}</button>
      <EventDetail :key="current.eventId" :access="access" :events="events" :event-id="current.eventId" @deleted="deleted" />
    </div>
  </section>
</template>
