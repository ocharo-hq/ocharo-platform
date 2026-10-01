<script setup lang="ts">
import BuildIdentity from '../presentation/common/BuildIdentity.vue';
import { computed, onMounted } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import { CalendarHeart, ChevronRight, Image, LayoutDashboard, Palette, Plug, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { AppErrorBoundary, RouteAnnouncer } from '@wow-two-beta/ui-vue/router';
import { useBrand } from '../application/brand';
import { platformLinks } from './PlatformLinks';

/** Composes Ocharo Brend: the single place for logos, palette, contacts and scheduled appearances. */
defineOptions({ name: 'AppRoot' });
const brand = useBrand();
const route = useRoute();
const navigation = [
  { path: '/', label: 'Umumiy', icon: LayoutDashboard },
  { path: '/logos', label: 'Logotiplar', icon: Image },
  { path: '/identity', label: 'Identitet', icon: Palette },
  { path: '/appearances', label: 'Tadbirlar', icon: CalendarHeart },
  { path: '/connect', label: 'Ulanish', icon: Plug },
];
function isActive(path: string) {
  return path === '/' ? route.path === '/' : route.path.startsWith(path);
}
const section = computed(() => navigation.find((item) => isActive(item.path))?.label ?? 'Brend');
onMounted(() => brand.refresh());
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <a
      href="#main-content"
      class="sr-only focus:not-sr-only focus:fixed focus:left-5 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-card focus:px-5 focus:py-3"
      >Asosiy bo‘limga o‘tish</a
    >
    <aside class="fixed inset-y-0 left-0 z-20 hidden w-60 flex-col border-r border-border bg-card lg:flex">
      <div class="px-7 pb-9 pt-9">
        <img v-if="brand.kit.value" :src="brand.urls.logo('on-light')" alt="Ocharo" class="h-12 w-auto" />
        <p v-else class="font-editorial text-4xl tracking-tight">Ocharo<span class="text-primary">.</span></p>
        <p class="mt-2 text-[10px] uppercase tracking-[0.25em] text-muted-foreground">Brend markazi</p>
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
        <p class="mb-2 px-1 font-semibold text-foreground">Ocharo platformasi</p>
        <a
          v-for="link in platformLinks"
          :key="link.key"
          :href="link.url"
          :aria-current="link.key === 'brand' ? 'page' : undefined"
          :class="[
            'block rounded-lg px-2 py-1.5',
            link.key === 'brand'
              ? 'bg-primary-soft text-primary'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground',
          ]"
        >
          <span class="font-semibold">{{ link.label }}</span> · {{ link.hint }}
        </a>
      </nav>
    </aside>
    <div class="lg:ml-60">
      <header
        class="sticky top-0 z-10 flex h-20 items-center justify-between gap-4 border-b border-border bg-background/95 px-5 backdrop-blur-sm sm:px-8 xl:px-10"
      >
        <div class="flex items-center gap-3">
          <span class="font-editorial text-2xl lg:hidden">Ocharo.</span>
          <span class="hidden text-xs text-muted-foreground sm:inline">Brend</span><BuildIdentity />
          <Icon :icon="ChevronRight" :size="13" class="hidden text-muted-foreground sm:block" />
          <span class="text-xs font-semibold">{{ section }}</span>
        </div>
        <span v-if="brand.draft.value" class="text-xs text-muted-foreground">
          Nashr {{ brand.draft.value.latestRelease ?? '—' }} · {{ brand.pending.value.length }} ta nashr qilinmagan
          o‘zgarish
        </span>
      </header>
      <nav aria-label="Mobil bo‘limlar" class="flex gap-2 overflow-x-auto border-b border-border px-5 py-3 lg:hidden">
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
      <main id="main-content" tabindex="-1" class="mx-auto max-w-[1400px] px-5 py-8 sm:px-8 xl:px-10">
        <div
          v-if="brand.error.value"
          role="alert"
          class="mb-6 flex items-start justify-between gap-4 rounded-xl border border-destructive/20 bg-destructive-soft px-5 py-4 text-sm text-destructive-soft-foreground"
        >
          <p class="leading-6">{{ brand.error.value }}</p>
          <button class="shrink-0 rounded p-1" aria-label="Xabarni yopish" @click="brand.error.value = null">
            <Icon :icon="X" :size="15" />
          </button>
        </div>
        <div
          v-if="brand.notice.value"
          role="status"
          class="mb-6 flex items-start justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-4 text-sm text-success-soft-foreground"
        >
          <p class="leading-6">{{ brand.notice.value }}</p>
          <button class="shrink-0 rounded p-1" aria-label="Xabarni yopish" @click="brand.notice.value = null">
            <Icon :icon="X" :size="15" />
          </button>
        </div>
        <AppErrorBoundary>
          <RouterView v-slot="{ Component }"><component :is="Component" :key="route.fullPath" /></RouterView>
          <template #fallback="{ reset }">
            <section class="panel p-10 text-center">
              <h1 class="font-editorial text-2xl">Bu bo‘limni ochib bo‘lmadi.</h1>
              <button class="btn-primary mt-5" @click="reset">Qayta urinish</button>
            </section>
          </template>
        </AppErrorBoundary>
      </main>
      <RouteAnnouncer />
    </div>
  </div>
</template>
