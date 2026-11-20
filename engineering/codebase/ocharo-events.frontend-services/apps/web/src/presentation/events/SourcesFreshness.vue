<script setup lang="ts">
import { computed } from 'vue';
import { Clock, TriangleAlert } from 'lucide-vue-next';
import { formatRelativeTime, toRelativeTimeParts } from '@wow-two-beta/fe/foundation/i18n';

import { useSourcesFreshness } from '@/application/events/UseSourcesFreshness';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { readIntakeFreshness } from '@/domain/events/IntakeFreshness';

/**
 * "Sources updated 3 hours ago" (E24), from WoW2 Events' freshness read of the catalog that the frame makes once
 * (`provideSourcesFreshness`), so the team can tell a quiet week from a stalled SCFD pull: the line turns to a warning
 * when the last record is more than 24 hours old or a source failed its last poll (with how many). It stands at the foot of
 * the sidebar and, on a phone, which has no sidebar, beside the feed's title. Compact it is only its icon (a clock, or a
 * warning sign), with the full text as its tooltip and accessible name. A read that fails, or no frame around it, shows
 * nothing — the feed's own failure notice (OE07) is the page's error surface, and this line adds none.
 */
defineOptions({ name: 'SourcesFreshness' });
defineProps<{
  /** Whether only the icon fits (the sidebar as an icon rail, a phone's title row); the text stays as its tooltip and name. */
  compact?: boolean;
}>();
const { t, uiLocale } = useUiLocale();
const state = useSourcesFreshness();
const freshness = computed(() => state?.freshness.value ?? null);
const now = computed(() => state?.now.value ?? Date.now());

const view = computed(() => (freshness.value ? readIntakeFreshness(freshness.value, now.value) : null));
const text = computed(() => {
  const current = view.value;
  if (!current) return '';
  const parts: string[] = [];
  const age = current.receivedAt === null ? null : toRelativeTimeParts(current.receivedAt, now.value);
  if (age)
    parts.push(
      t('Sources updated {time}', {
        time: formatRelativeTime(age.value, age.unit, uiLocale.value, { numeric: 'auto' }),
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
    class="flex items-start gap-2 text-xs leading-5"
    :class="[view.warns ? 'text-warning' : 'text-muted-foreground', compact ? 'justify-center' : '']"
    :role="view.warns ? 'status' : undefined"
    :data-state="view.warns ? 'warning' : 'fresh'"
    :data-compact="compact ? '' : undefined"
    data-test="sources-freshness"
  >
    <span v-if="compact" role="img" :aria-label="text" :title="text" class="grid size-5 place-items-center">
      <component :is="view.warns ? TriangleAlert : Clock" :size="16" aria-hidden="true" />
    </span>
    <template v-else>
      <component :is="view.warns ? TriangleAlert : Clock" :size="14" aria-hidden="true" class="mt-[3px] shrink-0" />
      <span class="min-w-0">{{ text }}</span>
    </template>
  </p>
</template>
