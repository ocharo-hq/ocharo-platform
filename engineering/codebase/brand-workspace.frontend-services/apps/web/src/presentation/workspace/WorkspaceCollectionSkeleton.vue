<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/ui-vue/presentation/feedback';

/** Matches the Desk collection being loaded without moving its toolbar or inventing content. */
defineOptions({ name: 'WorkspaceCollectionSkeleton' });
defineProps<{ shape: 'events' | 'deadlines' | 'suppliers' | 'media'; label: string }>();
</script>

<template>
  <div aria-busy="true">
    <span role="status" class="sr-only">{{ label }}</span>
    <div v-if="shape === 'events'" class="space-y-3" aria-hidden="true">
      <SkeletonState class="h-4 w-32 motion-reduce:animate-none" />
      <div class="events-list">
        <div v-for="index in 3" :key="index" class="event-card min-h-64">
          <SkeletonState class="h-5 w-4/5 motion-reduce:animate-none" />
          <SkeletonState class="h-4 w-3/5 motion-reduce:animate-none" />
          <SkeletonState class="h-4 w-1/2 motion-reduce:animate-none" />
          <div class="flex gap-2"><SkeletonState v-for="badge in 3" :key="badge" class="h-6 w-20 motion-reduce:animate-none" /></div>
          <SkeletonState class="h-4 w-4/5 motion-reduce:animate-none" />
          <SkeletonState class="h-4 w-2/3 motion-reduce:animate-none" />
          <div class="flex gap-2 border-t border-border pt-3"><SkeletonState v-for="action in 2" :key="action" class="h-8 w-20 motion-reduce:animate-none" /></div>
        </div>
      </div>
    </div>
    <div v-else-if="shape === 'deadlines'" class="events-deadlines" aria-hidden="true">
      <div v-for="index in 3" :key="index" class="events-deadline min-h-28">
        <SkeletonState class="h-4 w-4/5 motion-reduce:animate-none" />
        <SkeletonState class="h-4 w-3/5 motion-reduce:animate-none" />
        <SkeletonState class="h-5 w-2/3 motion-reduce:animate-none" />
      </div>
    </div>
    <div v-else-if="shape === 'suppliers'" class="space-y-1" aria-hidden="true">
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
