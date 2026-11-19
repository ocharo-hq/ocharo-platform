<script setup lang="ts">
import { computed } from 'vue';
import { toRelativeTimeParts } from '@wow-two-beta/fe/foundation/i18n';
import type { EventsClient } from '@wow-two-beta/fe-events/kit';

import { useIntakeFreshness } from '@/application/events/UseEvents';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { readIntakeFreshness } from '@/domain/events/IntakeFreshness';

/**
 * "Sources updated 3 hours ago" under the page title (E24), from WoW2 Events' freshness read of the catalog, so the team
 * can tell a quiet week from a stalled SCFD pull: the line turns to a warning when the last record is more than 24 hours
 * old or a source failed its last poll (with how many). A read that fails shows nothing — the feed's own failure notice
 * (OE07) is the page's error surface, and this line adds none.
 */
defineOptions({ name: 'SourcesFreshness' });
const props = defineProps<{
  client: EventsClient;
  catalog: string;
}>();
const { t, uiLocale } = useUiLocale();
const { freshness, now } = useIntakeFreshness(props.client, props.catalog);

const view = computed(() => (freshness.value ? readIntakeFreshness(freshness.value, now.value) : null));
const text = computed(() => {
  const current = view.value;
  if (!current) return '';
  const parts: string[] = [];
  const age = current.receivedAt === null ? null : toRelativeTimeParts(current.receivedAt, now.value);
  if (age)
    parts.push(
      t('Sources updated {time}', {
        time: new Intl.RelativeTimeFormat(uiLocale.value, { numeric: 'auto' }).format(age.value, age.unit),
      }),
    );
  if (current.stale) parts.push(t('No new records in over 24 hours.'));
  if (current.failingSources > 0) parts.push(t('Sources failing: {count}', { count: current.failingSources }));
  return parts.join(' · ');
});
</script>

<template>
  <p
    v-if="view && text"
    class="text-sm"
    :class="view.warns ? 'text-warning' : 'text-muted-foreground'"
    :role="view.warns ? 'status' : undefined"
    :data-state="view.warns ? 'warning' : 'fresh'"
    data-test="sources-freshness"
  >
    {{ text }}
  </p>
</template>
