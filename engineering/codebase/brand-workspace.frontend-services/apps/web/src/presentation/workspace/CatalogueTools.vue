<script setup lang="ts">
import { computed } from 'vue';
import type { DesignSnapshot } from '@/domain/workspace/EditorModels';
import { moveCatalogueProduct, setCataloguePrice } from '@/domain/workspace/CatalogueLayout';
import { catalogueProducts } from '@/domain/workspace/Templates';

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
  return `${product?.locales[props.snapshot.locale].name ?? id} · ${id.split('-v')[1] ?? ''}`;
}
function selectProduct(event: Event): void {
  const product = (event.target as HTMLSelectElement).value;
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
    <h3>Catalogue tools</h3>
    <template v-if="page?.productIds?.length">
      <label class="field-label">Garment on this page<select :value="selectedProduct" @change="selectProduct"><option v-for="product in page.productIds" :key="product" :value="product">{{ name(product) }}</option></select></label>
      <div class="template-tool-actions"><button type="button" :disabled="productIndex <= 0" @click="reorder(-1)">← Earlier</button><button type="button" :disabled="productIndex >= page.productIds.length - 1" @click="reorder(1)">Later →</button></div>
      <label v-for="item in prices" :key="item.id" class="field-label">{{ page?.catalogueSource ? `${item.priceLabel ?? 'Edition price'} · ${page.catalogueSource.currency ?? ''}` : item.tier === 'bulk' ? '10+ pieces · UZS each' : '1 piece · UZS' }}<input type="number" min="0" max="999999999" :step="page?.catalogueSource ? 0.01 : 1000" :value="Number(item.text?.replaceAll(' ', ''))" @change="price(item.id, $event)"></label>
      <p v-if="!page?.catalogueSource" class="template-help">Pricing for 2–9 pieces remains unspecified. Each garment moves with its specifications and prices.</p>
    </template>
    <label v-if="photos.length" class="field-label">Photo frames<select :value="selectedId ?? ''" @change="emit('update:selectedId', ($event.target as HTMLSelectElement).value)"><option value="" disabled>Select a photo</option><option v-for="photo in photos" :key="photo.id" :value="photo.id">{{ photo.label }}</option></select></label>
    <p v-if="!page?.productIds?.length" class="template-help">Select a photo to change its crop, size, or position. Drag its corner to resize.</p>
    <p v-if="page?.catalogueSource" class="template-source">Source draft revision {{ page.catalogueSource.revision }} · product revision {{ page.catalogueSource.productRevision }}. Edition prices are independent of canonical prices.</p><p v-else class="template-source">Based on Ocharo journal v22 · {{ snapshot.locale.toUpperCase() }}</p>
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
