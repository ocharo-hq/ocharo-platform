<script setup lang="ts">
import { SkeletonState, SkeletonStateGroup } from '@wow-two-beta/ui-vue/presentation/feedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
withDefaults(
  defineProps<{
    label?: string;
    cards?: number;
    variant?: 'generic' | 'product' | 'collection' | 'catalogue' | 'designer-web' | 'designer-pdf' | 'product-form';
  }>(),
  { label: undefined, cards: 3, variant: 'generic' },
);
</script>
<template>
  <SkeletonStateGroup :is-loading="true" :label="label ?? t('Loading…')" class="catalogue-content-skeleton">
    <div v-if="variant === 'product-form'" class="catalogue-skeleton-product-form" aria-hidden="true">
      <SkeletonState class="h-5 w-28" /><SkeletonState class="mt-6 h-10 w-64" />
      <div class="catalogue-skeleton-review panel mt-5 flex flex-wrap gap-3 p-4">
        <SkeletonState v-for="index in 3" :key="index" class="h-9 w-28" />
      </div>
      <div class="catalogue-skeleton-form-grid mt-6">
        <div class="space-y-5">
          <div v-for="section in 3" :key="section" class="panel p-6">
            <SkeletonState class="h-7 w-32" />
            <div class="catalogue-skeleton-fields mt-4">
              <div v-for="field in 6" :key="field" class="space-y-2">
                <SkeletonState class="h-3 w-24" /><SkeletonState class="h-11 w-full" />
              </div>
            </div>
          </div>
        </div>
        <aside class="panel self-start p-6">
          <div class="flex items-center justify-between gap-3">
            <SkeletonState class="h-7 w-24" /><SkeletonState class="h-11 w-28" />
          </div>
          <div class="mt-4 grid grid-cols-2 gap-3">
            <SkeletonState v-for="index in 2" :key="index" class="aspect-[3/4] w-full" />
          </div>
        </aside>
      </div>
    </div>
    <div
      v-else-if="variant === 'designer-web' || variant === 'designer-pdf'"
      class="catalogue-skeleton-designer"
      aria-hidden="true"
    >
      <div class="catalogue-skeleton-designer-toolbar">
        <SkeletonState class="h-11 w-36" />
        <div class="min-w-[190px] flex-1 space-y-2">
          <SkeletonState class="h-3 w-24" /><SkeletonState class="h-11 w-full" />
        </div>
        <div class="w-48 space-y-2"><SkeletonState class="h-3 w-32" /><SkeletonState class="h-11 w-full" /></div>
        <SkeletonState class="h-11 w-64" />
      </div>
      <SkeletonState class="h-5 w-48" />
      <SkeletonState class="my-4 h-14 w-full" /><SkeletonState class="my-4 h-14 w-full" />
      <div v-if="variant === 'designer-pdf'" class="mb-[18px] flex flex-wrap gap-2.5">
        <SkeletonState v-for="index in 4" :key="index" class="h-11 w-40" />
      </div>
      <div :class="`catalogue-skeleton-${variant}-grid`">
        <aside class="catalogue-skeleton-page-rail panel">
          <SkeletonState class="catalogue-skeleton-rail-heading h-7 w-28" />
          <div class="catalogue-skeleton-rail-list space-y-2">
            <SkeletonState v-for="index in 3" :key="index" class="h-11 w-full" />
          </div>
          <div v-for="index in 3" :key="index" class="space-y-2">
            <SkeletonState class="h-3 w-24" /><SkeletonState class="h-11 w-full" />
          </div>
        </aside>
        <div class="catalogue-skeleton-preview-panel">
          <div class="mb-3 flex flex-wrap gap-3">
            <SkeletonState class="h-11 w-40" /><SkeletonState class="h-11 w-40" />
          </div>
          <div
            class="catalogue-skeleton-preview"
            :class="variant === 'designer-pdf' ? 'catalogue-skeleton-pdf-canvas' : 'catalogue-skeleton-web-preview'"
          >
            <div v-if="variant === 'designer-pdf'" class="catalogue-skeleton-paper">
              <SkeletonState class="h-full w-full" />
            </div>
            <div v-else class="min-h-[600px] space-y-6">
              <SkeletonState class="h-48 w-full" />
              <div class="grid grid-cols-2 gap-4">
                <SkeletonState v-for="index in 2" :key="index" class="aspect-[3/4] w-full" />
              </div>
            </div>
          </div>
        </div>
        <aside v-if="variant === 'designer-pdf'" class="catalogue-skeleton-inspector panel">
          <SkeletonState class="h-7 w-32" />
          <div v-for="index in 6" :key="index" class="space-y-2">
            <SkeletonState class="h-3 w-24" /><SkeletonState class="h-11 w-full" />
          </div>
        </aside>
      </div>
    </div>
    <div v-else class="catalogue-skeleton-grid" :class="`catalogue-skeleton-${variant}`" aria-hidden="true">
      <div v-for="index in cards" :key="index" class="catalogue-skeleton-card">
        <template v-if="variant === 'collection'">
          <SkeletonState class="h-6 w-2/3" /><SkeletonState class="mt-2 h-3 w-3/4" />
        </template>
        <template v-else>
          <SkeletonState
            :class="
              variant === 'product'
                ? 'aspect-[3/4] w-full'
                : variant === 'catalogue'
                  ? 'h-[230px] w-full'
                  : 'h-32 w-full'
            "
          />
          <div class="space-y-3 p-4">
            <SkeletonState class="h-6 w-3/4" /><SkeletonState class="h-4 w-1/2" /><SkeletonState class="h-4 w-2/3" />
          </div>
        </template>
      </div>
    </div>
  </SkeletonStateGroup>
</template>
