<script setup lang="ts">
import { computed, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { Ellipsis, X } from 'lucide-vue-next';
import { BottomNavMenu, BottomNavMenuItem } from '@wow-two-beta/fe/presentation/nav';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { DeskSections, deskSectionPath, type DeskSection } from '@/domain/workspace/DeskSections';

import { SectionIcons } from './ShellIcons';

/**
 * The phone's navigation: a bottom bar of the four sections used most, then More, which opens every other section as
 * a full-screen dialog. The bar sits in the shell's footer row, so the scrolling region ends above it.
 */
defineOptions({ name: 'PhoneNav' });
const props = defineProps<{ organizationId: string; current: DeskSection }>();
const { t } = useUiLocale();
const moreOpen = ref(false);
const barSections = DeskSections.filter((section) => section.onPhoneBar);
const moreSections = DeskSections.filter((section) => !section.onPhoneBar);
/** More reads as current while one of its sections is open. */
const moreIsCurrent = computed(() => moreSections.some((section) => section.id === props.current));
</script>

<template>
  <BottomNavMenu
    :is-fixed="false"
    :aria-label="t('Desk sections')"
    variant="glass"
    class="z-sticky [grid-area:footer] md:hidden"
  >
    <BottomNavMenuItem v-for="section in barSections" :key="section.id" as-child :is-active="props.current === section.id">
      <template #icon><component :is="SectionIcons[section.id]" :size="20" /></template>
      <RouterLink :to="deskSectionPath(props.organizationId, section.id)">{{ t(section.shortLabel) }}</RouterLink>
    </BottomNavMenuItem>
    <BottomNavMenuItem as-child :is-active="moreIsCurrent">
      <template #icon><Ellipsis :size="20" /></template>
      <button type="button" aria-haspopup="dialog" :aria-expanded="moreOpen" @click="moreOpen = true">{{ t('More') }}</button>
    </BottomNavMenuItem>
  </BottomNavMenu>
  <Modal v-model:open="moreOpen">
    <ModalContent v-if="moreOpen" variant="glass-menu" class="flex max-h-dvh flex-col gap-4 overflow-y-auto p-5 pt-[calc(1.25rem+env(safe-area-inset-top))] text-foreground sm:max-w-md sm:rounded-2xl">
      <div class="flex items-center justify-between gap-3">
        <ModalTitle class="text-lg font-semibold">{{ t('More in Desk') }}</ModalTitle>
        <button
          type="button"
          class="grid size-11 place-items-center rounded-xl text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          :aria-label="t('Close')"
          @click="moreOpen = false"
        >
          <X :size="20" aria-hidden="true" />
        </button>
      </div>
      <nav :aria-label="t('More in Desk')">
        <ul role="list" class="flex flex-col gap-1">
          <li v-for="section in moreSections" :key="section.id">
            <RouterLink
              :to="deskSectionPath(props.organizationId, section.id)"
              :aria-current="props.current === section.id ? 'page' : undefined"
              class="flex min-h-12 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring aria-[current=page]:bg-primary-soft aria-[current=page]:text-primary-soft-foreground"
              @click="moreOpen = false"
            >
              <span class="oc-app-icon"><component :is="SectionIcons[section.id]" :size="18" /></span>{{ t(section.label) }}
            </RouterLink>
          </li>
        </ul>
      </nav>
    </ModalContent>
  </Modal>
</template>
