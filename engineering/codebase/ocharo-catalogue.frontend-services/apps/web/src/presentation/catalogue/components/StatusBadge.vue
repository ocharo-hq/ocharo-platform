<script setup lang="ts">
import { computed } from 'vue';
import type { ProductStatus } from '../../../domain/catalogue/CatalogueModels';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
const { t } = useUiLocale();

/** Product lifecycle chip. */
defineOptions({ name: 'StatusBadge' });
const props = defineProps<{ status: ProductStatus }>();
const tone = computed(
  () =>
    ({
      draft: 'bg-warning-soft text-warning-soft-foreground',
      active: 'bg-success-soft text-success-soft-foreground',
      archived: 'bg-muted text-muted-foreground',
    })[props.status],
);
</script>

<template>
  <span :class="['chip', tone]">{{
    t(props.status === 'active' ? 'Active' : props.status === 'draft' ? 'Draft' : 'Archived')
  }}</span>
</template>
