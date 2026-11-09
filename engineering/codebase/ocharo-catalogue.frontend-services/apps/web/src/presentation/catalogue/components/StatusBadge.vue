<script setup lang="ts">
import { Badge, type BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import type { ProductStatus } from '../../../domain/catalogue/CatalogueModels';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
const { t } = useUiLocale();

/** Product lifecycle chip. */
defineOptions({ name: 'StatusBadge' });
const props = defineProps<{ status: ProductStatus }>();
/** The badge tone of each status. @internal */
const StatusTones: Readonly<Record<ProductStatus, BadgeVariant>> = {
  draft: 'warning',
  active: 'success',
  archived: 'neutral',
};
</script>

<template>
  <Badge :variant="StatusTones[props.status]" size="sm">{{
    t(props.status === 'active' ? 'Active' : props.status === 'draft' ? 'Draft' : 'Archived')
  }}</Badge>
</template>
