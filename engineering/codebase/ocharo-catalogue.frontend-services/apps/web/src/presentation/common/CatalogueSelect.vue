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
    <SelectPickerTrigger
      v-bind="{ ...$attrs, id, 'aria-label': label }"
      class="field flex min-w-0 items-center justify-between gap-2"
      ><SelectPickerValue
    /></SelectPickerTrigger>
    <SelectPickerContent
      class="z-[100] max-h-80 overflow-auto rounded-xl border border-border bg-card p-1 text-foreground shadow-xl"
    >
      <SelectPickerItem
        v-for="option in options"
        :key="option.value"
        :item-key="option.value"
        :label="option.label"
        class="rounded-lg px-3 py-2 text-sm"
      />
    </SelectPickerContent>
  </SelectPicker>
</template>
