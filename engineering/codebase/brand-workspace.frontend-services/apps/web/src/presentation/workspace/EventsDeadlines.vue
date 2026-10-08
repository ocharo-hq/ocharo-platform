<script setup lang="ts">
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { EventsControls } from '@/application/workspace/UseEvents';
import {
  daysLeftLabel, daysUntil, deadlineTone, eventsPath, formatDay, statusLabels, statusTones,
  type EventsLocation, type EventSummary, type ShowcaseEvent,
} from '@/domain/workspace/Events';

/** The next application deadlines, soonest first, as the summary reports them. */
defineOptions({ name: 'EventsDeadlines' });
const props = defineProps<{ access: AccessControls; events: EventsControls; summary: EventSummary | undefined; loading: boolean; failed: boolean; today: string }>();
const emit = defineEmits<{ open: [location: EventsLocation] }>();
const { t, uiLocale } = useUiLocale();
const days = (event: ShowcaseEvent) => daysUntil(event.applicationDeadline!, props.today);
const href = (event: ShowcaseEvent) => eventsPath(props.events.organizationId(), { view: 'event', eventId: event.id });
function follow(event: MouseEvent, item: ShowcaseEvent): void {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  if (!props.access.busy.value) emit('open', { view: 'event', eventId: item.id });
}
</script>

<template>
  <section class="workspace-card space-y-3 p-4 sm:p-5" aria-labelledby="events-deadlines-title">
    <h2 id="events-deadlines-title" class="text-base font-semibold">{{ t("Next deadlines") }}</h2>
    <p v-if="loading && !summary" role="status" class="text-sm text-muted-foreground">{{ t("Loading deadlines…") }}</p>
    <p v-else-if="!summary" role="status" class="text-sm text-muted-foreground">{{ failed ? t("Deadlines unavailable. Try refreshing.") : t("Loading deadlines…") }}</p>
    <p v-else-if="!summary.upcomingDeadlines.length" class="text-sm text-muted-foreground">{{ t("No upcoming application deadlines.") }}</p>
    <ol v-else class="events-deadlines">
      <li v-for="item in summary.upcomingDeadlines.filter(entry => entry.applicationDeadline)" :key="item.id" class="events-deadline" data-test="deadline">
        <a :href="href(item)" class="events-deadline-title" @click="follow($event, item)">{{ item.title }}</a>
        <span class="text-xs text-muted-foreground">{{ t("Apply by {date}", { date: formatDay(item.applicationDeadline!, uiLocale) }) }}</span>
        <span class="flex flex-wrap items-center gap-2">
          <span class="events-days text-xs" :data-tone="deadlineTone(days(item))">{{ daysLeftLabel(days(item), uiLocale) }}</span>
          <Badge size="sm" :variant="statusTones[item.status]">{{ t(statusLabels[item.status]) }}</Badge>
        </span>
      </li>
    </ol>
  </section>
</template>
