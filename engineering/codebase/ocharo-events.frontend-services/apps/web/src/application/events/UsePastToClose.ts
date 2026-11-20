import { onMounted, onScopeDispose, shallowRef, watch } from 'vue';
import type { TrackedEventModel, TrackingSpec } from '@wow-two-beta/fe-events/contracts';
import { isEventPast, type EventsClient, type EventsFailure } from '@wow-two-beta/fe-events/kit';

import { ocharoPursuingStages, previousIsoDate, type EventStatus } from '@/domain/events/Events';

/** The most events one read lists; a team closing more than this closes them a page at a time. */
const pageSize = 50;

/**
 * "Past, not closed" (OE12): the events that have ended while Ocharo was still pursuing them (shortlisted, applying,
 * applied or confirmed), each closed with one tracking write as Attended or Skipped. Events lists them server-side by
 * stage and by a start no later than yesterday; the hook keeps those whose last day has passed. An ended event Ocharo
 * never tracked needs no action, so it never appears. `count` is how many wait: the feed's attention pill needs it before
 * its strip is unfolded, so the read lives here, on the page, and the strip only shows what this hook holds.
 *
 * A read that fails leaves the list as it was and sets `failure`, which the strip shows with a retry once unfolded. A
 * close that meets a newer version (a conflict or a missing event) reads the list again and raises `changedNotice`.
 * `closed` fires after each successful close, so the page can read the views that show the event's new stage.
 */
export function usePastToClose(source: {
  readonly client: () => EventsClient;
  readonly catalog: () => string;
  /** The person's calendar day, `YYYY-MM-DD`. */
  readonly today: () => string;
  readonly closed?: () => void;
}) {
  const items = shallowRef<readonly TrackedEventModel[]>([]);
  const failure = shallowRef<EventsFailure | null>(null);
  /** The id of the event being closed, `null` while no write is in flight. */
  const writing = shallowRef<string | null>(null);
  const changedNotice = shallowRef(false);
  const count = shallowRef(0);
  let generation = 0;

  function setItems(next: readonly TrackedEventModel[]): void {
    items.value = next;
    count.value = next.length;
  }

  /** Reads the events waiting to be closed; the latest read wins when several overlap. */
  async function reload(): Promise<void> {
    const run = ++generation;
    const result = await source.client().listTracking(source.catalog(), {
      stages: [...ocharoPursuingStages],
      to: previousIsoDate(source.today()),
      includeUndated: false,
      limit: pageSize,
    });
    if (run !== generation) return;
    if (!result.ok) {
      failure.value = result.failure;
      return;
    }
    failure.value = null;
    setItems(result.value.items.filter((item) => item.tracking && isEventPast(item.event, source.today())));
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
    const result = await source.client().putTracking(source.catalog(), item.event.id, spec);
    writing.value = null;
    if (result.ok) {
      setItems(items.value.filter((entry) => entry.event.id !== item.event.id));
      source.closed?.();
      return;
    }
    if (result.failure.reason === 'conflict' || result.failure.reason === 'notFound') {
      changedNotice.value = true;
      await reload();
      return;
    }
    failure.value = result.failure;
  }

  onMounted(reload);
  watch([source.today, source.client, source.catalog], () => void reload());
  onScopeDispose(() => {
    generation++;
  });
  return { items, failure, writing, changedNotice, count, reload, close };
}
