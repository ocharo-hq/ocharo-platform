<script setup lang="ts">
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { useAppearance } from '../../application/shared/UseAppearance';
import CatalogueSelect from './CatalogueSelect.vue';
import BuildIdentity from './BuildIdentity.vue';

defineOptions({ name: 'InterfacePreferences' });
const { t, uiLocale, setUiLocale } = useUiLocale();
const { appearance, setAppearance } = useAppearance();
</script>

<template>
  <div class="interface-preferences space-y-5">
    <p class="text-sm text-muted-foreground">
      {{ t('Changes apply to this browser. Catalogue content languages stay separate.') }}
    </p>
    <div class="grid gap-5 sm:grid-cols-2">
      <div>
        <label class="label" for="interface-language">{{ t('Interface language') }}</label>
        <CatalogueSelect
          id="interface-language"
          :model-value="uiLocale"
          :label="t('Interface language')"
          :options="[
            { value: 'uz', label: 'O‘zbekcha' },
            { value: 'ru', label: 'Русский' },
            { value: 'en', label: 'English' },
          ]"
          @update:model-value="setUiLocale"
        />
      </div>
      <div>
        <label class="label" for="interface-appearance">{{ t('Appearance') }}</label>
        <CatalogueSelect
          id="interface-appearance"
          :model-value="appearance"
          :label="t('Appearance')"
          :options="[
            { value: 'system', label: t('System') },
            { value: 'light', label: t('Light') },
            { value: 'dark', label: t('Dark') },
          ]"
          @update:model-value="setAppearance"
        />
      </div>
    </div>
    <div class="border-t border-border pt-4 text-xs text-muted-foreground">
      <p class="mb-2">{{ t('About') }}</p>
      <BuildIdentity />
    </div>
  </div>
</template>
