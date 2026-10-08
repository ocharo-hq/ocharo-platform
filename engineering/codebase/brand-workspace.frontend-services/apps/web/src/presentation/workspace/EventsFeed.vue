<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Plus } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { eventSaveOutcome, useEventList, useEventSummary, type EventSaveOutcome, type EventsControls } from '@/application/workspace/UseEvents';
import {
  eventFormats, eventScopes, eventStatuses, formatLabels, groupEventsByMonth, localIsoDate, monthHeading, scopeLabels, statusLabels, undatedGroupKey,
  type EventFilter, type EventFormat, type EventInput, type EventScope, type EventsLocation, type EventStatus,
} from '@/domain/workspace/Events';

import EventCard from './EventCard.vue';
import EventForm from './EventForm.vue';
import EventsDeadlines from './EventsDeadlines.vue';

/** The feed: next deadlines, filters, and every event grouped by the month it starts in. */
defineOptions({ name: 'EventsFeed' });
const props = defineProps<{ access: AccessControls; events: EventsControls; active: boolean }>();
const emit = defineEmits<{ open: [location: EventsLocation] }>();
const { t, uiLocale } = useUiLocale();
// The operator's own calendar day: past events and deadlines are judged against it.
const today = ref(localIsoDate());
const search = ref('');
const query = ref('');
const scope = ref<EventScope | ''>('');
const format = ref<EventFormat | ''>('');
const status = ref<EventStatus | ''>('');
const showPast = ref(false);
const adding = ref(false);
let timer: ReturnType<typeof setTimeout> | undefined;
watch(search, value => {
  clearTimeout(timer);
  timer = setTimeout(() => { query.value = value.trim(); }, 250);
});
onScopeDispose(() => clearTimeout(timer));
// Leaving the feed closes the add form, so its unsaved state cannot outlive the confirmation.
watch(() => props.active, active => { if (!active) adding.value = false; });

const filter = computed<EventFilter>(() => ({
  query: query.value, scope: scope.value, format: format.value, status: status.value, from: showPast.value ? null : today.value,
}));
const list = useEventList(props.events, () => filter.value);
const summary = useEventSummary(props.events, () => today.value);
const groups = computed(() => groupEventsByMonth(list.events.value ?? []));
const filtered = computed(() => Boolean(query.value || scope.value || format.value || status.value));
const statusCount = (value: EventStatus) => summary.summary.value?.byStatus[value] ?? 0;
const totalCount = computed(() => eventStatuses.reduce((sum, value) => sum + statusCount(value), 0));
const scopeChips = computed(() => [{ value: '' as const, label: t('All') }, ...eventScopes.map(value => ({ value, label: t(scopeLabels[value]) }))]);

function heading(key: string): string {
  return key === undatedGroupKey ? t('Date not announced') : monthHeading(key, uiLocale.value);
}
async function create(input: EventInput): Promise<EventSaveOutcome> {
  const result = await props.events.createEvent(input);
  const outcome = eventSaveOutcome(result);
  if (result.ok) {
    props.events.setFormDirty('event-create', false);
    adding.value = false;
    emit('open', { view: 'event', eventId: result.value.id });
  }
  return outcome;
}
</script>

<template>
  <div class="space-y-6">
    <EventsDeadlines :access="access" :events="events" :summary="summary.summary.value" :loading="summary.loading.value" :failed="Boolean(summary.error.value)" :today="today" @open="location => emit('open', location)" />

    <section class="space-y-4" aria-labelledby="events-feed-title">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="events-feed-title" class="text-lg font-semibold">{{ t("Event feed") }}</h2>
        <Button v-if="events.canEdit.value && !adding" size="sm" variant="outline" :is-disabled="access.busy.value" @click="adding = true">
          <Plus :size="16" aria-hidden="true" />{{ t("Add event") }}
        </Button>
      </div>
      <section v-if="adding" class="workspace-card p-4 sm:p-6" :aria-label="t('New event')">
        <h3 class="mb-3 font-semibold">{{ t("New event") }}</h3>
        <EventForm :events="events" dirty-key="event-create" :disabled="access.busy.value" :submit-label="t('Create event')" :save="create" @cancel="adding = false" />
      </section>

      <div class="events-filters" role="search" :aria-label="t('Filter events')">
        <label class="events-field events-filter-search">{{ t("Search events") }}
          <input v-model="search" type="search" class="events-input" :placeholder="t('Title, organizer, city or country')" autocomplete="off" />
        </label>
        <div class="events-field events-filter-scope">
          <span id="events-scope-label">{{ t("Scope") }}</span>
          <div class="events-chips" role="group" aria-labelledby="events-scope-label">
            <button
              v-for="chip in scopeChips" :key="chip.value" type="button" class="events-chip" :aria-pressed="scope === chip.value ? 'true' : 'false'"
              @click="scope = chip.value"
            >{{ chip.label }}</button>
          </div>
        </div>
        <label class="events-field">{{ t("Format") }}
          <select v-model="format" class="events-input">
            <option value="">{{ t("All formats") }}</option>
            <option v-for="item in eventFormats" :key="item" :value="item">{{ t(formatLabels[item]) }}</option>
          </select>
        </label>
        <label class="events-field">{{ t("Status") }}
          <select v-model="status" class="events-input">
            <option value="">{{ summary.summary.value ? `${t("All statuses")} (${totalCount})` : t("All statuses") }}</option>
            <option v-for="item in eventStatuses" :key="item" :value="item">{{ summary.summary.value ? `${t(statusLabels[item])} (${statusCount(item)})` : t(statusLabels[item]) }}</option>
          </select>
        </label>
        <label class="events-toggle">
          <input v-model="showPast" type="checkbox" />
          <span>{{ t("Show past events") }}</span>
        </label>
      </div>

      <p v-if="list.loading.value && !list.events.value" role="status" class="py-6 text-sm text-muted-foreground">{{ t("Loading events…") }}</p>
      <p v-else-if="list.error.value && !list.events.value" role="status" class="py-6 text-sm text-muted-foreground">{{ t("Events unavailable. Try refreshing.") }}</p>
      <template v-else>
        <section v-for="group in groups" :key="group.key" class="space-y-3" :aria-labelledby="`events-group-${group.key}`" data-test="event-group">
          <h3 :id="`events-group-${group.key}`" class="events-group-title">{{ heading(group.key) }}</h3>
          <ul class="events-list">
            <li v-for="item in group.events" :key="item.id" class="min-w-0">
              <EventCard :access="access" :events="events" :event="item" :today="today" @open="location => emit('open', location)" />
            </li>
          </ul>
        </section>
        <p v-if="!groups.length" class="editor-prompt">
          {{ filtered ? t("No events match these filters.") : showPast ? t("No events recorded yet.") : t("No upcoming events. Turn on past events to see earlier ones.") }}
        </p>
        <p v-if="list.truncated.value" class="text-xs text-muted-foreground">{{ t("Showing the first 200 events. Search or filter to narrow the list.") }}</p>
      </template>
      <p class="text-xs leading-5 text-muted-foreground">{{ t("Desk only records where Ocharo stands. Applications, registrations and payments happen outside Desk.") }}</p>
    </section>
  </div>
</template>
