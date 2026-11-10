<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { SidebarMenu, SidebarMenuItem, SidebarMenuSection } from '@wow-two-beta/fe/presentation/nav';

import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import {
  DeskGroupLabels,
  DeskSectionGroup,
  DeskSections,
  deskSectionPath,
  type DeskSection,
} from '@/domain/common/DeskSections';

import { SectionIcons } from './ShellIcons';

/**
 * Desk's sidebar: Overview, then each group of sections under its heading. As an icon
 * rail the labels stay for assistive technology and show as tooltips.
 */
defineOptions({ name: 'DeskNav' });
const props = defineProps<{ organizationId: string; current: DeskSection | null; collapsed: boolean }>();
const { t } = useUiLocale();
const groups = computed(() =>
  Object.values(DeskSectionGroup).map((group) => ({
    group,
    label: DeskGroupLabels[group],
    sections: DeskSections.filter((section) => section.group === group),
  })),
);
const labelClass = computed(() => (props.collapsed ? 'sr-only' : 'min-w-0 flex-1 truncate text-start'));
</script>

<template>
  <div class="flex h-full min-h-0 flex-col">
    <SidebarMenu
      :aria-label="t('Desk sections')"
      :is-collapsed="props.collapsed"
      class="min-h-0 flex-1 overflow-y-auto overflow-x-hidden py-3 [scrollbar-width:thin]"
    >
      <template v-for="entry in groups" :key="entry.group">
        <template v-if="!entry.label">
          <SidebarMenuItem
            v-for="section in entry.sections"
            :key="section.id"
            as-child
            :is-active="props.current === section.id"
          >
            <template #icon><component :is="SectionIcons[section.id]" :size="17" /></template>
            <RouterLink :to="deskSectionPath(props.organizationId, section.id)" :title="props.collapsed ? t(section.label) : undefined"
              ><span :class="labelClass">{{ t(section.label) }}</span></RouterLink
            >
          </SidebarMenuItem>
        </template>
        <SidebarMenuSection v-else :label="t(entry.label)" class="mt-3">
          <SidebarMenuItem
            v-for="section in entry.sections"
            :key="section.id"
            as-child
            :is-active="props.current === section.id"
          >
            <template #icon><component :is="SectionIcons[section.id]" :size="17" /></template>
            <RouterLink :to="deskSectionPath(props.organizationId, section.id)" :title="props.collapsed ? t(section.label) : undefined"
              ><span :class="labelClass">{{ t(section.label) }}</span></RouterLink
            >
          </SidebarMenuItem>
        </SidebarMenuSection>
      </template>
    </SidebarMenu>
  </div>
</template>
