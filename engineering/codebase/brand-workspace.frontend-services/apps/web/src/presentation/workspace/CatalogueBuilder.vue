<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { useCommerce } from '@/application/workspace/UseCommerce';
import type { CommerceDraft, CommerceRelease, Locale } from '@/domain/workspace/Commerce';
import type { DesignPage } from '@/domain/workspace/EditorModels';
import { catalogueLayouts, composeCataloguePages, productName, type CataloguePageLayout } from '@/domain/workspace/CatalogueComposer';

defineOptions({ name: 'CatalogueBuilder' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; locale: Locale; disabled: boolean; hasDocument: boolean; initialBrandId?: string }>();
const emit = defineEmits<{ pages: [pages: DesignPage[], brandName: string]; close: [] }>();
const api = useCommerce(props.access), controller = new AbortController();
const brandId = ref(props.initialBrandId ?? ''), releaseId = ref('draft'), query = ref(''), layout = ref<CataloguePageLayout>('product');
const draft = ref<CommerceDraft | null>(null), releases = ref<CommerceRelease[]>([]);
const images = ref<Record<string, string[]>>({}), loading = ref(false), error = ref('');
let sequence = 0;
const source = computed<CommerceDraft | null>(() => {
  if (releaseId.value === 'draft') return draft.value;
  const release = releases.value.find(item => item.id === releaseId.value && !item.withdrawnAt);
  return release ? { brandId: release.brandId, revision: release.sourceRevision, snapshot: release.snapshot, updatedAt: release.publishedAt } : null;
});
const products = computed(() => source.value?.snapshot.products.filter(item => item.document.status !== 'archived') ?? []);
const visible = computed(() => products.value.filter(item => `${productName(item, props.locale)} ${item.document.id}`.toLowerCase().includes(query.value.toLowerCase())));
const selected = computed(() => products.value.filter(item => Object.hasOwn(images.value, item.document.id)));
const capacity = computed(() => layout.value === 'collage' ? 6 : 2);
const warnings = computed(() => selected.value.reduce((sum, item) => sum + (images.value[item.document.id] ?? []).filter(hash => !source.value?.snapshot.mediaApprovals[hash]?.approvedForPublication).length, 0));
watch(() => props.workspace.brands.value, brands => { if (!brandId.value && brands?.length) brandId.value = brands[0]!.id; }, { immediate: true });
watch(brandId, () => { void load(); }, { immediate: true });
watch(releaseId, () => { images.value = {}; });
watch(layout, () => { images.value = Object.fromEntries(Object.entries(images.value).map(([id, hashes]) => [id, hashes.slice(0, capacity.value)])); });
onScopeDispose(() => controller.abort());
async function load(): Promise<void> {
  const request = ++sequence; draft.value = null; releases.value = []; images.value = {}; releaseId.value = 'draft'; error.value = '';
  if (!brandId.value) return;
  loading.value = true;
  const org = props.workspace.organization.value!.id, brand = brandId.value;
  try {
    const [current, history] = await Promise.all([api.get(org, brand, controller.signal), api.releases(org, brand, controller.signal)]);
    if (controller.signal.aborted || request !== sequence) return;
    if (!current.ok) { error.value = failureMessage(current.failure); return; }
    draft.value = current.value;
    if (history.ok) releases.value = history.value; else error.value = failureMessage(history.failure);
  } finally { if (request === sequence) loading.value = false; }
}
function toggleProduct(id: string): void {
  if (Object.hasOwn(images.value, id)) { const next = { ...images.value }; delete next[id]; images.value = next; }
  else {
    const first = products.value.find(item => item.document.id === id)?.document.media[0];
    images.value = { ...images.value, [id]: first ? [first.sha256] : [] };
  }
}
function toggleImage(id: string, hash: string): void {
  const current = images.value[id] ?? [];
  if (current.includes(hash)) images.value = { ...images.value, [id]: current.filter(item => item !== hash) };
  else if (current.length < capacity.value) images.value = { ...images.value, [id]: [...current, hash] };
}
function add(): void {
  if (!source.value || props.disabled || loading.value) return;
  try {
    const pages = composeCataloguePages(props.workspace.organization.value!.id, source.value,
      selected.value.map(item => ({ productId: item.document.id, imageHashes: images.value[item.document.id] ?? [] })), layout.value, props.locale);
    emit('pages', pages, source.value.snapshot.identity.name);
  } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Unable to compose pages.'; }
}
</script>

<template>
  <section class="catalogue-builder" aria-label="Catalogue builder">
    <header><div><p class="eyebrow">Build your edition</p><h2>Products, photographs, pages.</h2><p>Choose your source, select garments, and give each one a layout.</p></div><button type="button" class="quiet-button" @click="emit('close')">Close builder</button></header>
    <div class="builder-controls">
      <label class="field-label">Catalogue brand<select aria-label="Catalogue brand" v-model="brandId" :disabled="loading || disabled"><option value="">Choose brand</option><option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">{{ brand.name }}</option></select></label>
      <label class="field-label">Product source<select aria-label="Product source" v-model="releaseId" :disabled="loading || disabled"><option value="draft">Current saved draft{{ draft ? ` · revision ${draft.revision}` : '' }}</option><option v-for="release in releases.filter(item => !item.withdrawnAt)" :key="release.id" :value="release.id">Published release {{ release.revision }}</option></select></label>
      <label class="field-label">Find products<input aria-label="Find products" v-model="query" placeholder="Name or product ID"></label>
    </div>
    <fieldset class="layout-picker" :disabled="disabled"><legend>Page template</legend><label v-for="option in catalogueLayouts" :key="option.id" :class="{ chosen: layout === option.id }"><input v-model="layout" type="radio" name="catalogue-layout" :value="option.id"><strong>{{ option.title }}</strong><span>{{ option.description }}</span></label></fieldset>
    <p v-if="error" role="alert" class="editor-alert">{{ error }}</p><p v-if="loading" role="status">Loading saved products…</p>
    <p v-else-if="!source">Create and save products in Products &amp; releases to start your catalogue.</p>
    <div v-else class="builder-products">
      <article v-for="product in visible" :key="product.document.id" :class="{ chosen: Object.hasOwn(images, product.document.id) }">
        <label class="product-choice"><input type="checkbox" :checked="Object.hasOwn(images, product.document.id)" :disabled="disabled || !product.document.media.length" @change="toggleProduct(product.document.id)"><span><strong>{{ productName(product, locale) }}</strong><small>{{ product.document.id }} · product revision {{ product.revision }}</small></span></label>
        <p v-if="!product.document.media.length" class="template-help">Add a product image in Products &amp; releases.</p>
        <div v-if="Object.hasOwn(images, product.document.id)" class="image-picker"><label v-for="image in product.document.media" :key="image.sha256" :class="{ chosen: images[product.document.id]?.includes(image.sha256) }"><input type="checkbox" :checked="images[product.document.id]?.includes(image.sha256)" :disabled="disabled || (!images[product.document.id]?.includes(image.sha256) && (images[product.document.id]?.length ?? 0) >= capacity)" @change="toggleImage(product.document.id, image.sha256)"><img :src="api.image(workspace.organization.value!.id, brandId, image.sha256)" :alt="image.alt || image.fileName" loading="lazy"><span>{{ image.role }}{{ source.snapshot.mediaApprovals[image.sha256]?.approvedForPublication ? '' : ' · unreviewed' }}</span></label></div>
      </article>
      <p v-if="!visible.length">No products match this search.</p>
    </div>
    <footer><div><p>{{ selected.length }} products selected · up to {{ capacity }} images per page</p><p v-if="warnings" class="review-warning">{{ warnings }} chosen images need publication review. This edition remains a private working design.</p><p class="template-help">Pages copy saved product revisions. Editing a page price changes this edition; canonical product prices stay in Products &amp; releases.</p></div><Button :is-disabled="disabled || loading || !selected.length" @click="add">{{ hasDocument ? 'Insert selected pages' : 'Create catalogue' }}</Button></footer>
  </section>
</template>

<style scoped>
.catalogue-builder { padding: 24px; background: #fbf9f3; border: 1px solid #d8d0c2; border-radius: 12px; margin: 20px 0; display: grid; gap: 22px; }
header, footer { display: flex; flex-wrap: wrap; gap: 20px; align-items: center; justify-content: space-between; }
h2 { font-size: 25px; font-family: Georgia, serif; margin: 6px 0; } p { margin: 4px 0; font-size: 13px; line-height: 1.6; }
.builder-controls { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
.layout-picker { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; border: 0; padding: 0; }
legend { margin-bottom: 10px; font-size: 13px; }.layout-picker label { display: grid; grid-template-columns: 18px 1fr; gap: 8px; padding: 15px; border: 1px solid #d8d0c2; border-radius: 8px; cursor: pointer; }
.layout-picker span { grid-column: 2; font-size: 12px; line-height: 1.5; }.chosen { background: #f1ebdd; border-color: #8a7451 !important; }
.builder-products { max-height: 430px; overflow: auto; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
article { min-width: 0; padding: 15px; border: 1px solid #d8d0c2; border-radius: 8px; }.product-choice { display: flex; align-items: center; gap: 12px; cursor: pointer; }strong, small { display: block; }small { color: #6b655b; font-size: 11px; margin-top: 4px; }
.image-picker { display: flex; gap: 10px; overflow-x: auto; padding-top: 14px; }.image-picker label { width: 95px; flex-shrink: 0; padding: 5px; border: 1px solid #d8d0c2; border-radius: 6px; cursor: pointer; }.image-picker img { width: 83px; height: 96px; object-fit: cover; }.image-picker span { display: block; font-size: 10px; overflow-wrap: anywhere; }.review-warning { color: #72501e; }.template-help { color: #6b655b; font-size: 12px; max-width: 700px; }
@media(max-width: 720px) { .catalogue-builder { padding: 16px; }.builder-controls,.layout-picker,.builder-products { grid-template-columns: 1fr; }.builder-products { max-height: 480px; } }
</style>
