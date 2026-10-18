<script setup lang="ts">
import { computed, ref } from 'vue';
import { ChevronDown, X } from 'lucide-vue-next';
import { TailwindBreakpoints, useBreakpoint } from '@wow-two-beta/fe/foundation/device';
import {
  Modal,
  ModalContent,
  ModalTitle,
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@wow-two-beta/fe/presentation/overlays';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoApps, type OcharoApp } from '@/domain/shared/OcharoApps';

import OcharoAppsPanel from './OcharoAppsPanel.vue';

/**
 * The app button: the Ocharo mark, the current app and its organization, and a chevron, as one control. It opens the
 * Ocharo app menu — a panel under the button from tablets up, a full-screen dialog on phones.
 */
defineOptions({ name: 'OcharoAppsMenu' });
const props = defineProps<{ current: OcharoApp['key']; label: string; subtitle?: string | null }>();
const { t } = useUiLocale();
const open = ref(false);
const breakpoint = useBreakpoint(TailwindBreakpoints);
/** Below `md`: the menu opens as a full-screen dialog. */
const isPhone = computed(() => !['md', 'lg', 'xl', '2xl'].includes(breakpoint.value ?? ''));
const apps = ocharoApps(import.meta.env as Record<string, string | undefined>);
const triggerClass =
  'oc-app-button flex min-h-11 min-w-0 shrink-0 items-center gap-2.5 rounded-xl border border-transparent py-1 ps-1 pe-2 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';
</script>

<template>
  <div class="flex min-w-0 shrink-0">
    <template v-if="isPhone">
      <button
        type="button"
        :class="[triggerClass, { 'oc-app-button-open': open }]"
        aria-haspopup="dialog"
        :aria-expanded="open"
        :aria-label="t('Ocharo apps — {app}', { app: t(props.label) })"
        @click="open = true"
      >
        <span class="oc-mark" aria-hidden="true">O</span>
        <span class="min-w-0 text-sm font-semibold">{{ t(props.label) }}</span>
        <ChevronDown :size="16" class="oc-app-chevron shrink-0 text-muted-foreground" aria-hidden="true" />
      </button>
      <Modal v-model:open="open">
        <ModalContent v-if="open" class="oc-glass-menu flex max-h-dvh flex-col gap-5 overflow-y-auto p-5 pt-[calc(1.25rem+env(safe-area-inset-top))] text-foreground sm:max-w-lg sm:rounded-2xl">
          <div class="flex items-center justify-between gap-3">
            <ModalTitle class="text-lg font-semibold">{{ t('Ocharo apps') }}</ModalTitle>
            <button
              type="button"
              class="grid size-11 place-items-center rounded-xl text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              :aria-label="t('Close')"
              @click="open = false"
            >
              <X :size="20" aria-hidden="true" />
            </button>
          </div>
          <OcharoAppsPanel :apps="apps" :current="props.current" @close="open = false" />
        </ModalContent>
      </Modal>
    </template>
    <Popover v-else v-model:open="open" placement="bottom-start">
      <PopoverTrigger as-child>
        <button
          type="button"
          :class="[triggerClass, { 'oc-app-button-open': open }]"
          :aria-label="t('Ocharo apps — {app}', { app: t(props.label) })"
        >
          <span class="oc-mark" aria-hidden="true">O</span>
          <span class="min-w-0"
            ><span class="block text-sm font-semibold leading-tight">{{ t(props.label) }}</span
            ><span v-if="props.subtitle" class="block max-w-40 truncate text-xs leading-tight text-muted-foreground">{{
              props.subtitle
            }}</span></span
          >
          <ChevronDown :size="16" class="oc-app-chevron shrink-0 text-muted-foreground" aria-hidden="true" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        aria-labelledby="ocharo-apps-title"
        class="oc-glass-menu w-[min(28rem,calc(100vw-2rem))] rounded-2xl border border-(--oc-glass-edge) p-4 text-foreground shadow-(--oc-menu-shadow)"
      >
        <h2 id="ocharo-apps-title" class="mb-3 px-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          {{ t('Ocharo apps') }}
        </h2>
        <OcharoAppsPanel :apps="apps" :current="props.current" @close="open = false" />
      </PopoverContent>
    </Popover>
  </div>
</template>
