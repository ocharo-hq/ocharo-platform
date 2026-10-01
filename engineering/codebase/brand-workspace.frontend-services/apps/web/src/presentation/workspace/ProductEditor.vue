<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  catalogueLocales, hasProductTranslation, productCopy, updateProductCopy,
  type CatalogueLocale, type Copy, type Product, type ProductMediaCategory,
} from '@/domain/workspace/Commerce';

const { t } = useUiLocale();

const props = defineProps<{ product: Product; canApprove: boolean; imageUrl: (sha: string) => string }>();
/** Commerce products are JSON documents; serialization also unwraps nested Vue proxies. */
function copyProduct(value: Product): Product { return JSON.parse(JSON.stringify(value)) as Product; }
const emit = defineEmits<{ 'update:product': [value: Product] }>();
// Each edit belongs to the parent's unsaved snapshot; never mutate an incoming prop.
const product = ref<Product>(copyProduct(props.product));
watch(() => props.product, value => {
  if (JSON.stringify(value) !== JSON.stringify(product.value)) product.value = copyProduct(value);
}, { deep: true });
watch(product, value => {
  if (JSON.stringify(value) !== JSON.stringify(props.product)) emit('update:product', copyProduct(value));
}, { deep: true });
const locale = ref<CatalogueLocale>('en');
const copy = computed(() => productCopy(product.value, locale.value));
const direction = computed(() => catalogueLocales.find(item => item.code === locale.value)?.dir ?? 'ltr');
const categories: { value: ProductMediaCategory; label: string }[] = [
  { value: 'model', label: 'Model photos' }, { value: 'product', label: 'Product views' },
  { value: 'detail', label: 'Details' }, { value: 'fabric', label: 'Fabric photos' },
];
function editCopy(field: keyof Copy, value: string | null) {
  updateProductCopy(product.value, locale.value, { ...copy.value, status: field === 'status' ? value as Copy['status'] : 'draft', [field]: value });
}
function text(event: Event) { return (event.target as HTMLInputElement).value; }
function labels(event: Event) { return [...new Set(text(event).split(',').map(value => value.trim()).filter(Boolean))]; }
function addPrice() {
  product.value.document.pricing = { currency: 'UZS', status: 'draft', tiers: [{ minimumQuantity: 1, maximumQuantity: null, unitPrice: 0 }], wording: null };
}
function addTier() {
  const pricing = product.value.document.pricing;
  if (!pricing) return;
  const last = pricing.tiers.at(-1);
  const minimum = last ? (last.maximumQuantity ?? last.minimumQuantity) + 1 : 1;
  if (last && last.maximumQuantity === null) last.maximumQuantity = minimum - 1;
  pricing.tiers.push({ minimumQuantity: minimum, maximumQuantity: null, unitPrice: 0 });
  pricing.status = 'draft';
}
function addGroup() {
  (product.value.mediaGroups ??= []).push({ id: `group-${crypto.randomUUID().slice(0, 8)}`, label: t('New image group'), category: 'model', mediaIds: [] });
}
function detach(id: string) {
  product.value.document.media = product.value.document.media.filter(image => image.id !== id);
  for (const group of product.value.mediaGroups ?? []) group.mediaIds = group.mediaIds.filter(value => value !== id);
}
function roleChanged(id: string) {
  if (product.value.document.media.find(image => image.id === id)?.role === 'reference')
    for (const group of product.value.mediaGroups ?? []) group.mediaIds = group.mediaIds.filter(value => value !== id);
}
</script>

<template>
  <div class="space-y-6">
    <div class="grid gap-4 sm:grid-cols-3">
      <label>{{ t('Category') }}<input v-model="product.document.garmentType" class="native-input mt-2" /></label>
      <label>{{ t('Variant code') }}<input v-model="product.document.variantCode" class="native-input mt-2" /></label>
      <label>{{ t('Status') }}<select v-model="product.document.status" class="native-select mt-2"><option value="draft">{{t('Draft')}}</option><option value="active">{{t('Active')}}</option><option value="archived">{{t('Archived')}}</option></select></label>
    </div>
    <section class="space-y-4" aria-labelledby="product-copy-title">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div><h3 id="product-copy-title" class="font-semibold">{{ t('Product translations') }}</h3><p class="text-sm text-muted-foreground">{{ t('Product wording appears in every catalogue using this product.') }}</p></div>
        <label>{{ t('Content language') }}<select v-model="locale" class="native-select mt-2" :aria-label="t('Product content language')"><option v-for="language in catalogueLocales" :key="language.code" :value="language.code">{{ language.name }} · {{ language.code }}</option></select></label>
      </div>
      <p v-if="!hasProductTranslation(product,locale)" class="rounded-lg bg-muted p-3 text-sm">{{ t('This translation is empty. Add wording before approving it.') }}</p>
      <label class="block">{{ t('Product name') }}<input :value="copy.name??''" :dir="direction" maxlength="300" class="native-input mt-2" @input="editCopy('name',text($event))" /></label>
      <div class="grid gap-4 sm:grid-cols-2">
        <label>{{ t('Material / fabric name') }}<input :value="copy.fabric??''" :dir="direction" maxlength="1000" class="native-input mt-2" @input="editCopy('fabric',text($event))" /></label>
        <label>{{ t('Decoration') }}<input :value="copy.decoration??''" :dir="direction" maxlength="1000" class="native-input mt-2" @input="editCopy('decoration',text($event))" /></label>
      </div>
      <label class="block">{{ t('Description') }}<textarea :value="copy.description??''" :dir="direction" rows="4" maxlength="10000" class="native-input mt-2" @input="editCopy('description',text($event))" /></label>
      <label v-if="canApprove" class="flex gap-2"><input type="checkbox" :checked="copy.status==='approved'" :disabled="!copy.name?.trim()" @change="editCopy('status',($event.target as HTMLInputElement).checked?'approved':'draft')"/>{{ t('Approve this language for publication') }}</label>
    </section>
    <div class="grid gap-4 sm:grid-cols-2">
      <label>{{ t('Material codes, separated by commas') }}<input :value="product.document.fabricCodes.join(', ')" class="native-input mt-2" @change="product.document.fabricCodes=labels($event)" /></label>
      <label>{{ t('Sizes, separated by commas') }}<input :value="product.document.sizes.join(', ')" class="native-input mt-2" placeholder="S, M, L, 42–44" @change="product.document.sizes=labels($event)" /></label>
    </div>
    <label class="flex gap-2"><input v-model="product.document.madeToOrder" type="checkbox"/>{{ t('Made to order') }}</label>
    <section class="space-y-4 rounded-lg border border-border p-4" aria-labelledby="product-prices-title">
      <div class="flex justify-between gap-3"><div><h3 id="product-prices-title" class="font-semibold">{{ t('Product prices') }}</h3><p class="text-sm text-muted-foreground">{{ t('Edit amounts here. The catalogue controls which price rows appear.') }}</p></div><Button v-if="!product.document.pricing" size="sm" variant="outline" @click="addPrice">{{ t('Add price') }}</Button></div>
      <template v-if="product.document.pricing">
        <label class="block max-w-xs">{{ t('Currency') }}<input v-model="product.document.pricing.currency" maxlength="3" pattern="[A-Z]{3}" class="native-input mt-2" @input="product.document.pricing.status='draft'"/></label>
        <div v-for="(tier,index) in product.document.pricing.tiers" :key="index" class="grid items-end gap-3 sm:grid-cols-[1fr_1fr_1fr_auto]">
          <label>{{ t('Minimum quantity') }}<input v-model.number="tier.minimumQuantity" type="number" min="1" step="1" class="native-input mt-2" @input="product.document.pricing.status='draft'"/></label>
          <label>{{ t('Maximum quantity') }}<input :value="tier.maximumQuantity??''" type="number" :min="tier.minimumQuantity" step="1" :placeholder="t('No maximum')" class="native-input mt-2" @input="tier.maximumQuantity=text($event)===''?null:Number(text($event));product.document.pricing.status='draft'"/></label>
          <label>{{ t('Price per piece') }}<input v-model.number="tier.unitPrice" type="number" min="0" step="1" class="native-input mt-2" @input="product.document.pricing.status='draft'"/></label>
          <Button size="sm" variant="ghost" @click="product.document.pricing.tiers.splice(index,1);product.document.pricing.status='draft'">{{ t('Remove') }}</Button>
        </div>
        <Button size="sm" variant="outline" @click="addTier">{{ t('Add quantity price') }}</Button>
        <label v-if="canApprove" class="flex gap-2"><input type="checkbox" :checked="product.document.pricing.status==='confirmed'" @change="product.document.pricing.status=($event.target as HTMLInputElement).checked?'confirmed':'draft'"/>{{ t('Confirm prices for publication') }}</label>
      </template>
    </section>
    <section class="space-y-4" aria-labelledby="product-images-title">
      <h3 id="product-images-title" class="font-semibold">{{ t('Product images') }}</h3>
      <div class="flex flex-wrap gap-3"><figure v-for="image in product.document.media" :key="image.id" class="w-36"><img :src="imageUrl(image.sha256)" :alt="image.alt??''" class="h-36 w-36 rounded-lg bg-muted object-contain"/><input v-model="image.alt" :aria-label="t('Image description')" class="native-input mt-2" :placeholder="t('Image description')"/><select v-model="image.role" class="native-select mt-2" :aria-label="t('Image role')" @change="roleChanged(image.id)"><option value="photo">{{ t('Photo') }}</option><option value="model">{{ t('Model photos') }}</option><option value="cutout">{{ t('Product views') }}</option><option value="detail">{{ t('Details') }}</option><option value="fabric">{{ t('Fabric photos') }}</option><option value="reference">{{ t('Private reference') }}</option></select><Button size="sm" variant="ghost" @click="detach(image.id)">{{ t('Detach') }}</Button></figure></div>
      <div class="flex items-center justify-between gap-3"><div><h3 class="font-semibold">{{ t('Image groups') }}</h3><p class="text-sm text-muted-foreground">{{ t('Group model photos or product views to switch the whole catalogue together.') }}</p></div><Button size="sm" variant="outline" @click="addGroup">{{ t('Add image group') }}</Button></div>
      <p v-if="!product.mediaGroups?.length" class="text-sm text-muted-foreground">{{ t('Images currently use their roles. Add groups for explicit photo selections.') }}</p>
      <article v-for="group in product.mediaGroups" :key="group.id" class="space-y-3 rounded-lg border border-border p-4">
        <div class="grid gap-3 sm:grid-cols-[1fr_1fr_auto]"><label>{{ t('Group name') }}<input v-model="group.label" maxlength="120" class="native-input mt-2"/></label><label>{{ t('Image category') }}<select v-model="group.category" class="native-select mt-2"><option v-for="category in categories" :key="category.value" :value="category.value">{{ t(category.label) }}</option></select></label><Button size="sm" variant="ghost" @click="product.mediaGroups=product.mediaGroups?.filter(item=>item.id!==group.id)??[]">{{ t('Remove group') }}</Button></div>
        <div class="flex flex-wrap gap-3"><label v-for="image in product.document.media.filter(item=>item.role!=='reference')" :key="image.id" class="w-28 space-y-2"><img :src="imageUrl(image.sha256)" :alt="image.alt??''" class="h-24 w-28 rounded-md bg-muted object-contain"/><span class="flex items-start gap-2 text-xs"><input v-model="group.mediaIds" type="checkbox" :value="image.id"/><span class="break-all">{{ image.alt||image.fileName }}</span></span></label></div>
      </article>
    </section>
  </div>
</template>
