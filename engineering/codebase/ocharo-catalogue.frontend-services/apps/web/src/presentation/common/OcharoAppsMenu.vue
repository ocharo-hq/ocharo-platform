<script setup lang="ts">
import { ref, type HTMLAttributes } from 'vue';
import { RouterLink } from 'vue-router';
import { Grip } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { Popover, PopoverContent, PopoverTrigger } from '@wow-two-beta/ui-vue/presentation/overlays';
import { useUiLocale } from '../../application/shared/UseUiLocale';

defineOptions({ name: 'OcharoAppsMenu' });
defineProps<{ apps: readonly { key: string; label: string; url: string }[] }>();
const { t } = useUiLocale();
const open = ref(false);
const panelAttrs = { 'aria-labelledby': 'ocharo-apps-title' } satisfies Pick<HTMLAttributes, 'aria-labelledby'>;
const tileClass =
  'flex min-h-16 items-center justify-center rounded-xl px-2 py-3 text-center text-sm font-medium leading-5 break-words transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';
</script>

<template>
  <Popover v-model:open="open" placement="bottom-end">
    <PopoverTrigger as-child>
      <button
        type="button"
        :aria-label="t('Ocharo apps')"
        :title="t('Ocharo apps')"
        class="btn-outline size-11 min-h-11 min-w-11 rounded-full p-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Icon :icon="Grip" :size="20" />
      </button>
    </PopoverTrigger>
    <PopoverContent
      v-bind="panelAttrs"
      class="no-print w-[min(22rem,calc(100vw-2rem))] rounded-2xl border-border bg-card p-4 text-foreground shadow-xl"
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
