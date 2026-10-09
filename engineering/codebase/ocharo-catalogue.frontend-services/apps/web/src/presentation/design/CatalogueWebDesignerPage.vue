<script setup lang="ts">
import { useSsoSession } from '../../application/common/UseSsoSession';
const { canEdit: ssoCanEdit } = useSsoSession();
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/ui-vue/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, ref } from 'vue';
import { Columns2, Images, LayoutGrid } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { SegmentedPicker, ToggleInput } from '@wow-two-beta/ui-vue/presentation/forms';
import { useCatalogueDocument } from '../../application/design/UseCatalogueDocument';
import { requestConfirmation } from '../../application/common/UseFeedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { productEligible, productName } from '../../domain/design/CatalogueDesign';
import type { CatalogueWebPage } from '../../domain/design/EditorModels';
import CatalogueDesignerBar from './CatalogueDesignerBar.vue';
import CatalogueSharedSettings from './CatalogueSharedSettings.vue';
import CatalogueWebPreview from './CatalogueWebPreview.vue';
import CataloguePreviewViewport from './CataloguePreviewViewport.vue';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import ContentSkeleton from '../common/ContentSkeleton.vue';
defineOptions({ name: 'CatalogueWebDesignerPage' });
const editor = useCatalogueDocument(),
  { t } = useUiLocale(),
  selected = ref(''),
  viewport = ref('1440'),
  search = ref('');
const pages = computed(() => editor.snapshot.value?.catalogue?.web.pages ?? []),
  page = computed(() => pages.value.find((item) => item.id === selected.value) ?? pages.value[0]);
const products = computed(() =>
  (editor.source.value?.products ?? []).filter(
    (product) =>
      product.document.status !== 'archived' &&
      `${productName(product, editor.snapshot.value?.locale ?? 'uz')} ${product.document.id}`
        .toLowerCase()
        .includes(search.value.toLowerCase()),
  ),
);
const layouts = computed(() => [
  { value: 'spread', label: t('Product spread'), icon: Columns2 },
  { value: 'grid', label: t('Product grid'), icon: LayoutGrid },
  { value: 'collage', label: t('Photo collage'), icon: Images },
]);
const themes = computed(() => [
  { value: 'editorial', label: t('Editorial') },
  { value: 'grid', label: t('Grid') },
]);
const sizes = computed(() => [
  { value: '1440', label: t('Desktop') + ' · 1440' },
  { value: '768', label: t('Tablet') + ' · 768' },
  { value: '390', label: t('Mobile') + ' · 390' },
]);
function add() {
  const id = crypto.randomUUID();
  editor.change((next) =>
    next.catalogue!.web.pages.push({ id, title: t('New page'), layout: 'spread', isActive: true, productIds: [] }),
  );
  selected.value = id;
}
function edit(change: (page: CatalogueWebPage) => void) {
  if (page.value)
    editor.change((next) => {
      const target = next.catalogue!.web.pages.find((item) => item.id === page.value!.id);
      if (target) change(target);
    });
}
function setLayout(value: string | readonly string[] | null) {
  if (value === 'spread' || value === 'grid' || value === 'collage') edit((item) => (item.layout = value));
}
function move(index: number, delta: number) {
  editor.change((next) => {
    const list = next.catalogue!.web.pages,
      target = index + delta;
    if (target < 0 || target >= list.length) return;
    const [value] = list.splice(index, 1);
    if (value) list.splice(target, 0, value);
  });
}
function duplicate() {
  if (!page.value) return;
  const copy = JSON.parse(JSON.stringify(page.value)) as CatalogueWebPage;
  copy.id = crypto.randomUUID();
  copy.title = `${copy.title} · ${t('Copy')}`.slice(0, 160);
  editor.change((next) => next.catalogue!.web.pages.push(copy));
  selected.value = copy.id;
}
async function remove() {
  if (
    !page.value ||
    !(await requestConfirmation(t('Remove this page? You can restore it with Undo.'), {
      confirmLabel: t('Remove page'),
    }))
  )
    return;
  const id = page.value.id;
  editor.change((next) => (next.catalogue!.web.pages = next.catalogue!.web.pages.filter((item) => item.id !== id)));
}
function toggle(id: string, checked: boolean) {
  edit(
    (page) => (page.productIds = checked ? [...page.productIds, id] : page.productIds.filter((value) => value !== id)),
  );
}
function reorderProduct(index: number, delta: number) {
  edit((page) => {
    const target = index + delta;
    if (target < 0 || target >= page.productIds.length) return;
    const [id] = page.productIds.splice(index, 1);
    if (id) page.productIds.splice(target, 0, id);
  });
}
const { refresh: refreshDocument, refreshing } = useRefresh(() => editor.load());
const refresh = () => editor.reload(refreshDocument);
const resultBusy = computed(() => editor.loading.value || refreshing.value);
</script>
<template>
  <section class="catalogue-query-surface">
    <div class="flex flex-wrap items-center justify-between gap-4 mb-5">
      <h1 class="font-editorial text-2xl">{{ t('Web designer') }}</h1>
      <RefreshButton :busy="resultBusy" :disabled="editor.busy.value && !editor.loading.value" @refresh="refresh" />
    </div>
    <div class="catalogue-result-region">
      <ContentSkeleton
        v-if="resultBusy && !editor.snapshot.value"
        :label="t('Loading catalogue')"
        variant="designer-web"
      />
      <ResultEmpty v-else-if="!editor.snapshot.value" :label="t('Content unavailable')" />
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading catalogue')">
        <section class="native-designer">
          <CatalogueDesignerBar :editor="editor" mode="web" />
          <p v-if="!editor.snapshot.value.catalogue?.outputs.web" class="panel">
            {{ t('Web output is disabled. Enable it in catalogue settings.') }}
          </p>
          <CatalogueSharedSettings :editor="editor" />
          <div class="designer-grid">
            <aside class="panel page-panel">
              <h2>{{ t('Web pages') }}</h2>
              <button class="btn-outline" :disabled="editor.busy.value || pages.length >= 100" @click="add">
                + {{ t('Add page') }}
              </button>
              <ol class="pages">
                <li v-for="(item, index) in pages" :key="item.id" :class="{ selected: page?.id === item.id }">
                  <button class="page-name" @click="selected = item.id">
                    {{ index + 1 }} · {{ item.title }}
                    <small class="text-muted-foreground">{{
                      t('{count} products', { count: item.productIds.length })
                    }}</small>
                    <small v-if="!item.isActive">{{ t('Inactive') }}</small>
                  </button>
                  <div class="actions">
                    <button
                      :disabled="index === 0 || editor.busy.value"
                      :aria-label="t('Move page up')"
                      @click="move(index, -1)"
                    >
                      ↑</button
                    ><button
                      :disabled="index === pages.length - 1 || editor.busy.value"
                      :aria-label="t('Move page down')"
                      @click="move(index, 1)"
                    >
                      ↓
                    </button>
                  </div>
                </li>
              </ol>
              <template v-if="page"
                ><label
                  >{{ t('Page title')
                  }}<input
                    :value="page.title"
                    class="field"
                    maxlength="160"
                    @change="edit((item) => (item.title = ($event.target as HTMLInputElement).value))" /></label
                ><label class="check"
                  ><input
                    type="checkbox"
                    :checked="page.isActive"
                    @change="edit((item) => (item.isActive = ($event.target as HTMLInputElement).checked))"
                  />{{ t('Active page') }}</label
                >
                <div class="actions">
                  <button class="btn-outline" :disabled="pages.length >= 100 || editor.busy.value" @click="duplicate">
                    {{ t('Duplicate page') }}</button
                  ><button class="btn-ghost" :disabled="!ssoCanEdit || editor.busy.value" @click="remove">
                    {{ t('Remove page') }}
                  </button>
                </div>
                <h3>{{ t('Products on this page') }}</h3>
                <p class="hint">
                  {{ t('Selected products stay visible while you design. Review is required before publication.') }}
                </p>
                <input v-model="search" type="search" class="field" :placeholder="t('Find a product')" />
                <div class="products">
                  <label v-for="product in products" :key="product.document.id" class="check"
                    ><input
                      type="checkbox"
                      :checked="page.productIds.includes(product.document.id)"
                      @change="toggle(product.document.id, ($event.target as HTMLInputElement).checked)"
                    /><span
                      >{{ productName(product, editor.snapshot.value.locale)
                      }}<small v-if="!productEligible(product, editor.snapshot.value.locale)">{{
                        t('Needs review')
                      }}</small></span
                    ></label
                  >
                </div>
                <ol class="product-order">
                  <li v-for="(id, index) in page.productIds" :key="id">
                    <span>{{ id }}</span
                    ><button
                      :disabled="index === 0"
                      :aria-label="t('Move product up')"
                      @click="reorderProduct(index, -1)"
                    >
                      ↑</button
                    ><button
                      :disabled="index === page.productIds.length - 1"
                      :aria-label="t('Move product down')"
                      @click="reorderProduct(index, 1)"
                    >
                      ↓
                    </button>
                  </li>
                </ol>
              </template>
            </aside>
            <div class="preview-panel">
              <div class="preview-toolbar">
                <h2 class="w-full">{{ t('Draft preview') }}</h2>
                <div class="min-w-0 flex-1 basis-48">
                  <CatalogueSelect v-model="viewport" :options="sizes" :label="t('Preview size')" />
                </div>
                <div class="min-w-0 flex-1 basis-48">
                  <CatalogueSelect
                    :model-value="editor.snapshot.value.catalogue!.web.theme"
                    :options="themes"
                    :label="t('Web theme')"
                    @update:model-value="
                      (value) => editor.change((next) => (next.catalogue!.web.theme = value as 'editorial' | 'grid'))
                    "
                  />
                </div>
              </div>
              <div v-if="page" class="mb-4 flex flex-wrap items-center justify-between gap-3">
                <span class="text-sm font-medium">{{ page.title }}</span>
                <SegmentedPicker
                  :model-value="page.layout"
                  :aria-label="t('Page template')"
                  @update:model-value="setLayout"
                >
                  <ToggleInput
                    v-for="layout in layouts"
                    :key="layout.value"
                    :value="layout.value"
                    :aria-label="layout.label"
                    :title="layout.label"
                    :is-disabled="editor.busy.value"
                    class="min-h-11 gap-2"
                  >
                    <Icon :icon="layout.icon" :size="16" />
                    <span>{{ layout.label }}</span>
                  </ToggleInput>
                </SegmentedPicker>
              </div>
              <ResultEmpty v-if="!pages.length" :label="t('No pages yet. Add a page to start designing.')" />
              <CataloguePreviewViewport v-else :width="Number(viewport)">
                <SkeletonStateSlot class="catalogue-value" :is-block="true"
                  ><CatalogueWebPreview
                    :snapshot="editor.snapshot.value"
                    :products="editor.source.value?.products ?? []"
                    :title="editor.title.value"
                    :brand-name="editor.source.value?.settings.brandName"
                    :page-id="page?.id"
                    editing
                /></SkeletonStateSlot>
              </CataloguePreviewViewport>
              <p class="hint">
                {{ t('The public website reflows to the reader’s screen. PDF page dimensions are edited separately.') }}
              </p>
            </div>
          </div>
        </section>
      </SkeletonStateGroup>
    </div>
  </section>
</template>
<style scoped>
.native-designer {
  min-width: 0;
}
.designer-grid {
  display: grid;
  grid-template-columns: 300px minmax(0, 1fr);
  gap: 20px;
}
.page-panel {
  display: grid;
  align-content: start;
  gap: 15px;
  padding: 18px;
}
.page-panel h2,
.preview-toolbar h2 {
  font:
    24px Georgia,
    serif;
}
.page-panel label {
  display: grid;
  gap: 6px;
  font-size: 12px;
}
.page-panel .check {
  display: flex;
  gap: 8px;
  align-items: start;
}
.check input {
  margin-top: 4px;
}
.check small {
  display: block;
  color: var(--color-muted-foreground);
  font-size: 11px;
}
.pages {
  display: grid;
  gap: 8px;
}
.pages li {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 9px;
  display: flex;
  gap: 8px;
}
.pages li.selected {
  outline: 2px solid var(--color-primary);
}
.page-name {
  text-align: start;
  flex: 1;
  font-size: 13px;
}
.page-name small {
  display: block;
}
.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.actions button {
  font-size: 12px;
}
.products {
  display: grid;
  gap: 12px;
  max-height: 310px;
  overflow: auto;
}
.product-order li {
  display: flex;
  gap: 10px;
  align-items: center;
  font-size: 12px;
  padding: 6px 0;
}
.product-order span {
  flex: 1;
  overflow-wrap: anywhere;
}
.hint {
  font-size: 12px;
  line-height: 1.6;
  color: var(--color-muted-foreground);
}
.preview-panel {
  min-width: 0;
}
.preview-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 15px;
}
@media (max-width: 1050px) {
  .designer-grid {
    grid-template-columns: 1fr;
  }
  .page-panel {
    grid-template-columns: 1fr;
  }
  .pages {
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  }
}
</style>
