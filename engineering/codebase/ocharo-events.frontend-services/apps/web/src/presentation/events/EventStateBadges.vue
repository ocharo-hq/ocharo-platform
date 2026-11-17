<script setup lang="ts">
import { Badge } from '@wow-two-beta/fe/presentation/display';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  attendanceLabels,
  happeningLabels,
  happeningTones,
  listingRemoved,
  type ShowcaseEvent,
} from '@/domain/events/Events';

/**
 * The badges that say an event no longer runs as listed, is attended online, or left every source that listed it; a
 * plain in-person event shows none.
 */
defineOptions({ name: 'EventStateBadges' });
defineProps<{ event: Pick<ShowcaseEvent, 'eventStatus' | 'attendance' | 'sources'> }>();
const { t } = useUiLocale();
</script>

<template>
  <Badge
    v-if="event.eventStatus !== 'scheduled'"
    size="sm"
    :variant="happeningTones[event.eventStatus]"
    data-test="event-happening"
    >{{ t(happeningLabels[event.eventStatus]) }}</Badge
  >
  <Badge v-if="event.attendance !== 'offline'" size="sm" variant="info" data-test="event-attendance">{{
    t(attendanceLabels[event.attendance])
  }}</Badge>
  <Badge v-if="listingRemoved(event)" size="sm" variant="warning" data-test="listing-removed">{{
    t('Listing removed at source')
  }}</Badge>
</template>
