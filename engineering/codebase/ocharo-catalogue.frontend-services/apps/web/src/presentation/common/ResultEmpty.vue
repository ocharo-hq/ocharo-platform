<script setup lang="ts">
import { EmptyState } from '@wow-two-beta/ui-vue/presentation/display';
defineProps<{ label: string }>();
</script>
<template>
  <EmptyState :title="label" class="catalogue-empty-state">
    <template #title
      ><span class="text-sm font-normal text-muted-foreground">{{ label }}</span></template
    >
  </EmptyState>
</template>
