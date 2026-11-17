<script setup lang="ts">
import { Badge } from '@wow-two-beta/fe/presentation/display';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { attendanceLabels, happeningLabels, happeningTones, type ShowcaseEvent } from '@/domain/events/Events';

/** The badges that say an event no longer runs as listed, or is attended online; a plain in-person event shows none. */
defineOptions({ name: 'EventStateBadges' });
defineProps<{ event: Pick<ShowcaseEvent, 'eventStatus' | 'attendance'> }>();
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
</template>
