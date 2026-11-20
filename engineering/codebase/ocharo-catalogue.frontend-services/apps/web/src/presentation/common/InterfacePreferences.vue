<script setup lang="ts">
import { LabelText } from '@wow-two-beta/fe/presentation/display';
import { computed } from 'vue';
import { useColorMode } from '@wow-two-beta/fe/foundation/primitives';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import CatalogueSelect from './CatalogueSelect.vue';
import BuildIdentity from './BuildIdentity.vue';

defineOptions({ name: 'InterfacePreferences' });
const { t } = useUiLocale();
const colorMode = useColorMode();
/** The SDK provider owns the mode and remembers it; `system` hands it back to the device. */
const appearance = computed(() => (colorMode.followsSystem ? 'system' : colorMode.mode));
function setAppearance(value: string): void {
  if (value === 'system') colorMode.followSystem();
  else if (value === 'light' || value === 'dark') colorMode.setMode(value);
}
</script>

<template>
  <div class="interface-preferences space-y-5">
    <p class="text-sm text-muted-foreground">
      {{ t('Changes apply to this browser. Catalogue content languages stay separate.') }}
    </p>
    <div>
      <LabelText for="interface-appearance" class="mb-1.5 block">{{ t('Appearance') }}</LabelText>
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
    <div class="border-t border-border pt-4 text-xs text-muted-foreground">
      <p class="mb-2">{{ t('About') }}</p>
      <BuildIdentity />
    </div>
  </div>
</template>
