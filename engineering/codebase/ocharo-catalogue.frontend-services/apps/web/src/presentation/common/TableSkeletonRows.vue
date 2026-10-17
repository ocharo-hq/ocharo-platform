<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
withDefaults(defineProps<{ columns: number; rows?: number }>(), { rows: 5 });
</script>
<template>
  <tr v-for="row in rows" :key="row" class="catalogue-skeleton-row border-t border-border" aria-hidden="true">
    <td v-for="column in columns" :key="column" class="px-4 py-3">
      <div v-if="column === 1" class="flex items-center gap-3">
        <SkeletonState class="h-12 w-9 shrink-0" />
        <div class="space-y-2"><SkeletonState class="h-4 w-28" /><SkeletonState class="h-3 w-20" /></div>
      </div>
      <SkeletonState v-else class="h-8 w-full min-w-12" />
    </td>
  </tr>
</template>
