<script setup lang="ts">
import { ref, type HTMLAttributes } from 'vue';
import { RouterLink } from 'vue-router';
import { ChevronDown } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { Popover, PopoverContent, PopoverTrigger } from '@wow-two-beta/ui-vue/presentation/overlays';
import { useUiLocale } from '../../application/shared/UseUiLocale';

defineOptions({ name: 'OcharoAppsMenu' });
defineProps<{ apps: readonly { key: string; label: string; url: string }[]; logoSrc: string }>();
const { t } = useUiLocale();
const open = ref(false);
const panelAttrs = { 'aria-labelledby': 'ocharo-apps-title' } satisfies Pick<HTMLAttributes, 'aria-labelledby'>;
const tileClass =
  'flex min-h-16 items-center justify-center rounded-xl px-2 py-3 text-center text-sm font-medium leading-5 break-words transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';
</script>

<template>
  <Popover v-model:open="open" placement="bottom-start">
    <PopoverTrigger as-child>
      <button
        type="button"
        :aria-label="t('Ocharo apps')"
        :title="t('Ocharo apps')"
        class="flex min-h-11 min-w-11 shrink-0 items-center gap-2 rounded-lg px-2 transition-colors hover:bg-chrome-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-card"
        :class="{ 'bg-chrome-hover': open }"
      >
        <span class="flex flex-col items-center justify-center gap-0.5">
          <img :src="logoSrc" alt="Ocharo" class="h-[30px] w-20 shrink-0 object-contain" />
          <span
            class="whitespace-nowrap text-[10px] font-medium uppercase leading-3 tracking-[.28em] text-muted-foreground"
            >{{ t('Catalogue') }}</span
          >
        </span>
        <Icon
          :icon="ChevronDown"
          :size="16"
          class="shrink-0 text-muted-foreground transition-transform"
          :class="{ 'rotate-180': open }"
        />
      </button>
    </PopoverTrigger>
    <PopoverContent
      v-bind="panelAttrs"
      class="catalogue-apps-panel no-print w-[min(22rem,calc(100vw-2rem))] rounded-2xl border-border bg-menu p-4 text-foreground"
    >
      <h2 id="ocharo-apps-title" class="text-sm font-semibold">{{ t('Ocharo apps') }}</h2>
      <div class="mt-3 grid grid-cols-3 gap-2">
        <template v-for="app in apps" :key="app.key">
          <RouterLink
            v-if="app.key === 'catalogue'"
            to="/catalogues"
            aria-current="true"
            :class="[tileClass, 'bg-primary-soft text-primary']"
            @click="open = false"
            >{{ t(app.label) }}</RouterLink
          >
          <a
            v-else
            :href="app.url"
            target="_blank"
            rel="noopener noreferrer"
            :class="[tileClass, 'text-foreground hover:bg-muted']"
            @click="open = false"
            >{{ t(app.label) }}</a
          >
        </template>
      </div>
    </PopoverContent>
  </Popover>
</template>
