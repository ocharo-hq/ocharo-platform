<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
withDefaults(defineProps<{ label?: string; cards?: number; variant?: 'gallery' | 'rows' | 'form' | 'imports' | 'stats' | 'canvas' | 'designer' }>(), { label: 'Loading content', cards: 3, variant: 'gallery' });
const { t } = useUiLocale();
</script>
<template>
  <section aria-busy="true" class="query-region space-y-5 py-4" :class="'skeleton-' + variant">
    <span role="status" class="sr-only">{{ t(label) }}</span>
    <div v-if="variant === 'rows'" class="space-y-3" aria-hidden="true"><div v-for="row in cards" :key="row" class="flex items-center justify-between gap-4 py-4"><div class="w-2/3 space-y-2"><SkeletonState class="h-5 w-3/4" /><SkeletonState class="h-4 w-1/2" /></div><SkeletonState class="h-9 w-20" /></div></div>
    <div v-else-if="variant === 'form' || variant === 'designer'" class="skeleton-editor-grid" aria-hidden="true"><aside class="space-y-4"><SkeletonState class="h-10 w-full" /><SkeletonState v-for="row in 4" :key="row" class="h-16 w-full" /></aside><div class="workspace-card space-y-5 p-6"><SkeletonState class="h-7 w-48" /><div v-if="variant === 'form'" class="grid gap-5 sm:grid-cols-2"><div v-for="field in 8" :key="field" class="space-y-2"><SkeletonState class="h-4 w-24" /><SkeletonState class="h-10 w-full" /></div></div><SkeletonState v-else class="h-[600px] w-full" /></div></div>
    <div v-else-if="variant === 'stats'" class="grid gap-4 sm:grid-cols-3" aria-hidden="true"><div v-for="stat in cards" :key="stat" class="space-y-3"><SkeletonState class="h-4 w-28" /><SkeletonState class="h-9 w-20" /></div></div>
    <div v-else-if="variant === 'canvas'" aria-hidden="true"><SkeletonState class="mx-auto aspect-[210/297] w-full max-w-sm" /></div>
    <div v-else class="skeleton-card-grid" :class="{ 'skeleton-import-grid': variant === 'imports' }" aria-hidden="true"><div v-for="index in cards" :key="index" class="overflow-hidden rounded-xl border border-border bg-card"><SkeletonState :class="variant === 'imports' ? 'h-48 w-full' : 'h-[230px] w-full'" /><div class="space-y-4 p-5"><SkeletonState class="h-6 w-3/4" /><SkeletonState class="h-4 w-1/2" /><SkeletonState class="h-4 w-2/3" /><div class="flex gap-3"><SkeletonState class="h-9 w-24" /><SkeletonState class="h-9 w-24" /></div></div></div></div>
  </section>
</template>
<style scoped>
.skeleton-card-grid { display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:24px; }
.skeleton-import-grid { grid-template-columns:repeat(2,minmax(0,1fr)); }
.skeleton-editor-grid { display:grid;grid-template-columns:260px minmax(0,1fr);gap:24px; }
.skeleton-designer .skeleton-editor-grid { grid-template-columns:270px minmax(0,1fr);gap:22px; }
@media(max-width:1023px){.skeleton-form .skeleton-editor-grid{grid-template-columns:1fr;}}
@media(max-width:900px){.skeleton-designer .skeleton-editor-grid{grid-template-columns:1fr;}}
@media(max-width:639px){.skeleton-import-grid{grid-template-columns:1fr;}}
</style>
