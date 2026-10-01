<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';

/** Changes the operator interface independently of the document being edited. */
defineOptions({ name: 'UiLanguagePicker' });
const { uiLocale, setUiLocale, t } = useUiLocale();
</script>

<template>
  <label class="inline-flex items-center gap-2 text-xs">
    <span>{{ t('Interface language') }}</span>
    <select :aria-label="t('Interface language')" :value="uiLocale" class="native-select w-auto" @change="setUiLocale(($event.target as HTMLSelectElement).value)">
      <option value="uz" lang="uz">O‘zbekcha</option>
      <option value="ru" lang="ru">Русский</option>
      <option value="en" lang="en">English</option>
    </select>
  </label>
</template>
