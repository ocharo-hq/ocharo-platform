<script setup lang="ts">
import { computed } from 'vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { CatalogueDocumentControls } from '../../application/design/UseCatalogueDocument';
import {
  catalogueCopy,
  catalogueSettings,
  copyFields,
  mediaGroups,
  randomizeImages,
  refreshCatalogue,
} from '../../domain/design/CatalogueDesign';
import type { CatalogueCopy, CatalogueMediaGroup, CatalogueSettings } from '../../domain/design/EditorModels';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { CheckboxField, Field, TextAreaInput } from '@wow-two-beta/fe/presentation/forms';
import CatalogueSelect from '../common/CatalogueSelect.vue';
defineOptions({ name: 'CatalogueSharedSettings' });
const props = defineProps<{ editor: CatalogueDocumentControls }>(),
  { t } = useUiLocale();
const copy = computed(() => (props.editor.snapshot.value ? catalogueCopy(props.editor.snapshot.value) : null)),
  settings = computed(() => catalogueSettings(props.editor.snapshot.value ?? undefined));
const groups = computed(() =>
  mediaGroups.map((value) => ({
    value,
    label: t(
      value === 'all'
        ? 'All active images'
        : value === 'model'
          ? 'Model images'
          : value === 'product'
            ? 'Product images'
            : value === 'detail'
              ? 'Detail images'
              : 'Fabric images',
    ),
  })),
);
const labels: Record<keyof CatalogueCopy, string> = {
  collection: 'Collection title',
  coverNote: 'Cover note',
  header: 'Header',
  footer: 'Footer',
  sizes: 'Sizes label',
  pieces: 'Quantity label',
  priceOnRequest: 'Price on request',
  prices: 'Price heading',
  materials: 'Fabric label',
  decoration: 'Decoration label',
};
const prices: { key: keyof CatalogueSettings['priceDisplay']; label: string }[] = [
  { key: 'show', label: 'Show prices' },
  { key: 'showCurrency', label: 'Show currency' },
  { key: 'showQuantity', label: 'Show quantity' },
  { key: 'showRetail', label: 'Show retail price' },
  { key: 'showWholesale', label: 'Show wholesale price' },
];
function setCopy(field: keyof CatalogueCopy, value: string) {
  props.editor.change((next) => {
    next.catalogueSettings = catalogueSettings(next);
    next.catalogueSettings.copyByLocale[next.locale] = { ...catalogueCopy(next), [field]: value };
    const updated = refreshCatalogue(next, props.editor.source.value?.products ?? []);
    next.pages = updated.pages;
  });
}
function price(key: keyof CatalogueSettings['priceDisplay'], value: boolean) {
  props.editor.change((next) => {
    next.catalogueSettings = catalogueSettings(next);
    next.catalogueSettings.priceDisplay[key] = value;
    next.pages = refreshCatalogue(next, props.editor.source.value?.products ?? []).pages;
  });
}
function group(value: string) {
  props.editor.change((next) => {
    next.catalogueSettings = catalogueSettings(next);
    next.catalogueSettings.mediaGroup = value as CatalogueMediaGroup;
    next.pages = randomizeImages(next, props.editor.source.value?.products ?? [], undefined, () => 0.5).pages;
  });
}
</script>
<template>
  <Card radius="2xl" padding="xl" class="shared-settings"
    ><details>
      <summary>{{ t('Catalogue copy and price display') }}</summary>
      <p>
        {{
          t(
            'These labels belong to the catalogue language. Product names, materials and prices are edited in Products.',
          )
        }}
      </p>
      <Field :label="t('Image group for all products')">
        <CatalogueSelect
          :model-value="settings.mediaGroup"
          :options="groups"
          :label="t('Image group for all products')"
          :disabled="editor.busy.value"
          @update:model-value="group"
        />
      </Field>
      <div class="price-options">
        <CheckboxField
          v-for="flag in prices"
          :key="flag.key"
          :model-value="settings.priceDisplay[flag.key]"
          :is-disabled="editor.busy.value"
          :label="t(flag.label)"
          @update:model-value="(checked) => price(flag.key, checked)"
        />
      </div>
      <div class="copy-fields">
        <Field v-for="field in copyFields" :key="field" :label="t(labels[field])">
          <TextAreaInput
            :model-value="copy?.[field] ?? ''"
            :rows="field === 'coverNote' || field === 'footer' ? 2 : 1"
            maxlength="1000"
            :is-disabled="editor.busy.value"
            @change="setCopy(field, ($event.target as HTMLTextAreaElement).value)"
          />
        </Field>
      </div></details
  ></Card>
</template>
<style scoped>
.shared-settings {
  margin: 16px 0;
}
.shared-settings summary {
  cursor: pointer;
  font-weight: 600;
}
.shared-settings p {
  font-size: 13px;
  line-height: 1.6;
  margin: 12px 0;
  color: var(--color-muted-foreground);
}
.copy-fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 14px;
  margin-top: 16px;
}
.price-options {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin: 16px 0;
}
</style>
