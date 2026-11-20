<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import type { TrackedEventModel, WorkflowStageModel } from '@wow-two-beta/fe-events/contracts';
import {
  EventsLabels,
  FailureNotice,
  eventTitle,
  formatEventDates,
  type EventsFailure,
} from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoClosingStages, type EventStatus } from '@/domain/events/Events';

/**
 * "Past, not closed" (OE12): the events that have ended while Ocharo was still pursuing them, each closed with one click
 * as Attended or Skipped. It is the body of the feed's "past to close" attention pill (the `#attention` slot of
 * `EventsFeed`), so it only shows what the page's `usePastToClose` holds and asks it to close or reload; the pill, which
 * shows the count and is gone at zero, is the kit's. An ended event Ocharo never tracked needs no action, and the strip
 * hides itself when nothing is left to close.
 */
defineOptions({ name: 'PastEventsStrip' });
defineProps<{
  /** The ended events Ocharo was pursuing, read by `usePastToClose`. */
  items: readonly TrackedEventModel[];
  /** The failure of the last read or write, shown with a retry. */
  failure: EventsFailure | null;
  /** The id of the event being closed, `null` while no write is in flight. */
  writing: string | null;
  /** Whether a close met a newer version, so the list was read again. */
  changedNotice: boolean;
  /** The organization's stages, for their labels. */
  stages: readonly WorkflowStageModel[];
  /** Whether the person may write tracking (`events:track`); the list stays read-only otherwise. */
  canTrack: boolean;
  eventHref: (id: string) => string;
}>();
const emit = defineEmits<{
  open: [eventId: string];
  /** Asks to move the event to the stage, as Attended or Skipped. */
  close: [item: TrackedEventModel, stage: EventStatus];
  retry: [];
}>();
const { t, uiLocale } = useUiLocale();

/** Opens the event inside the page on a plain click; any other click follows the link. */
function follow(event: MouseEvent, id: string): void {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  emit('open', id);
}
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
    <FailureNotice v-if="failure" :failure="failure" :locale="uiLocale" retry @retry="emit('retry')" />
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
            @click="emit('close', item, stage)"
          >
            {{ EventsLabels.stage(stage, stages, uiLocale) }}
          </Button>
        </div>
      </li>
    </ul>
  </section>
</template>
