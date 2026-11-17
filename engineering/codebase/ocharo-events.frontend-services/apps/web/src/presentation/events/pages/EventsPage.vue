<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink } from 'vue-router';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess } from '@/application/events/UseEventsAccess';
import { useEvents, useEventsNavigation } from '@/application/events/UseEvents';
import type { EventsWorkspace } from '@/application/events/UseEventsAccess';
import { deliveryPath } from '@/domain/delivery/TelegramDelivery';
import { isEventIdentifier, type EventsLocation } from '@/domain/events/Events';

import EventDetail from '../EventDetail.vue';
import EventsFeed from '../EventsFeed.vue';
import '../Events.css';

/** Records events where Ocharo could show its products. It never applies, registers or pays. */
defineOptions({ name: 'EventsPage' });
const props = defineProps<{
  access: EventsAccess;
  workspace: EventsWorkspace;
  initialLocation?: EventsLocation | undefined;
}>();
const { t } = useUiLocale();
const events = useEvents(props.access, props.workspace);
const feed: EventsLocation = { view: 'feed', eventId: null };
const navigation = useEventsNavigation(props.access, events.organizationId, props.initialLocation ?? feed);
const current = navigation.current;
const showsFeed = computed(() => current.value.view !== 'event' || !current.value.eventId);
/** The organization's Telegram delivery settings, linked for its admins only. */
const deliveryHref = computed(() => {
  const organizationId = props.workspace.organization.value?.id;
  return props.workspace.canAdmin?.value && organizationId && isEventIdentifier(organizationId)
    ? deliveryPath(organizationId.toLowerCase())
    : null;
});
function open(location: EventsLocation): void {
  void navigation.open(location);
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
      <p class="max-w-2xl text-muted-foreground">
        {{
          t(
            "Every event where Ocharo could show its products — local, regional and international — with dates, deadlines, costs and where Ocharo stands. Events records decisions; it never applies, registers or pays on Ocharo's behalf.",
          )
        }}
      </p>
    </header>
    <!-- The feed stays mounted behind an open event, so its filters survive the round trip. -->
    <EventsFeed v-show="showsFeed" :access="access" :events="events" :active="showsFeed" @open="open" />
    <div v-if="!showsFeed && current.eventId">
      <button
        type="button"
        class="mb-4 text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50"
        :disabled="access.busy.value"
        @click="open(feed)"
      >
        ← {{ t('All events') }}
      </button>
      <EventDetail :key="current.eventId" :access="access" :events="events" :event-id="current.eventId" />
    </div>
  </section>
</template>
