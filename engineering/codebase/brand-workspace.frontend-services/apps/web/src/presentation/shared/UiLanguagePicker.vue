<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';
import DeskSelect from './DeskSelect.vue';

/** Changes the operator interface independently of the document being edited. */
defineOptions({ name: 'UiLanguagePicker' });
const { uiLocale, setUiLocale, t } = useUiLocale();
const languages = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
] as const;
</script>

<template>
  <div class="inline-flex items-center gap-2 text-xs">
    <span>{{ t('Interface language') }}</span>
    <div class="w-36">
      <DeskSelect :label="t('Interface language')" :model-value="uiLocale" :options="languages"
        @update:model-value="setUiLocale" />
    </div>
  </div>
</template>
