import { computed, onMounted, watch } from 'vue';
import { useEventsRequest, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { ocharoDeadlineKinds } from '@/domain/events/Events';

/**
 * How many nearest deadlines the catalog's tracking summary lists, for the "Deadlines" pill on the feed. The kit's
 * `EventsDeadlines` list exposes no count, so this reads the same summary it reads (the day and the application deadline
 * kinds it is given) through the same client. A read that fails counts none, so the pill stays hidden and the feed's own
 * failure notice remains the page's error surface.
 */
export function useDeadlineCount(source: {
  readonly client: () => EventsClient;
  readonly catalog: () => string;
  /** The person's calendar day, `YYYY-MM-DD`. */
  readonly today: () => string;
}) {
  const request = useEventsRequest((signal) =>
    source
      .client()
      .getTrackingSummary(source.catalog(), { today: source.today(), deadlineKinds: ocharoDeadlineKinds }, signal),
  );
  const count = computed(() => request.value.value?.upcomingDeadlines.length ?? 0);
  const reload = () => request.run();
  onMounted(reload);
  watch([source.client, source.catalog, source.today], reload);
  return { count, reload };
}
