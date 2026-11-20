<script setup lang="ts">
import { ref, shallowRef, watch } from 'vue';
import { ChevronDown } from 'lucide-vue-next';
import type { WorkflowStageModel } from '@wow-two-beta/fe-events/contracts';
import { EventsDeadlines, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useDeadlineCount } from '@/application/events/UseDeadlineCount';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoDeadlineKinds } from '@/domain/events/Events';

import PastEventsStrip from './PastEventsStrip.vue';

/**
 * What waits on a decision, folded under the feed's title: two toggle pills, "Deadlines" (the nearest application
 * deadlines, `EventsDeadlines`) and "Past to close" (the ended events Ocharo was pursuing, `PastEventsStrip`), each
 * unfolding its list inline below and folded by default. A pill shows its count and is gone at zero, so a quiet team sees
 * nothing. The strip stays mounted while folded, since it is what tells the count; the deadlines list mounts when
 * unfolded, its count read apart through the same client (`useDeadlineCount`), as the kit's list exposes none.
 * Interim: fe-events' next feed will give these an attention slot of its own.
 */
defineOptions({ name: 'AttentionPills' });
const props = defineProps<{
  client: EventsClient;
  catalog: string;
  /** The person's calendar day, `YYYY-MM-DD`. */
  today: string;
  /** The organization's stages, for their labels. */
  stages: readonly WorkflowStageModel[];
  /** Whether the person may write tracking (`events:track`); the past events stay read-only otherwise. */
  canTrack: boolean;
  eventHref: (id: string) => string;
}>();
const emit = defineEmits<{
  open: [eventId: string];
  /** Fires after a past event was closed, so the page reloads the feed that shows its stage. */
  changed: [];
}>();
const { t, uiLocale } = useUiLocale();

const deadlinesCount = useDeadlineCount({
  client: () => props.client,
  catalog: () => props.catalog,
  today: () => props.today,
});
const pastCount = shallowRef(0);
const deadlinesOpen = ref(false);
const pastOpen = ref(false);
const deadlines = ref<{ reload: () => unknown } | null>(null);
const past = ref<{ reload: () => unknown } | null>(null);

// A pill that disappears at zero folds, so it comes back folded.
watch(deadlinesCount.count, (count) => {
  if (count === 0) deadlinesOpen.value = false;
});
watch(pastCount, (count) => {
  if (count === 0) pastOpen.value = false;
});

/** Reads both again: a write in the feed (a quick stage, a new event) can change either list. */
function reload(): void {
  void deadlinesCount.reload();
  deadlines.value?.reload();
  past.value?.reload();
}
/** A past event closed in the strip leaves it by itself; its new stage reaches the feed and can leave the deadlines. */
function afterClose(): void {
  void deadlinesCount.reload();
  deadlines.value?.reload();
  emit('changed');
}
defineExpose({ reload });

const pillClass =
  'group inline-flex min-h-9 items-center gap-2 rounded-full border border-border px-3 text-sm font-medium transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring aria-expanded:border-primary/30 aria-expanded:bg-primary-soft aria-expanded:text-primary-soft-foreground';
const countClass = 'rounded-full bg-muted px-2 text-xs tabular-nums group-aria-expanded:bg-background/60';
</script>

<template>
  <div class="space-y-3" data-test="attention">
    <div v-if="deadlinesCount.count.value > 0 || pastCount > 0" class="flex flex-wrap gap-2">
      <button
        v-if="deadlinesCount.count.value > 0"
        type="button"
        :class="pillClass"
        :aria-expanded="deadlinesOpen"
        aria-controls="attention-deadlines"
        data-test="attention-deadlines-pill"
        @click="deadlinesOpen = !deadlinesOpen"
      >
        {{ t('Deadlines') }}
        <span :class="countClass" data-test="attention-count">{{ deadlinesCount.count.value }}</span>
        <ChevronDown :size="14" aria-hidden="true" class="transition-transform group-aria-expanded:rotate-180" />
      </button>
      <button
        v-if="pastCount > 0"
        type="button"
        :class="pillClass"
        :aria-expanded="pastOpen"
        aria-controls="attention-past"
        data-test="attention-past-pill"
        @click="pastOpen = !pastOpen"
      >
        {{ t('Past to close') }}
        <span :class="countClass" data-test="attention-count">{{ pastCount }}</span>
        <ChevronDown :size="14" aria-hidden="true" class="transition-transform group-aria-expanded:rotate-180" />
      </button>
    </div>
    <div v-if="deadlinesOpen" id="attention-deadlines">
      <EventsDeadlines
        ref="deadlines"
        :client="client"
        :catalog="catalog"
        :locale="uiLocale"
        :today="today"
        :stages="stages"
        :deadline-kinds="ocharoDeadlineKinds"
        :event-href="eventHref"
        @open="emit('open', $event)"
      />
    </div>
    <div v-show="pastOpen" id="attention-past">
      <PastEventsStrip
        ref="past"
        :client="client"
        :catalog="catalog"
        :today="today"
        :stages="stages"
        :can-track="canTrack"
        :event-href="eventHref"
        @open="emit('open', $event)"
        @changed="afterClose"
        @count="pastCount = $event"
      />
    </div>
  </div>
</template>
