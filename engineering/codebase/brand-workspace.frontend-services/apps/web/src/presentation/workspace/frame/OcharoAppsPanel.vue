<script setup lang="ts">
import { computed } from 'vue';
import { ExternalLink } from 'lucide-vue-next';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { OcharoAppKind, type OcharoApp } from '@/domain/shared/OcharoApps';

import { AppIcons } from './ShellIcons';

/** The app menu's body: the workspace apps as tiles, then the public sites, which open in a new tab. */
defineOptions({ name: 'OcharoAppsPanel' });
const props = defineProps<{ apps: readonly OcharoApp[]; current: OcharoApp['key'] }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useUiLocale();
const workspaceApps = computed(() => props.apps.filter((app) => app.kind === OcharoAppKind.Workspace));
const publicSites = computed(() => props.apps.filter((app) => app.kind === OcharoAppKind.PublicSite));
const tileClass =
  'flex min-h-14 items-center gap-3 rounded-xl px-2.5 py-2 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';
</script>

<template>
  <div class="flex flex-col gap-3">
    <ul role="list" class="grid grid-cols-1 gap-1 sm:grid-cols-2">
      <li v-for="app in workspaceApps" :key="app.key">
        <button
          v-if="app.key === props.current"
          type="button"
          aria-current="true"
          :class="[tileClass, 'w-full bg-primary-soft']"
          @click="emit('close')"
        >
          <span class="oc-app-icon oc-app-icon-current"><component :is="AppIcons[app.key]" :size="18" /></span>
          <span class="min-w-0"
            ><b class="block text-sm font-semibold text-primary-soft-foreground">{{ t(app.label) }}</b
            ><span class="block truncate text-xs text-muted-foreground">{{ t(app.summary) }}</span></span
          >
        </button>
        <a v-else :href="app.url" :class="[tileClass, 'hover:bg-muted']" @click="emit('close')">
          <span class="oc-app-icon"><component :is="AppIcons[app.key]" :size="18" /></span>
          <span class="min-w-0"
            ><b class="block text-sm font-semibold text-foreground">{{ t(app.label) }}</b
            ><span class="block truncate text-xs text-muted-foreground">{{ t(app.summary) }}</span></span
          >
        </a>
      </li>
    </ul>
    <template v-if="publicSites.length">
      <div class="h-px bg-border" />
      <h3 class="px-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">{{ t('Public sites') }}</h3>
      <ul role="list" class="flex flex-wrap gap-2 px-1">
        <li v-for="site in publicSites" :key="site.key">
          <a
            :href="site.url"
            target="_blank"
            rel="noopener noreferrer"
            class="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-border px-3 text-sm text-muted-foreground transition-colors hover:border-input hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            @click="emit('close')"
            >{{ t(site.label) }}<ExternalLink :size="14" aria-hidden="true" /><span class="sr-only">{{
              t('(opens in a new tab)')
            }}</span></a
          >
        </li>
      </ul>
    </template>
  </div>
</template>
