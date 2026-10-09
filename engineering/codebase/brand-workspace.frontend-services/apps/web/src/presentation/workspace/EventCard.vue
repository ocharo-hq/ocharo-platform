<script setup lang="ts">
import { computed, ref } from 'vue';
import { Star } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { EventsControls } from '@/application/workspace/UseEvents';
import {
  daysLeftLabel, daysUntil, deadlineTone, eventPlace, eventsPath, formatDay, formatEventDates, formatLabels, scopeLabels, statusLabels, statusTones,
  type EventsLocation, type EventStatus, type ShowcaseEvent,
} from '@/domain/workspace/Events';

/** One event in the feed. Candidates carry quick review actions for editors. */
defineOptions({ name: 'EventCard' });
const props = defineProps<{ access: AccessControls; events: EventsControls; event: ShowcaseEvent; today: string }>();
const emit = defineEmits<{ open: [location: EventsLocation] }>();
const { t, uiLocale } = useUiLocale();
const saving = ref(false);
const href = computed(() => eventsPath(props.events.organizationId(), { view: 'event', eventId: props.event.id }));
const place = computed(() => eventPlace(props.event, uiLocale.value));
const deadlineDays = computed(() => props.event.applicationDeadline ? daysUntil(props.event.applicationDeadline, props.today) : null);

function follow(event: MouseEvent): void {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  if (!props.access.busy.value) emit('open', { view: 'event', eventId: props.event.id });
}
async function review(status: EventStatus): Promise<void> {
  if (saving.value) return;
  saving.value = true;
  try {
    const result = await props.events.patchEvent(props.event, { status });
    if (result.ok || result.failure.code === 'cancelled') return;
    if (result.failure.status === 409 || result.failure.status === 404) {
      notifyError('This event changed elsewhere. The feed has been reloaded; review it and try again.', `event-review-${props.event.id}`);
      await props.events.refreshAll();
    } else notifyError(failureMessage(result.failure));
  } finally { saving.value = false; }
}
</script>

<template>
  <article class="event-card" :data-status="event.status" :aria-labelledby="`event-card-${event.id}`">
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0 space-y-1">
        <p v-if="event.status === 'candidate'" class="event-new">{{ t("New — review") }}</p>
        <h4 :id="`event-card-${event.id}`" class="break-words text-base font-semibold leading-6">
          <a :href="href" class="block rounded-sm underline-offset-4 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary" @click="follow">{{ event.title }}</a>
        </h4>
        <p v-if="event.organizer" class="break-words text-sm text-muted-foreground">{{ event.organizer }}</p>
        <p v-if="place" class="text-sm text-muted-foreground">{{ place }}</p>
      </div>
      <span v-if="event.priority === 'high'" class="event-priority" :title="t('High priority')">
        <Star :size="14" aria-hidden="true" /><span>{{ t("High priority") }}</span>
      </span>
    </div>
    <p class="text-sm font-medium" data-test="event-dates">{{ formatEventDates(event, uiLocale) }}</p>
    <div class="flex flex-wrap gap-2">
      <Badge size="sm" variant="outline">{{ t(formatLabels[event.format]) }}</Badge>
      <Badge size="sm" variant="neutral">{{ t(scopeLabels[event.scope]) }}</Badge>
      <Badge size="sm" :variant="statusTones[event.status]">{{ t(statusLabels[event.status]) }}</Badge>
    </div>
    <dl class="grid gap-1 text-sm">
      <div v-if="event.applicationDeadline && deadlineDays !== null" class="flex flex-wrap gap-x-2">
        <dt class="text-muted-foreground">{{ t("Application deadline") }}</dt>
        <dd>
          {{ formatDay(event.applicationDeadline, uiLocale) }} ·
          <span class="events-days" :data-tone="deadlineTone(deadlineDays)">{{ daysLeftLabel(deadlineDays, uiLocale) }}</span>
        </dd>
      </div>
      <div class="flex min-w-0 flex-wrap gap-x-2">
        <dt class="text-muted-foreground">{{ t("Cost") }}</dt>
        <dd class="min-w-0 break-words" :class="{ 'text-muted-foreground': !event.cost }">{{ event.cost ?? t("Cost not stated") }}</dd>
      </div>
    </dl>
    <div v-if="events.canEdit.value && event.status === 'candidate'" class="flex flex-wrap gap-2 border-t border-border pt-3">
      <Button size="sm" variant="soft" :is-loading="saving" :is-disabled="access.busy.value || saving" @click="review('shortlisted')">{{ t("Shortlist") }}</Button>
      <Button size="sm" variant="ghost" :is-disabled="access.busy.value || saving" @click="review('skipped')">{{ t("Skip") }}</Button>
    </div>
  </article>
</template>
