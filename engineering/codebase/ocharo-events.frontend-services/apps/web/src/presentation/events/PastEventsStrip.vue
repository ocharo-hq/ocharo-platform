<script setup lang="ts">
import { onMounted, shallowRef, watch } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import type { TrackedEventModel, TrackingSpec, WorkflowStageModel } from '@wow-two-beta/fe-events/contracts';
import {
  EventsLabels,
  FailureNotice,
  eventTitle,
  formatEventDates,
  isEventPast,
  type EventsClient,
  type EventsFailure,
} from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoClosingStages, ocharoPursuingStages, previousIsoDate, type EventStatus } from '@/domain/events/Events';

/**
 * "Past, not closed" (OE12): the events that have ended while Ocharo was still pursuing them (shortlisted, applying,
 * applied or confirmed), each closed with one click as Attended or Skipped through one tracking write. Events lists them
 * server-side by stage and by a start no later than yesterday; the strip keeps those whose last day has passed. An ended
 * event Ocharo never tracked needs no action, and the strip hides itself when nothing is left to close. It tells the page
 * how many events wait (`count`), which the feed's "Past to close" pill shows while the strip is folded away.
 */
defineOptions({ name: 'PastEventsStrip' });
const props = defineProps<{
  client: EventsClient;
  catalog: string;
  /** The person's calendar day, `YYYY-MM-DD`. */
  today: string;
  /** The organization's stages, for their labels. */
  stages: readonly WorkflowStageModel[];
  /** Whether the person may write tracking (`events:track`); the list stays read-only otherwise. */
  canTrack: boolean;
  eventHref: (id: string) => string;
}>();
const emit = defineEmits<{
  open: [eventId: string];
  /** Fires after an event was closed, so the page reloads the views that show its stage. */
  changed: [];
  /** Fires with how many events wait to be closed, whenever that number changes (0 while the strip shows nothing). */
  count: [rows: number];
}>();
const { t, uiLocale } = useUiLocale();

/** The most events one read lists; a team closing more than this closes them a page at a time. */
const pageSize = 50;
const items = shallowRef<readonly TrackedEventModel[]>([]);
const failure = shallowRef<EventsFailure | null>(null);
const writing = shallowRef<string | null>(null);
const changedNotice = shallowRef(false);
let generation = 0;

async function load(): Promise<void> {
  const run = ++generation;
  const result = await props.client.listTracking(props.catalog, {
    stages: [...ocharoPursuingStages],
    to: previousIsoDate(props.today),
    includeUndated: false,
    limit: pageSize,
  });
  if (run !== generation) return;
  if (!result.ok) {
    failure.value = result.failure;
    return;
  }
  failure.value = null;
  items.value = result.value.items.filter((item) => item.tracking && isEventPast(item.event, props.today));
}

/** Moves one event to `stage`, sending the rest of its tracking back as it was read, at the revision read. */
async function close(item: TrackedEventModel, stage: EventStatus): Promise<void> {
  const tracking = item.tracking;
  if (!tracking || writing.value) return;
  writing.value = item.event.id;
  changedNotice.value = false;
  const spec: TrackingSpec = {
    stage,
    priority: tracking.priority,
    assignee: tracking.assignee ?? null,
    notes: tracking.notes ?? '',
    ...(tracking.fields !== undefined ? { fields: tracking.fields } : {}),
    references: tracking.references ?? [],
    expectedRevision: tracking.revision,
  };
  const result = await props.client.putTracking(props.catalog, item.event.id, spec);
  writing.value = null;
  if (result.ok) {
    items.value = items.value.filter((entry) => entry.event.id !== item.event.id);
    emit('changed');
    return;
  }
  if (result.failure.reason === 'conflict' || result.failure.reason === 'notFound') {
    changedNotice.value = true;
    await load();
    return;
  }
  failure.value = result.failure;
}

/** Opens the event inside the page on a plain click; any other click follows the link. */
function follow(event: MouseEvent, id: string): void {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  emit('open', id);
}

onMounted(load);
watch([() => props.today, () => props.client], () => void load());
watch(
  () => items.value.length,
  (rows) => emit('count', rows),
  { immediate: true },
);
defineExpose({ reload: load });
</script>

<template>
  <section
    v-if="items.length || failure"
    class="space-y-3 rounded-lg border border-border p-4"
    aria-labelledby="past-open-title"
    data-test="past-open"
  >
    <h2 id="past-open-title" class="text-base font-semibold">{{ t('Past, not closed') }}</h2>
    <p class="text-sm text-muted-foreground">
      {{
        t(
          'These events have ended while Ocharo was still pursuing them. Record whether Ocharo attended or skipped each.',
        )
      }}
    </p>
    <p v-if="changedNotice" class="text-sm text-warning" role="status" data-test="past-open-changed">
      {{ t('An event changed since the list was read, so the list was reloaded.') }}
    </p>
    <FailureNotice v-if="failure" :failure="failure" :locale="uiLocale" retry @retry="load" />
    <ul v-if="items.length" class="divide-y divide-border">
      <li
        v-for="item in items"
        :key="item.event.id"
        class="flex flex-wrap items-center justify-between gap-3 py-2"
        data-test="past-open-item"
      >
        <div class="min-w-0">
          <a
            :href="eventHref(item.event.id)"
            class="font-medium text-foreground underline-offset-4 hover:underline"
            @click="follow($event, item.event.id)"
            >{{ eventTitle(item.event.facts, uiLocale) }}</a
          >
          <p class="text-sm text-muted-foreground">
            {{ formatEventDates(item.event, uiLocale) }} ·
            {{ EventsLabels.stage(item.tracking?.stage ?? '', stages, uiLocale) }}
          </p>
        </div>
        <div v-if="canTrack" class="flex flex-wrap gap-2">
          <Button
            v-for="stage in ocharoClosingStages"
            :key="stage"
            size="sm"
            variant="outline"
            :is-loading="writing === item.event.id"
            :is-disabled="writing !== null"
            :data-test="`past-open-${stage}`"
            @click="close(item, stage)"
          >
            {{ EventsLabels.stage(stage, stages, uiLocale) }}
          </Button>
        </div>
      </li>
    </ul>
  </section>
</template>
