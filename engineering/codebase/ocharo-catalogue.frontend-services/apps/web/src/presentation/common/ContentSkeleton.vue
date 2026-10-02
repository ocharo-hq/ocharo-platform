<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/ui-vue/presentation/feedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
withDefaults(defineProps<{ label?: string; cards?: number }>(), { label: undefined, cards: 3 });
</script>
<template>
  <section aria-busy="true" class="space-y-5 py-4">
    <span role="status" class="sr-only">{{ label ?? t('Loading…') }}</span>
    <div aria-hidden="true"><SkeletonState class="h-8 w-56" /></div>
    <div class="grid gap-4 md:grid-cols-3" aria-hidden="true">
      <div v-for="index in cards" :key="index" class="space-y-3 rounded-xl border border-border p-5">
        <SkeletonState class="h-32 w-full" /><SkeletonState class="h-5 w-3/4" /><SkeletonState class="h-4 w-1/2" />
      </div>
    </div>
  </section>
</template>
