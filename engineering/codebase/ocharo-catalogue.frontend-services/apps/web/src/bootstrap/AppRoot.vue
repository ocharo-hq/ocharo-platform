<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import { Boxes, BookOpen, Layers, Settings, Shirt, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { AppErrorBoundary, RouteAnnouncer } from '@wow-two-beta/ui-vue/router';
import { AppShell, AppShellHeader, AppShellMain, Navbar } from '@wow-two-beta/ui-vue/presentation/layout';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/ui-vue/presentation/overlays';
import { useCatalogueStore } from '../application/catalogue';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { useErrorToast } from '../application/common/UseFeedback';
import FeedbackHost from '../presentation/common/FeedbackHost.vue';
import BuildIdentity from '../presentation/common/BuildIdentity.vue';
import CatalogueSelect from '../presentation/common/CatalogueSelect.vue';

defineOptions({ name: 'AppRoot' });
const store = useCatalogueStore();
const route = useRoute();
const { t, uiLocale, setUiLocale } = useUiLocale();
const preferences = ref(false);
const theme = ref('system');
const navigation = [
  { path: '/', label: 'Products', icon: Shirt },
  { path: '/catalogues', label: 'Catalogues', icon: BookOpen },
  { path: '/collections', label: 'Collections', icon: Layers },
  { path: '/stock', label: 'Stock', icon: Boxes },
  { path: '/settings', label: 'Settings', icon: Settings },
];
function active(path: string) {
  return path === '/' ? route.path === '/' || route.path.startsWith('/products') : route.path.startsWith(path);
}
const title = computed(() => navigation.find((item) => active(item.path))?.label ?? 'Catalogue');
const publicRoute = computed(() => route.path.startsWith('/c/'));
const dark = ref(false);
function applyTheme() {
  dark.value =
    theme.value === 'dark' || (theme.value === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.classList.toggle('dark', dark.value);
  document.documentElement.style.colorScheme = dark.value ? 'dark' : 'light';
}
watch(theme, () => {
  applyTheme();
  try {
    localStorage.setItem('ocharo.catalogue.theme', theme.value);
  } catch {
    /* current tab still updates */
  }
});
watch(
  [title, uiLocale],
  () => {
    document.title = `${t(title.value)} · Ocharo Catalogue`;
  },
  { immediate: true },
);
useErrorToast(store.error);
onMounted(() => {
  if (!publicRoute.value) void store.loadSettings();
  try {
    const saved = localStorage.getItem('ocharo.catalogue.theme');
    if (saved && ['light', 'dark', 'system'].includes(saved)) theme.value = saved;
  } catch {
    /* storage optional */
  }
  applyTheme();
});
</script>
<template>
  <AppShell sidebar-width="0px" aside-width="0px" class="min-h-screen bg-background text-foreground">
    <FeedbackHost />
    <a
      href="#main-content"
      class="no-print sr-only focus:not-sr-only focus:fixed focus:left-5 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-card focus:px-5 focus:py-3"
      >{{ t('Skip to content') }}</a
    >
    <AppShellHeader
      class="no-print border-b border-border bg-card/95 backdrop-blur max-lg:h-auto max-lg:flex-col max-lg:items-stretch max-lg:gap-0"
    >
      <Navbar :bordered="false" container-class="max-w-[1800px] px-5 sm:px-8" class="shrink-0 bg-card">
        <template #start>
          <RouterLink
            to="/catalogues"
            class="flex min-h-11 shrink-0 flex-col items-center justify-center gap-0.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-card"
            aria-label="Ocharo Catalogue"
          >
            <img
              :src="dark ? '/brand/ocharo-wordmark-reverse.png' : '/brand/ocharo-wordmark-ink.png'"
              alt="Ocharo"
              class="h-[30px] w-20 shrink-0 object-contain"
            />
            <span
              class="whitespace-nowrap text-[10px] font-medium uppercase leading-3 tracking-[.28em] text-muted-foreground"
              >{{ t('Catalogue') }}</span
            >
          </RouterLink>
        </template>
        <template #center>
          <nav v-if="!publicRoute" :aria-label="t('Main navigation')" class="hidden gap-1 lg:flex">
            <RouterLink
              v-for="item in navigation"
              :key="item.path"
              :to="item.path"
              :aria-current="active(item.path) ? 'page' : undefined"
              :class="[
                'flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm',
                active(item.path)
                  ? 'bg-primary-soft font-semibold text-primary'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              ]"
              ><Icon :icon="item.icon" :size="17" />{{ t(item.label) }}</RouterLink
            >
          </nav>
        </template>
        <template #end
          ><button
            type="button"
            class="btn-outline min-h-11 min-w-11 rounded-full lg:min-h-0 lg:min-w-0"
            :aria-label="t('Preferences')"
            @click="preferences = true"
          >
            <Icon :icon="Settings" :size="18" /><span class="hidden xl:inline">{{ t('Preferences') }}</span>
          </button></template
        >
      </Navbar>
      <nav
        v-if="!publicRoute"
        :aria-label="t('Main navigation')"
        class="flex w-full shrink-0 gap-2 overflow-x-auto border-t border-border px-4 py-2 lg:hidden"
      >
        <RouterLink
          v-for="item in navigation"
          :key="item.path"
          :to="item.path"
          :aria-current="active(item.path) ? 'page' : undefined"
          :class="[
            'flex min-h-11 shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-xs',
            active(item.path) ? 'bg-primary-soft text-primary' : 'text-muted-foreground',
          ]"
          ><Icon :icon="item.icon" :size="15" />{{ t(item.label) }}</RouterLink
        >
      </nav>
    </AppShellHeader>
    <AppShellMain v-bind="{ id: 'main-content', tabindex: -1 }" class="w-full bg-background">
      <div class="mx-auto w-full max-w-[1800px] px-5 py-7 sm:px-8 xl:px-10 print:max-w-none print:p-0">
        <div
          v-if="store.notice.value"
          role="status"
          class="no-print mb-5 flex justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-3 text-sm text-success-soft-foreground"
        >
          <p>{{ store.notice.value }}</p>
          <button :aria-label="t('Close')" @click="store.notice.value = null"><Icon :icon="X" :size="16" /></button>
        </div>
        <AppErrorBoundary>
          <RouterView v-slot="{ Component }"><component :is="Component" :key="route.fullPath" /></RouterView>
          <template #fallback="{ reset }"
            ><section class="panel p-10 text-center">
              <h1 class="font-editorial text-2xl">{{ t('Could not open this page') }}</h1>
              <button class="btn-primary mt-5" @click="reset">{{ t('Retry') }}</button>
            </section></template
          >
        </AppErrorBoundary>
      </div>
      <RouteAnnouncer />
    </AppShellMain>
    <Modal v-model:open="preferences"
      ><ModalContent :is-blurred="true" class="w-[min(28rem,calc(100vw-2rem))] bg-card text-foreground"
        ><ModalTitle class="font-editorial text-2xl">{{ t('Preferences') }}</ModalTitle>
        <div class="mt-6 space-y-5">
          <div>
            <label class="label">{{ t('Interface language') }}</label
            ><CatalogueSelect
              :model-value="uiLocale"
              :label="t('Interface language')"
              :options="[
                { value: 'uz', label: 'O‘zbekcha' },
                { value: 'ru', label: 'Русский' },
                { value: 'en', label: 'English' },
              ]"
              @update:model-value="setUiLocale"
            />
          </div>
          <div>
            <label class="label">{{ t('Appearance') }}</label
            ><CatalogueSelect
              v-model="theme"
              :label="t('Appearance')"
              :options="[
                { value: 'system', label: t('System') },
                { value: 'light', label: t('Light') },
                { value: 'dark', label: t('Dark') },
              ]"
            />
          </div>
          <div class="border-t border-border pt-4 text-xs text-muted-foreground">
            <p class="mb-2">{{ t('About') }}</p>
            <BuildIdentity />
          </div>
          <button class="btn-primary w-full" @click="preferences = false">{{ t('Done') }}</button>
        </div>
      </ModalContent></Modal
    >
  </AppShell>
</template>
