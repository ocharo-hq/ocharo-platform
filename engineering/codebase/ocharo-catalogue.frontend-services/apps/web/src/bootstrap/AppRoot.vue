<script setup lang="ts">
import { useSsoSession } from '../application/common/UseSsoSession';
const { session: ssoSession } = useSsoSession();
import SsoConnection from '../presentation/common/SsoConnection.vue';
import { computed, onMounted, ref, watch } from 'vue';
import { provideLocale } from '@wow-two-beta/fe/foundation/i18n';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import { Boxes, BookOpen, Layers, Settings, Shirt, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { AppErrorBoundary, RouteAnnouncer } from '@wow-two-beta/fe/router';
import { AppShell, AppShellHeader, AppShellMain, Navbar } from '@wow-two-beta/fe/presentation/layout';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import { useCatalogueStore } from '../application/catalogue';
import { useUiLocale } from '../application/shared/UseUiLocale';
import FeedbackHost from '../presentation/common/FeedbackHost.vue';
import InterfacePreferences from '../presentation/common/InterfacePreferences.vue';
import { useAppearance } from '../application/shared/UseAppearance';
import OcharoAppsMenu from '../presentation/common/OcharoAppsMenu.vue';
import { platformLinks } from './PlatformLinks';

defineOptions({ name: 'AppRoot' });
const store = useCatalogueStore();
const route = useRoute();
const { t, uiLocale } = useUiLocale();
const preferences = ref(false);
const { dark, initializeAppearance } = useAppearance();
provideLocale(
  uiLocale,
  computed(() => ({
    'OverlayCloseButton.close': t('Close'),
    'SelectPickerContent.searchPlaceholder': t('Search…'),
    'SelectPickerContent.noResultsLabel': t('No results'),
    'Toast.closeLabel': t('Close'),
    'Alert.closeLabel': t('Close'),
    'Banner.closeLabel': t('Close'),
    'Tag.closeLabel': t('Remove'),
    'Spinner.label': t('Loading…'),
  })),
);
const navigation = [
  { path: '/catalogues', label: 'Catalogues', icon: BookOpen },
  { path: '/collections', label: 'Collections', icon: Layers },
  { path: '/', label: 'Products', icon: Shirt },
  { path: '/stock', label: 'Stock', icon: Boxes },
  { path: '/settings', label: 'Settings', icon: Settings },
];
function active(path: string) {
  return path === '/' ? route.path === '/' || route.path.startsWith('/products') : route.path.startsWith(path);
}
const title = computed(() => navigation.find((item) => active(item.path))?.label ?? 'Catalogue');
const publicRoute = computed(() => route.path.startsWith('/c/'));
watch(
  [title, uiLocale],
  () => {
    document.title = `${t(title.value)} · Ocharo Catalogue`;
  },
  { immediate: true },
);
initializeAppearance();
onMounted(() => {
  if (!publicRoute.value) void store.loadSettings();
});
</script>
<template>
  <AppShell sidebar-width="0px" aside-width="0px" class="catalogue-canvas min-h-screen bg-background text-foreground">
    <FeedbackHost />
    <a
      href="#main-content"
      class="no-print sr-only focus:not-sr-only focus:fixed focus:left-5 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-card focus:px-5 focus:py-3"
      >{{ t('Skip to content') }}</a
    >
    <AppShellHeader
      class="catalogue-chrome no-print border-b border-border bg-chrome max-lg:h-auto max-lg:flex-col max-lg:items-stretch max-lg:gap-0"
    >
      <Navbar :has-border="false" container-class="max-w-[1800px] px-5 sm:px-8" class="shrink-0 bg-transparent">
        <template #start>
          <OcharoAppsMenu
            v-if="!publicRoute"
            :apps="platformLinks"
            :logo-src="dark ? '/brand/ocharo-wordmark-reverse.png' : '/brand/ocharo-wordmark-ink.png'"
          />
          <RouterLink
            v-else
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
                  ? 'bg-primary-soft font-semibold text-primary-soft-foreground'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              ]"
              ><Icon :icon="item.icon" :size="17" />{{ t(item.label) }}</RouterLink
            >
          </nav>
        </template>
        <template #end>
          <a
            v-if="ssoSession.enabled && ssoSession.accountUrl"
            :href="ssoSession.accountUrl"
            target="_blank"
            rel="noopener"
            class="btn-outline"
            >{{ t('Account') }}</a
          >
          <div v-if="publicRoute" class="flex shrink-0 items-center gap-2">
            <button
              type="button"
              class="btn-outline min-h-11 min-w-11 rounded-full lg:min-h-0 lg:min-w-0"
              :aria-label="t('Settings')"
              @click="preferences = true"
            >
              <Icon :icon="Settings" :size="18" /><span class="hidden xl:inline">{{ t('Settings') }}</span>
            </button>
          </div>
        </template>
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
            active(item.path) ? 'bg-primary-soft text-primary-soft-foreground' : 'text-muted-foreground',
          ]"
          ><Icon :icon="item.icon" :size="15" />{{ t(item.label) }}</RouterLink
        >
      </nav>
    </AppShellHeader>
    <AppShellMain v-bind="{ id: 'main-content', tabindex: -1 }" class="catalogue-main w-full bg-background">
      <div class="mx-auto w-full max-w-[1800px] px-5 py-7 sm:px-8 xl:px-10 print:max-w-none print:p-0">
        <div
          v-if="store.notice.value"
          role="status"
          class="no-print mb-5 flex justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-3 text-sm text-success-soft-foreground"
        >
          <p>{{ store.notice.value }}</p>
          <button :aria-label="t('Close')" @click="store.notice.value = null"><Icon :icon="X" :size="16" /></button>
        </div>
        <SsoConnection v-if="!publicRoute" />
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
        ><ModalTitle class="font-editorial text-2xl">{{ t('Settings') }}</ModalTitle>
        <div class="mt-6 space-y-5">
          <InterfacePreferences />
          <button class="btn-primary w-full" @click="preferences = false">{{ t('Done') }}</button>
        </div>
      </ModalContent></Modal
    >
  </AppShell>
</template>
