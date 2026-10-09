<script setup lang="ts" generic="T extends string | number">
import { computed, ref, useTemplateRef } from 'vue';
import {
  SelectPicker, SelectPickerTrigger, SelectPickerValue, SelectPickerContent, SelectPickerItem,
} from '@wow-two-beta/ui-vue/presentation/forms';
import { useUiLocale } from '@/application/shared/UseUiLocale';

/** Composes the SDK picker with Desk labels; the SDK owns focus, keyboard and popup behavior. */
defineOptions({ name: 'DeskSelect', inheritAttrs: false });
const props = defineProps<{
  modelValue?: T | null;
  options: ReadonlyArray<{ value: T; label: string | number; disabled?: boolean }>;
  label: string;
  disabled?: boolean;
  loading?: boolean;
  required?: boolean;
  name?: string;
  id?: string;
  placeholder?: string;
  searchable?: boolean;
}>();
const emit = defineEmits<{ 'update:modelValue': [value: T] }>();
const { t } = useUiLocale();
const trigger = useTemplateRef<{ el: HTMLButtonElement | null }>('trigger');
const invalid = ref(false);
const hasValue = computed(() => props.modelValue !== '' && props.options.some(option => option.value === props.modelValue && !option.disabled));
function focusInvalid(): void {
  invalid.value = true;
  trigger.value?.el?.focus();
}
const sdkOptions = computed(() => props.options.map(option => ({
  itemKey: option.value, value: option.value, label: option.label, isDisabled: option.disabled ?? false,
})));
function disabledByFieldset(): boolean {
  const button = trigger.value?.el;
  let fieldset = button?.closest('fieldset[disabled]');
  while (fieldset) {
    const legend = Array.from(fieldset.children).find(child => child.tagName === 'LEGEND');
    if (!legend?.contains(button ?? null)) return true;
    fieldset = fieldset.parentElement?.closest('fieldset[disabled]');
  }
  return false;
}
function select(value: T | null): void {
  if (props.disabled || props.loading || trigger.value?.el?.matches(':disabled') || disabledByFieldset() || value === null) return;
  invalid.value = false;
  if (props.options.some(option => option.value === value && !option.disabled)) emit('update:modelValue', value);
}
</script>

<template>
  <SelectPicker :model-value="modelValue ?? null" :options="sdkOptions"
    :is-disabled="disabled" :is-loading="loading" :is-invalid="invalid && !hasValue" v-bind="name ? { name } : {}"
    :loading-label="t('Loading…')" @update:model-value="select">
    <SelectPickerTrigger ref="trigger" v-bind="$attrs" :id="id" :aria-label="label" :aria-required="required || undefined"
      class="desk-select-trigger min-w-0">
      <SelectPickerValue :placeholder="placeholder ?? ''" />
    </SelectPickerTrigger>
    <SelectPickerContent variant="glass-outline" tone="neutral" radius="lg" padding="xs"
      :is-searchable="searchable" :search-placeholder="t('Search…')" :no-results-label="t('No results')"
      class="desk-select-content z-[100] max-h-80 max-w-[calc(100vw-2rem)] overflow-y-auto">
      <SelectPickerItem v-for="option in options" :key="option.value" :item-key="option.value"
        :label="option.label" :is-disabled="option.disabled ?? false" />
    </SelectPickerContent>
    <!-- The SDK submits a hidden key; this proxy preserves native required-form validation. -->
    <input v-if="required" class="sr-only" aria-hidden="true" tabindex="-1" type="text"
      :value="hasValue ? 'selected' : ''" :disabled="disabled" required
      @invalid.prevent="focusInvalid" />
  </SelectPicker>
</template>
