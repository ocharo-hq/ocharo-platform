<script setup lang="ts">
import FeedbackHost from '../presentation/common/FeedbackHost.vue';
import { useErrorToast } from '../application/common/UseFeedback';
import BuildIdentity from '../presentation/common/BuildIdentity.vue';
import CatalogueDeskEntry from '../presentation/common/CatalogueDeskEntry.vue';
import { computed, onMounted } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import {
  Boxes,
  BookOpen,
  ChevronRight,
  Languages,
  Layers,
  Printer,
  Settings,
  Shirt,
  Tag,
  X,
} from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { AppErrorBoundary, RouteAnnouncer } from '@wow-two-beta/ui-vue/router';
import { useCatalogueStore } from '../application/catalogue';
import { catalogueDeskUrl, platformLinks } from './PlatformLinks';

/** Composes Ocharo Catalogue: products, prices, collections, print sheet and settings. */
defineOptions({ name: 'AppRoot' });
const store = useCatalogueStore();
const route = useRoute();
useErrorToast(store.error);
const navigation = [
  { path: '/', label: 'Mahsulotlar', icon: Shirt },
  { path: '/copy', label: 'Matnlar', icon: Languages },
  { path: '/pricing', label: 'Narxlar', icon: Tag },
  { path: '/stock', label: 'Qoldiq', icon: Boxes },
  { path: '/collections', label: 'To‘plamlar', icon: Layers },
  { path: '/print', label: 'Katalog varag‘i', icon: Printer },
  { path: '/settings', label: 'Sozlamalar', icon: Settings },
];
const isPrint = computed(() => route.path === '/print');
function isActive(path: string) {
  return path === '/' ? route.path === '/' || route.path.startsWith('/products') : route.path.startsWith(path);
}
const section = computed(() => navigation.find((item) => isActive(item.path))?.label ?? 'Ocharo');
onMounted(() => store.loadSettings());
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <FeedbackHost />
    <a
      href="#main-content"
      class="no-print sr-only focus:not-sr-only focus:fixed focus:left-5 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-card focus:px-5 focus:py-3"
      >Asosiy bo‘limga o‘tish</a
    >
    <aside class="no-print fixed inset-y-0 left-0 z-20 hidden w-60 flex-col border-r border-border bg-card lg:flex">
      <div class="px-7 pb-9 pt-9">
        <p class="font-editorial text-4xl tracking-tight">Ocharo<span class="text-primary">.</span></p>
        <p class="mt-2 text-[10px] uppercase tracking-[0.25em] text-muted-foreground">Katalog tahriri</p>
      </div>
      <nav aria-label="Asosiy bo‘limlar" class="space-y-1 px-4">
        <RouterLink
          v-for="item in navigation"
          :key="item.path"
          :to="item.path"
          :class="[
            'flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left text-sm transition-colors',
            isActive(item.path)
              ? 'bg-primary-soft font-semibold text-primary'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground',
          ]"
          :aria-current="isActive(item.path) ? 'page' : undefined"
        >
          <Icon :icon="item.icon" :size="18" />{{ item.label }}
        </RouterLink>
      </nav>
      <nav
        aria-label="Ocharo platformasi"
        class="mx-5 mb-5 mt-auto rounded-xl border border-border bg-background p-3 text-[11px]"
      >
        <p class="mb-2 flex items-center gap-2 px-1 font-semibold text-foreground">
          <Icon :icon="BookOpen" :size="14" />Ocharo platformasi
        </p>
        <a
          v-for="link in platformLinks"
          :key="link.key"
          :href="link.url"
          :aria-current="link.key === 'catalogue' ? 'page' : undefined"
          :class="[
            'block rounded-lg px-2 py-1.5',
            link.key === 'catalogue'
              ? 'bg-primary-soft text-primary'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground',
          ]"
        >
          <span class="font-semibold">{{ link.label }}</span> · {{ link.hint }}
        </a>
      </nav>
    </aside>
    <div :class="isPrint ? 'lg:ml-60 print:ml-0' : 'lg:ml-60'">
      <header
        class="no-print sticky top-0 z-10 flex h-20 items-center justify-between gap-4 border-b border-border bg-background/95 px-5 backdrop-blur-sm sm:px-8 xl:px-10"
      >
        <div class="flex items-center gap-3">
          <span class="font-editorial text-2xl lg:hidden">Ocharo.</span>
          <span class="hidden text-xs text-muted-foreground sm:inline">Katalog</span>
          <Icon :icon="ChevronRight" :size="13" class="hidden text-muted-foreground sm:block" />
          <span class="text-xs font-semibold">{{ section }}</span>
        </div>
      </header>
      <nav
        aria-label="Mobil bo‘limlar"
        class="no-print flex gap-2 overflow-x-auto border-b border-border px-5 py-3 lg:hidden"
      >
        <RouterLink
          v-for="item in navigation"
          :key="item.path"
          :to="item.path"
          :class="[
            'flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-xs',
            isActive(item.path) ? 'bg-primary-soft text-primary' : 'text-muted-foreground',
          ]"
        >
          <Icon :icon="item.icon" :size="14" />{{ item.label }}
        </RouterLink>
      </nav>
      <main
        id="main-content"
        tabindex="-1"
        class="mx-auto max-w-[1600px] px-5 py-8 sm:px-8 xl:px-10 print:max-w-none print:p-0"
      >
        <CatalogueDeskEntry :desk-url="catalogueDeskUrl" />
        <div
          v-if="store.notice.value"
          role="status"
          class="no-print mb-6 flex items-start justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-4 text-sm text-success-soft-foreground"
        >
          <p class="leading-6">{{ store.notice.value }}</p>
          <button class="shrink-0 rounded p-1" aria-label="Xabarni yopish" @click="store.notice.value = null">
            <Icon :icon="X" :size="15" />
          </button>
        </div>
        <AppErrorBoundary>
          <RouterView v-slot="{ Component }"><component :is="Component" :key="route.fullPath" /></RouterView>
          <template #fallback="{ reset }">
            <section class="panel p-10 text-center">
              <h1 class="font-editorial text-2xl">Bu bo‘limni ochib bo‘lmadi.</h1>
              <p class="mt-4 text-sm text-muted-foreground">Ma’lumotlar o‘chirilmagan. Qayta urinib ko‘ring.</p>
              <button class="btn-primary mt-5" @click="reset">Qayta urinish</button>
            </section>
          </template>
        </AppErrorBoundary>
        <footer class="no-print mt-10 border-t border-border pt-4 text-xs text-muted-foreground"><span class="mr-2">Ilova haqida</span><BuildIdentity /></footer>
      </main>
      <RouteAnnouncer />
    </div>
  </div>
</template>
