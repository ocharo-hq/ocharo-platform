<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { SidebarMenu, SidebarMenuItem, SidebarMenuSection } from '@wow-two-beta/fe/presentation/nav';

import { useUiLocale } from '../../application/shared/UseUiLocale';
import {
  CatalogueGroupLabels,
  CatalogueSectionGroup,
  CatalogueSections,
  type CatalogueSection,
} from '../../domain/shared/CatalogueSections';
import { SectionIcons } from './ShellIcons';

/**
 * Catalogue's sidebar: the authoring sections, then Admin under its heading. As an icon rail the labels stay for
 * assistive technology and show as tooltips. The frame owns the collapse control.
 */
defineOptions({ name: 'CatalogueNav' });
const props = defineProps<{ current: CatalogueSection | null; collapsed: boolean }>();
const { t } = useUiLocale();
const groups = computed(() =>
  Object.values(CatalogueSectionGroup).map((group) => ({
    group,
    label: CatalogueGroupLabels[group],
    sections: CatalogueSections.filter((section) => section.group === group),
  })),
);
const labelClass = computed(() => (props.collapsed ? 'sr-only' : 'min-w-0 flex-1 truncate text-start'));
</script>

<template>
  <SidebarMenu
    v-bind="{ 'aria-label': t('Catalogue sections') }"
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
          <RouterLink :to="section.path" :title="props.collapsed ? t(section.label) : undefined"
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
          <RouterLink :to="section.path" :title="props.collapsed ? t(section.label) : undefined"
            ><span :class="labelClass">{{ t(section.label) }}</span></RouterLink
          >
        </SidebarMenuItem>
      </SidebarMenuSection>
    </template>
  </SidebarMenu>
</template>
