<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/ui-vue/presentation/feedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
withDefaults(defineProps<{ label?: string; cards?: number }>(), { label: 'Loading content', cards: 3 });
const { t } = useUiLocale();
</script>
<template>
  <section aria-busy="true" class="space-y-5 py-4">
    <span role="status" class="sr-only">{{ t(label) }}</span>
    <SkeletonState class="h-8 w-56" aria-hidden="true" />
    <div class="grid gap-4 md:grid-cols-3" aria-hidden="true">
      <div v-for="index in cards" :key="index" class="space-y-3 rounded-xl border border-border p-5">
        <SkeletonState class="h-32 w-full" /><SkeletonState class="h-5 w-3/4" /><SkeletonState class="h-4 w-1/2" />
      </div>
    </div>
  </section>
</template>
