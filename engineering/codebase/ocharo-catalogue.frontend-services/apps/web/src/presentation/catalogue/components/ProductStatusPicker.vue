<script setup lang="ts">
import { Archive, CircleCheck, FilePenLine } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { SegmentedPicker, ToggleInput } from '@wow-two-beta/ui-vue/presentation/forms';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
import type { ProductStatus } from '../../../domain/catalogue/CatalogueModels';

/** Selects one product status with labelled icons and a visible active label. */
defineOptions({ name: 'ProductStatusPicker' });
const model = defineModel<ProductStatus>({ required: true });
const options = [
  { value: 'draft', label: 'Draft', icon: FilePenLine },
  { value: 'active', label: 'Active', icon: CircleCheck },
  { value: 'archived', label: 'Archived', icon: Archive },
] as const;
const { t } = useUiLocale();

/** Keeps the required status selected when its active segment is pressed again. */
function select(value: string | readonly string[] | null) {
  if (value === 'draft' || value === 'active' || value === 'archived') model.value = value;
}
</script>

<template>
  <SegmentedPicker :model-value="model" :aria-label="t('Status')" @update:model-value="select">
    <ToggleInput
      v-for="option in options"
      :key="option.value"
      :value="option.value"
      :title="t(option.label)"
      :aria-label="t(option.label)"
      class="min-h-10 min-w-10 gap-2"
    >
      <Icon :icon="option.icon" :size="16" />
      <span v-if="model === option.value">{{ t(option.label) }}</span>
    </ToggleInput>
  </SegmentedPicker>
</template>
