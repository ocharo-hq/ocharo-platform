<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';

/** Matches the Desk collection being loaded without moving its toolbar or inventing content. */
defineOptions({ name: 'WorkspaceCollectionSkeleton' });
defineProps<{ shape: 'suppliers' | 'media'; label: string }>();
</script>

<template>
  <div aria-busy="true">
    <span role="status" class="sr-only">{{ label }}</span>
    <div v-if="shape === 'suppliers'" class="space-y-1" aria-hidden="true">
      <div v-for="index in 4" :key="index" class="sourcing-row">
        <SkeletonState class="h-11 w-11 rounded-xl motion-reduce:animate-none" />
        <div class="space-y-2"><SkeletonState class="h-4 w-4/5 motion-reduce:animate-none" /><SkeletonState class="h-3 w-3/5 motion-reduce:animate-none" /></div>
      </div>
    </div>
    <div v-else class="sourcing-gallery" aria-hidden="true">
      <div v-for="index in 6" :key="index" class="space-y-2">
        <SkeletonState class="aspect-square w-full rounded-xl motion-reduce:animate-none" />
        <SkeletonState class="h-3 w-3/4 motion-reduce:animate-none" />
      </div>
    </div>
  </div>
</template>
