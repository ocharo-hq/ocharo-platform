<script setup lang="ts">
import {
  SelectPicker,
  SelectPickerTrigger,
  SelectPickerValue,
  SelectPickerContent,
  SelectPickerItem,
} from '@wow-two-beta/fe/presentation/forms';
defineOptions({ inheritAttrs: false });
defineProps<{
  modelValue: string;
  options: ReadonlyArray<{ value: string; label: string }>;
  label: string;
  disabled?: boolean;
  id?: string;
}>();
const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
</script>
<template>
  <SelectPicker
    :model-value="modelValue"
    :is-disabled="disabled"
    @update:model-value="
      (value) => {
        if (typeof value === 'string') emit('update:modelValue', value);
      }
    "
  >
    <SelectPickerTrigger v-bind="{ ...$attrs, id, 'aria-label': label }" class="w-full min-w-0"
      ><SelectPickerValue
    /></SelectPickerTrigger>
    <SelectPickerContent variant="glass-outline" class="z-[100] max-h-80 overflow-auto">
      <SelectPickerItem v-for="option in options" :key="option.value" :item-key="option.value" :label="option.label" />
    </SelectPickerContent>
  </SelectPicker>
</template>
