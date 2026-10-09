<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed } from 'vue';
import type { DesignSnapshot } from '@/domain/workspace/EditorModels';
import { moveCatalogueProduct, setCataloguePrice } from '@/domain/workspace/CatalogueLayout';
import { catalogueProducts } from '@/domain/workspace/Templates';

const { t } = useUiLocale();

defineOptions({ name: 'CatalogueTools' });
const props = defineProps<{ snapshot: DesignSnapshot; pageId: string; selectedId: string | null }>();
const emit = defineEmits<{ 'update:snapshot': [snapshot: DesignSnapshot]; 'update:pageId': [id: string]; 'update:selectedId': [id: string | null] }>();
const page = computed(() => props.snapshot.pages.find((item) => item.id === props.pageId));
const selectedProduct = computed(() => page.value?.elements.find((element) => element.id === props.selectedId)?.productId ?? page.value?.productIds?.[0]);
const productIndex = computed(() => page.value?.productIds?.indexOf(selectedProduct.value ?? '') ?? -1);
const prices = computed(() => page.value?.elements.filter((element) => element.productId === selectedProduct.value && element.role === 'price') ?? []);
const photos = computed(() => page.value?.elements.filter((element) => element.role === 'photo') ?? []);
function name(id: string): string {
  if (page.value?.catalogueSource?.productId === id) return page.value.elements.find(item => item.role === 'title')?.text || id;
  const product = catalogueProducts.find((item) => item.id === id);
  return `${product?.locales[props.snapshot.locale as 'en'|'ru'|'uz']?.name ?? id} · ${id.split('-v')[1] ?? ''}`;
}
function selectProduct(product: string): void {
  emit('update:selectedId', page.value?.elements.find((element) => element.productId === product && element.role === 'photo')?.id ?? null);
}
function price(id: string, event: Event): void {
  const amount = Number((event.target as HTMLInputElement).value);
  if (!Number.isFinite(amount) || amount < 0 || amount > 999999999) return;
  emit('update:snapshot', setCataloguePrice(props.snapshot, props.pageId, id, amount));
}
function reorder(direction: -1|1): void {
  if (selectedProduct.value) emit('update:snapshot', moveCatalogueProduct(props.snapshot, props.pageId, selectedProduct.value, direction));
}
</script>

<template>
  <section class="template-tools">
    <h3>{{ t("Catalogue tools") }}</h3>
    <template v-if="page?.productIds?.length">
      <label class="field-label">{{ t("Garment on this page") }}<DeskSelect :model-value="selectedProduct ?? null" @update:model-value="selectProduct" :label="t('Garment on this page')" :options="[...(page.productIds).map(product => ({ value: product, label: (name(product)) }))]" /></label>
      <div class="template-tool-actions"><button type="button" :disabled="productIndex <= 0" @click="reorder(-1)">{{ t("← Earlier") }}</button><button type="button" :disabled="productIndex >= page.productIds.length - 1" @click="reorder(1)">{{ t("Later →") }}</button></div>
      <label v-for="item in prices.filter(price=>!price.binding)" :key="item.id" class="field-label">{{ page?.catalogueSource ? `${item.priceLabel ?? t("Edition price")} · ${page.catalogueSource.currency ?? ''}` : item.tier === 'bulk' ? t('10+ pieces · UZS each') : t('1 piece · UZS') }}<input type="number" min="0" max="999999999" :step="page?.catalogueSource ? 0.01 : 1000" :value="Number(item.text?.replaceAll(' ', ''))" @change="price(item.id, $event)"></label>
      <p v-if="!page?.catalogueSource" class="template-help">{{ t("Pricing for 2–9 pieces remains unspecified. Each garment moves with its specifications and prices.") }}</p>
    </template>
    <label v-if="photos.length" class="field-label">{{ t("Photo frames") }}<DeskSelect :model-value="selectedId ?? ''" @update:model-value="emit('update:selectedId', $event)" :label="t('Photo frames')" :options="[({ value: '' as const, label: (t('Select a photo')), disabled: true }), ...(photos).map(photo => ({ value: photo.id, label: (photo.label) }))]" /></label>
    <p v-if="!page?.productIds?.length" class="template-help">{{ t("Select a photo to change its crop, size, or position. Drag its corner to resize.") }}</p>
    <p v-if="page?.catalogueSource" class="template-source">{{ t("Source draft revision") }} {{ page.catalogueSource.revision }} {{ t("· product revision") }} {{ page.catalogueSource.productRevision }}{{ t(". Connected values update when you refresh saved product values.") }}</p><p v-else class="template-source">{{ t("Based on Ocharo journal v22 ·") }} {{ snapshot.locale.toUpperCase() }}</p>
  </section>
</template>

<style scoped>
.template-tools { border-top: 1px solid #ded8cb; padding-top: 20px; margin-top: 20px; display: grid; gap: 12px; }
h3 { font-size: 14px; margin: 0; }
.template-tool-actions { display: flex; gap: 8px; }
.template-tool-actions button { border: 1px solid #d7d1c4; background: #fbf9f3; border-radius: 6px; padding: 7px 10px; font-size: 12px; cursor: pointer; }
.template-tool-actions button:disabled { opacity: .4; cursor: default; }
.template-help, .template-source { font-size: 11px; line-height: 1.6; color: #7b7569; margin: 0; }
.template-source { font-size: 10px; }
</style>
