<script setup lang="ts">
import { computed, ref } from 'vue';
import { RouterLink, RouterView } from 'vue-router';
import { Settings } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { useColorMode } from '@wow-two-beta/fe/foundation/primitives';
import { AppShell, AppShellHeader, AppShellMain, Navbar } from '@wow-two-beta/fe/presentation/layout';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import InterfacePreferences from '../presentation/common/InterfacePreferences.vue';

/**
 * The published reader's own frame, outside the operator frame: the Ocharo wordmark and a Settings dialog limited to
 * this browser's interface language and appearance. It loads no operator data.
 */
defineOptions({ name: 'PublicReaderLayout' });
const { t } = useUiLocale();
const colorMode = useColorMode();
const preferences = ref(false);
const wordmark = computed(() =>
  colorMode.mode === 'dark' ? '/brand/ocharo-wordmark-reverse.png' : '/brand/ocharo-wordmark-ink.png',
);
</script>

<template>
  <AppShell class="catalogue-frame">
    <AppShellHeader variant="glass" class="no-print">
      <Navbar :has-border="false" container-class="max-w-[1800px] px-5 sm:px-8" class="shrink-0 bg-transparent">
        <template #start>
          <RouterLink
            to="/catalogues"
            class="flex min-h-11 shrink-0 flex-col items-center justify-center gap-0.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-card"
            aria-label="Ocharo Catalogue"
          >
            <img :src="wordmark" alt="Ocharo" class="h-[30px] w-20 shrink-0 object-contain" />
            <span
              class="whitespace-nowrap text-[10px] font-medium uppercase leading-3 tracking-[.28em] text-muted-foreground"
              >{{ t('Catalogue') }}</span
            >
          </RouterLink>
        </template>
        <template #end>
          <button
            type="button"
            class="btn-outline min-h-11 min-w-11 rounded-full lg:min-h-0 lg:min-w-0"
            :aria-label="t('Settings')"
            @click="preferences = true"
          >
            <Icon :icon="Settings" :size="18" /><span class="hidden xl:inline">{{ t('Settings') }}</span>
          </button>
        </template>
      </Navbar>
    </AppShellHeader>
    <AppShellMain class="w-full bg-background">
      <div class="mx-auto w-full max-w-[1800px] px-5 py-7 sm:px-8 xl:px-10 print:max-w-none print:p-0">
        <AppErrorBoundary>
          <RouterView v-slot="{ Component, route }"><component :is="Component" :key="route.fullPath" /></RouterView>
          <template #fallback="{ reset }"
            ><section class="panel p-10 text-center">
              <h1 class="font-editorial text-2xl">{{ t('Could not open this page') }}</h1>
              <button class="btn-primary mt-5" @click="reset">{{ t('Retry') }}</button>
            </section></template
          >
        </AppErrorBoundary>
      </div>
    </AppShellMain>
    <Modal v-model:open="preferences"
      ><ModalContent :is-blurred="true" class="w-[min(28rem,calc(100vw-2rem))] bg-card text-foreground"
        ><ModalTitle class="font-editorial text-2xl">{{ t('Settings') }}</ModalTitle>
        <div class="mt-6 space-y-5">
          <InterfacePreferences />
          <button class="btn-primary w-full" @click="preferences = false">{{ t('Done') }}</button>
        </div>
      </ModalContent></Modal
    >
  </AppShell>
</template>
