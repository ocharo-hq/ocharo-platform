<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { BottomNavMenu, BottomNavMenuItem } from '@wow-two-beta/fe/presentation/nav';

import { useEventsSections } from '../../application/events/UseEventsSections';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { EventsSection } from '../../domain/shared/EventsSections';
import { SectionIcons } from './ShellIcons';

/**
 * The phone's navigation: a bottom bar with every section the session is offered, three or four (the feed, the calendar,
 * the reminders and, for an admin, the Telegram delivery), so none hides behind a menu. The frame places the bar below the
 * scrolling region and hides it from tablets up and in print.
 */
defineOptions({ name: 'PhoneNav' });
const props = defineProps<{ current: EventsSection | null }>();
const { t } = useUiLocale();
const { links } = useEventsSections();
</script>

<template>
  <BottomNavMenu :is-fixed="false" v-bind="{ 'aria-label': t('Events sections') }" variant="glass">
    <BottomNavMenuItem v-for="section in links" :key="section.id" as-child :is-active="props.current === section.id">
      <template #icon><component :is="SectionIcons[section.id]" :size="20" /></template>
      <RouterLink :to="section.href">{{ t(section.shortLabel ?? section.label) }}</RouterLink>
    </BottomNavMenuItem>
  </BottomNavMenu>
</template>
