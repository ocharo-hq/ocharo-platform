<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, ref, shallowRef } from 'vue';
import { CalendarDays, RefreshCw, Star } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';

import { notifyError, requestConfirmation, useErrorToast } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { createEventCalendar, downloadEventCalendar } from '@/application/workspace/EventCalendar';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { eventSaveOutcome, useShowcaseEvent, useEventToday, type EventSaveOutcome, type EventsControls } from '@/application/workspace/UseEvents';
import { eventCalendarUiMessages } from '@/domain/shared/EventCalendarUiMessages';
import {
  countryName, daysLeftLabel, daysUntil, deadlineTone, eventPriorities, eventStatuses, formatDay, formatEventDates, formatLabels,
  isEventPriority, isEventStatus, isIsoDate, precisionLabels, priorityLabels, safeEventUrl, scopeLabels, statusLabels, statusTones,
  type EventInput, type EventPriority, type EventStatus, type ShowcaseEvent,
} from '@/domain/workspace/Events';

import EventForm from './EventForm.vue';

/** One event: every recorded fact, where Ocharo stands, and the editor's controls. */
defineOptions({ name: 'EventDetail' });
const props = defineProps<{ access: AccessControls; events: EventsControls; eventId: string }>();
const emit = defineEmits<{ deleted: [id: string] }>();
const { t, uiLocale } = useUiLocale();
const { event, loading, error } = useShowcaseEvent(props.events, () => props.eventId);
const today = useEventToday();
// The editor owns the revision it opened, even when background reads return a newer record.
const editing = shallowRef<ShowcaseEvent | null>(null);
const reloading = ref(false);
useErrorToast(computed(() => error.value ? failureMessage(error.value) : null));
const formVersion = ref(0);
const fieldConflict = ref(false);
const fieldSaving = ref(false);
const calendarMessages = computed(() => eventCalendarUiMessages[uiLocale.value]);
const calendar = computed(() => event.value ? createEventCalendar(event.value, uiLocale.value) : undefined);
const website = computed(() => safeEventUrl(event.value?.website));
const source = computed(() => safeEventUrl(event.value?.sourceUrl));
const where = computed(() => event.value
  ? [event.value.venue, event.value.city, countryName(event.value.country, uiLocale.value)].filter(Boolean).join(' · ')
  : '');
const deadlineDays = computed(() => event.value?.applicationDeadline ? daysUntil(event.value.applicationDeadline, today.value) : null);
const updatedOn = computed(() => {
  const day = event.value?.updatedAt.slice(0, 10) ?? '';
  return isIsoDate(day) ? formatDay(day, uiLocale.value) : event.value?.updatedAt ?? '';
});

async function saveDetails(input: EventInput): Promise<EventSaveOutcome> {
  const current = editing.value;
  if (!current || !props.events.canEdit.value || props.access.busy.value) return 'failed';
  const outcome = eventSaveOutcome(await props.events.updateEvent(current, input));
  if (outcome === 'saved') editing.value = null;
  return outcome;
}
/** Downloads the currently recorded exact dates for any authorized reader. */
function exportCalendar(): void {
  if (calendar.value && !downloadEventCalendar(calendar.value)) notifyError(calendarMessages.value.downloadFailed);
}
/** Committed status and priority remain visible until the server confirms the change. */
async function changeField(field: 'status' | 'priority', value: string): Promise<void> {
  const current = event.value;
  if (!current || fieldSaving.value || props.access.busy.value || value === current[field]) return;
  const changes: { status?: EventStatus; priority?: EventPriority } = {};
  if (field === 'status' && isEventStatus(value)) changes.status = value;
  else if (field === 'priority' && isEventPriority(value)) changes.priority = value;
  else return;
  fieldSaving.value = true;
  try {
    const outcome = eventSaveOutcome(await props.events.patchEvent(current, changes));
    fieldConflict.value = outcome === 'conflict';
  } finally { fieldSaving.value = false; }
}
function edit(): void {
  if (!event.value || props.access.busy.value || reloading.value || !props.events.canEdit.value) return;
  editing.value = { ...event.value };
}
async function reload(discardEdits = false): Promise<void> {
  if (reloading.value || props.access.busy.value) return;
  reloading.value = true;
  try {
    await props.events.refreshEvent(props.eventId);
    if (error.value || !event.value) return;
    fieldConflict.value = false;
    if (discardEdits && editing.value) {
      editing.value = { ...event.value };
      formVersion.value += 1;
    }
  } catch { notifyError(t('Content unavailable. Try refreshing.')); }
  finally { reloading.value = false; }
}
async function remove(): Promise<void> {
  const current = event.value;
  if (!current) return;
  if (!await requestConfirmation('Delete this event? Its status and notes are removed too. This cannot be undone.', { confirmLabel: 'Delete' })) return;
  const result = await props.events.deleteEvent(current);
  if (result.ok) { emit('deleted', current.id); return; }
  if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  if (result.failure.status === 409 || result.failure.status === 404) await props.events.refreshEvent(current.id);
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <Button size="sm" variant="ghost" :is-loading="reloading" :is-disabled="access.busy.value || loading" @click="reload()">
        <RefreshCw :size="16" aria-hidden="true" />{{ t("Refresh") }}
      </Button>
    </div>
    <div v-if="loading && !event" class="workspace-card space-y-5 p-5 sm:p-6" aria-busy="true">
      <span role="status" class="sr-only">{{ t("Loading event…") }}</span>
      <div class="space-y-5" aria-hidden="true">
        <SkeletonState class="h-8 w-3/5 motion-reduce:animate-none" />
        <SkeletonState class="h-4 w-2/5 motion-reduce:animate-none" />
        <div class="grid gap-6 sm:grid-cols-2">
          <div v-for="item in 6" :key="item" class="space-y-3">
            <SkeletonState class="h-3 w-1/3 motion-reduce:animate-none" />
            <SkeletonState class="h-5 w-4/5 motion-reduce:animate-none" />
          </div>
        </div>
      </div>
    </div>
    <section v-else-if="!event" role="status" class="query-label">
      <h2 class="text-xl font-semibold">{{ t("Event unavailable") }}</h2>
    </section>
    <article v-else class="workspace-card space-y-5 p-5 sm:p-6" aria-labelledby="event-detail-title">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="min-w-0 space-y-1">
          <p v-if="event.status === 'candidate'" class="event-new">{{ t("New — review") }}</p>
          <h2 id="event-detail-title" class="break-words text-2xl font-semibold">{{ event.title }}</h2>
          <p v-if="event.organizer" class="break-words text-sm text-muted-foreground">{{ event.organizer }}</p>
          <p class="text-sm font-medium">{{ formatEventDates(event, uiLocale) }}</p>
        </div>
        <div v-if="events.canEdit.value && !editing" class="flex flex-wrap items-center gap-3">
          <label class="flex items-center gap-2 text-sm text-muted-foreground">{{ t("Status") }}
            <DeskSelect class="w-auto" :model-value="event.status" :disabled="access.busy.value || fieldSaving" @update:model-value="changeField('status', $event)" :label="t('Status')" :options="[...(eventStatuses).map(item => ({ value: item, label: (t(statusLabels[item])) }))]" />
          </label>
          <label class="flex items-center gap-2 text-sm text-muted-foreground">{{ t("Priority") }}
            <DeskSelect class="w-auto" :model-value="event.priority" :disabled="access.busy.value || fieldSaving" @update:model-value="changeField('priority', $event)" :label="t('Priority')" :options="[...(eventPriorities).map(item => ({ value: item, label: (t(priorityLabels[item])) }))]" />
          </label>
        </div>
        <div v-else-if="!events.canEdit.value" class="flex flex-wrap items-center gap-2">
          <Badge :variant="statusTones[event.status]">{{ t(statusLabels[event.status]) }}</Badge>
          <span v-if="event.priority === 'high'" class="event-priority"><Star :size="14" aria-hidden="true" /><span>{{ t("High priority") }}</span></span>
          <Badge v-else variant="outline">{{ t("Priority: {priority}", { priority: t(priorityLabels[event.priority]) }) }}</Badge>
        </div>
      </div>
      <div class="flex flex-wrap gap-2">
        <Badge size="sm" variant="outline">{{ t(formatLabels[event.format]) }}</Badge>
        <Badge size="sm" variant="neutral">{{ t(scopeLabels[event.scope]) }}</Badge>
      </div>
      <div v-if="fieldConflict" role="alert" class="events-alert space-y-2">
        <p>{{ t("This record changed. Reload the latest version before trying again.") }}</p>
        <Button size="sm" variant="outline" @click="reload()">{{ t("Reload latest") }}</Button>
      </div>
      <EventForm
        v-if="editing" :key="formVersion" :events="events" :dirty-key="`event-${event.id}`" :initial="editing"
        :disabled="access.busy.value || reloading" :submit-label="t('Save event')" :save="saveDetails" @cancel="editing = null" @reload="reload(true)"
      />
      <template v-else>
        <dl class="grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt class="text-xs text-muted-foreground">{{ t("Dates") }}</dt>
            <dd class="mt-1">{{ formatEventDates(event, uiLocale) }} <span class="text-muted-foreground">· {{ t(precisionLabels[event.datePrecision]) }}</span></dd>
          </div>
          <div class="min-w-0">
            <dt class="text-xs text-muted-foreground">{{ t("Where") }}</dt>
            <dd class="mt-1 break-words">{{ where || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs text-muted-foreground">{{ t("Application deadline") }}</dt>
            <dd class="mt-1">
              <template v-if="event.applicationDeadline && deadlineDays !== null">
                {{ formatDay(event.applicationDeadline, uiLocale) }} ·
                <span class="events-days" :data-tone="deadlineTone(deadlineDays)">{{ daysLeftLabel(deadlineDays, uiLocale) }}</span>
              </template>
              <span v-else class="text-muted-foreground">{{ t("No deadline recorded") }}</span>
            </dd>
          </div>
          <div class="min-w-0">
            <dt class="text-xs text-muted-foreground">{{ t("Cost") }}</dt>
            <dd class="mt-1 whitespace-pre-wrap break-words" :class="{ 'text-muted-foreground': !event.cost }">{{ event.cost ?? t("Cost not stated") }}</dd>
          </div>
          <div class="min-w-0">
            <dt class="text-xs text-muted-foreground">{{ t("Website") }}</dt>
            <dd class="mt-1 break-all">
              <a v-if="website" :href="website" target="_blank" rel="noopener noreferrer" class="text-primary underline-offset-4 hover:underline">{{ event.website }}</a>
              <span v-else>{{ event.website || '—' }}</span>
            </dd>
          </div>
          <div class="min-w-0">
            <dt class="text-xs text-muted-foreground">{{ t("Where it was found") }}</dt>
            <dd class="mt-1 break-all">
              <a v-if="source" :href="source" target="_blank" rel="noopener noreferrer" class="text-primary underline-offset-4 hover:underline">{{ event.sourceUrl }}</a>
              <span v-else>{{ event.sourceUrl || '—' }}</span>
            </dd>
          </div>
          <div class="min-w-0 sm:col-span-2">
            <dt class="text-xs text-muted-foreground">{{ t("How to take part") }}</dt>
            <dd class="mt-1 whitespace-pre-wrap break-words leading-6">{{ event.participation || '—' }}</dd>
          </div>
          <div class="min-w-0 sm:col-span-2">
            <dt class="text-xs text-muted-foreground">{{ t("Notes") }}</dt>
            <dd class="mt-1 whitespace-pre-wrap break-words leading-6">{{ event.notes || '—' }}</dd>
          </div>
          <div v-if="event.key" class="min-w-0">
            <dt class="text-xs text-muted-foreground">{{ t("Import key") }}</dt>
            <dd class="mt-1 break-all font-mono text-xs">{{ event.key }}</dd>
          </div>
          <div>
            <dt class="text-xs text-muted-foreground">{{ t("Last updated") }}</dt>
            <dd class="mt-1">{{ t("Revision {revision} · {date}", { revision: event.revision, date: updatedOn }) }}</dd>
          </div>
        </dl>
        <div class="flex flex-wrap items-center gap-3 border-t border-border pt-4">
          <Button size="sm" variant="outline" :is-disabled="!calendar" @click="exportCalendar">
            <CalendarDays :size="16" aria-hidden="true" />
            {{ calendar && !calendar.hasOccurrence ? calendarMessages.downloadDeadline : calendarMessages.download }}
          </Button>
          <p v-if="!calendar" class="text-sm text-muted-foreground">{{ calendarMessages.exactDateNeeded }}</p>
          <template v-if="events.canEdit.value">
            <Button size="sm" variant="outline" :is-disabled="access.busy.value" @click="edit">{{ t("Edit details") }}</Button>
            <Button size="sm" variant="ghost" tone="danger" :is-disabled="access.busy.value" @click="remove">{{ t("Delete event") }}</Button>
          </template>
        </div>
      </template>
      <p class="text-xs leading-5 text-muted-foreground">{{ t("Desk only records where Ocharo stands. Applications, registrations and payments happen outside Desk.") }}</p>
    </article>
  </div>
</template>
