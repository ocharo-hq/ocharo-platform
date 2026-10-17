<script setup lang="ts">
import { useRefresh } from '@wow-two-beta/fe/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/fe/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, ref } from 'vue';
import { useCatalogueDocument, describeDesignFailure } from '../../application/design/UseCatalogueDocument';
import { notifyError, requestConfirmation } from '../../application/common/UseFeedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import {
  catalogueSettings,
  coverPage,
  productMedia,
  productName,
  productPage,
  randomizeImages,
  refreshCatalogue,
  replaceImage,
  mediaUrl,
} from '../../domain/design/CatalogueDesign';
import { originalCatalogue } from '../../domain/design/OriginalCatalogue';
import type { CatalogueBinding, DesignElement, DesignPage } from '../../domain/design/EditorModels';
import CatalogueDesignerBar from './CatalogueDesignerBar.vue';
import CatalogueSharedSettings from './CatalogueSharedSettings.vue';
import DesignCanvas from './DesignCanvas.vue';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import ContentSkeleton from '../common/ContentSkeleton.vue';
defineOptions({ name: 'CataloguePdfDesignerPage' });
const editor = useCatalogueDocument(),
  { t } = useUiLocale(),
  selectedPage = ref(''),
  selectedElement = ref<string | null>(null),
  chosenProduct = ref(''),
  layout = ref('product'),
  zoom = ref('100'),
  bindingProduct = ref(''),
  bindingField = ref('name'),
  bindingIndex = ref('0');
const pages = computed(() => editor.snapshot.value?.pages ?? []),
  page = computed(() => pages.value.find((item) => item.id === selectedPage.value) ?? pages.value[0]),
  element = computed(() => page.value?.elements.find((item) => item.id === selectedElement.value));
const products = computed(() => editor.source.value?.products ?? []),
  productOptions = computed(() => [
    { value: '', label: t('Choose a product') },
    ...products.value
      .filter((product) => product.document.status !== 'archived')
      .map((product) => ({
        value: product.document.id,
        label: productName(product, editor.snapshot.value?.locale ?? 'uz'),
      })),
  ]);
const availableImages = computed(() => {
  const product = products.value.find((item) => item.document.id === element.value?.productId);
  return product ? productMedia(product, catalogueSettings(editor.snapshot.value ?? undefined).mediaGroup) : [];
});
const layouts = computed(() => [
  { value: 'product', label: t('Product page') },
  { value: 'continuation', label: t('Product continuation') },
  { value: 'collage', label: t('Photo collage') },
]);
const fonts = computed(() => [
    { value: 'serif', label: t('Serif') },
    { value: 'sans', label: t('Sans serif') },
  ]),
  fits = computed(() => [
    { value: 'contain', label: t('Fit image') },
    { value: 'cover', label: t('Fill frame') },
  ]);
const fields = computed(() =>
  ['name', 'fabric', 'decoration', 'description', 'details', 'sizes', 'price', 'price-label', 'currency'].map(
    (value) => ({
      value,
      label: t(
        value === 'name'
          ? 'Product name'
          : value === 'fabric'
            ? 'Fabric'
            : value === 'decoration'
              ? 'Embellishment'
              : value === 'description'
                ? 'Description'
                : value === 'details'
                  ? 'Product details'
                  : value === 'sizes'
                    ? 'Sizes'
                    : value === 'price'
                      ? 'Price'
                      : value === 'price-label'
                        ? 'Price label'
                        : 'Currency',
      ),
    }),
  ),
);
const alignments = computed(() => [
  { value: 'left', label: t('Align left') },
  { value: 'center', label: t('Align center') },
  { value: 'right', label: t('Align right') },
]);
const bindingTiers = computed(() =>
  (products.value.find((product) => product.document.id === bindingProduct.value)?.document.pricing?.tiers ?? []).map(
    (tier, index) => ({ value: String(index), label: `${tier.minimumQuantity}–${tier.maximumQuantity ?? '∞'}` }),
  ),
);
const zooms = [
  { value: '75', label: '75%' },
  { value: '100', label: '100%' },
  { value: '125', label: '125%' },
  { value: '150', label: '150%' },
];
function select(id: string) {
  selectedPage.value = id;
  selectedElement.value = null;
}
function editPage(change: (page: DesignPage) => void) {
  if (page.value)
    editor.change((next) => {
      const target = next.pages.find((item) => item.id === page.value!.id);
      if (target) change(target);
    });
}
function editElement(patch: Partial<DesignElement>) {
  if (element.value)
    editPage((page) => {
      const target = page.elements.find((item) => item.id === element.value!.id);
      if (target) Object.assign(target, patch);
    });
}
function addPage() {
  if (!editor.snapshot.value || pages.value.length >= 100) return;
  const product = products.value.find((item) => item.document.id === chosenProduct.value);
  const value = product
    ? productPage(product, editor.snapshot.value, layout.value as 'product' | 'continuation' | 'collage')
    : coverPage(editor.title.value, '', editor.snapshot.value.locale);
  editor.change((next) => next.pages.push(value));
  select(value.id);
}
function movePage(index: number, delta: number) {
  editor.change((next) => {
    const target = index + delta;
    if (target < 0 || target >= next.pages.length) return;
    const [value] = next.pages.splice(index, 1);
    if (value) next.pages.splice(target, 0, value);
  });
}
function duplicatePage() {
  if (!page.value) return;
  const copy = JSON.parse(JSON.stringify(page.value)) as DesignPage;
  copy.id = crypto.randomUUID();
  copy.title = `${copy.title} · ${t('Copy')}`.slice(0, 160);
  copy.elements = copy.elements.map((item) => ({ ...item, id: crypto.randomUUID() }));
  editor.change((next) => next.pages.push(copy));
  select(copy.id);
}
async function removePage() {
  if (
    !page.value ||
    !(await requestConfirmation(t('Remove this page? You can restore it with Undo.'), {
      confirmLabel: t('Remove page'),
    }))
  )
    return;
  const id = page.value.id;
  editor.change((next) => (next.pages = next.pages.filter((item) => item.id !== id)));
  selectedElement.value = null;
}
function randomize(all = false) {
  if (editor.snapshot.value)
    editor.replace(randomizeImages(editor.snapshot.value, products.value, all ? undefined : page.value?.id));
}
function replace(sha: string) {
  if (editor.snapshot.value && page.value && element.value)
    try {
      editor.replace(replaceImage(editor.snapshot.value, page.value.id, element.value.id, sha, products.value));
    } catch (cause) {
      notifyError(describeDesignFailure(cause, t, 'Unable to change this image.'));
    }
}
function addElement(type: DesignElement['type']) {
  const id = crypto.randomUUID();
  editPage((page) =>
    page.elements.push({
      id,
      type,
      label: t(type === 'text' ? 'Text' : type === 'shape' ? 'Shape' : 'Image'),
      x: 18,
      y: 30,
      width: type === 'text' ? 130 : 70,
      height: type === 'text' ? 20 : 70,
      ...(type === 'text'
        ? { text: t('New text'), fontSize: 5, fontFamily: 'sans', color: '#302b25' }
        : type === 'shape'
          ? { background: '#d1c3af' }
          : { fit: 'contain', productId: chosenProduct.value || undefined }),
    }),
  );
  selectedElement.value = id;
}
function duplicateElement() {
  if (!element.value) return;
  const copy = { ...element.value, id: crypto.randomUUID(), x: element.value.x + 3, y: element.value.y + 3 };
  editPage((page) => page.elements.push(copy));
  selectedElement.value = copy.id;
}
function removeElement() {
  const id = selectedElement.value;
  editPage((page) => (page.elements = page.elements.filter((item) => item.id !== id)));
  selectedElement.value = null;
}
function layer(delta: number) {
  if (!element.value) return;
  const id = element.value.id;
  editPage((page) => {
    const index = page.elements.findIndex((item) => item.id === id),
      target = index + delta;
    if (target < 0 || target >= page.elements.length) return;
    const [item] = page.elements.splice(index, 1);
    if (item) page.elements.splice(target, 0, item);
  });
}
function bind() {
  if (!bindingProduct.value || !element.value) return;
  const binding: CatalogueBinding = {
    kind: 'product',
    field: bindingField.value as Extract<CatalogueBinding, { kind: 'product' }>['field'],
    productId: bindingProduct.value,
    ...(['price', 'price-label'].includes(bindingField.value) ? { index: Number(bindingIndex.value) } : {}),
  };
  editor.change((next) => {
    const target = next.pages
      .find((item) => item.id === page.value?.id)
      ?.elements.find((item) => item.id === element.value?.id);
    if (target) {
      target.binding = binding;
      target.productId = bindingProduct.value;
    }
    next.pages = refreshCatalogue(next, products.value).pages;
  });
}
function unlink() {
  editPage((page) => {
    const target = page.elements.find((item) => item.id === element.value?.id);
    if (target) delete target.binding;
  });
}
async function original() {
  if (
    !editor.snapshot.value ||
    !(await requestConfirmation(
      t('Replace PDF pages with the original catalogue design? Undo keeps your current pages.'),
      { confirmLabel: t('Use original design') },
    ))
  )
    return;
  try {
    const result = originalCatalogue(editor.snapshot.value, products.value);
    editor.replace(result.snapshot);
    selectedElement.value = null;
    editor.notice.value =
      result.warnings.join('\n') ||
      t('Original design connected to current products. Review independent text before exporting.');
  } catch (cause) {
    notifyError(describeDesignFailure(cause, t, 'Unable to load the original design.'));
  }
}
const { refresh: refreshDocument, refreshing } = useRefresh(() => editor.load());
const refresh = () => editor.reload(refreshDocument);
const resultBusy = computed(() => editor.loading.value || refreshing.value);
</script>
<template>
  <section class="catalogue-query-surface">
    <div class="flex flex-wrap items-center justify-between gap-4 mb-5">
      <h1 class="font-editorial text-2xl">{{ t('PDF designer') }}</h1>
      <RefreshButton :busy="resultBusy" :disabled="editor.busy.value && !editor.loading.value" @refresh="refresh" />
    </div>
    <div class="catalogue-result-region">
      <ContentSkeleton
        v-if="resultBusy && !editor.snapshot.value"
        :label="t('Loading catalogue')"
        variant="designer-pdf"
      />
      <ResultEmpty v-else-if="!editor.snapshot.value" :label="t('Content unavailable')" />
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading catalogue')">
        <section class="native-designer">
          <CatalogueDesignerBar :editor="editor" mode="pdf" /><CatalogueSharedSettings :editor="editor" />
          <div class="export-bar">
            <button
              class="btn-primary"
              :disabled="editor.busy.value || !editor.exportable.value?.snapshot.pages.length"
              @click="editor.download()"
            >
              {{ t('Download PDF') }}</button
            ><button class="btn-outline" :disabled="editor.busy.value || !page" @click="editor.download(page?.id)">
              {{ t('Download page PDF') }}</button
            ><button
              class="btn-outline"
              :disabled="editor.busy.value || !page"
              @click="editor.download(page?.id, 'png')"
            >
              {{ t('Download page PNG') }}</button
            ><button class="btn-outline" :disabled="editor.busy.value" @click="randomize(true)">
              {{ t('Randomize catalogue images') }}</button
            ><button
              class="btn-ghost"
              :disabled="editor.busy.value || !['uz', 'ru', 'en'].includes(editor.snapshot.value.locale)"
              @click="original"
            >
              {{ t('Use original design') }}
            </button>
          </div>
          <p v-if="editor.exportable.value?.excluded.length" class="panel excluded">
            {{
              t('These pages are excluded from export because they are inactive or contain products awaiting review:')
            }}
            {{ editor.exportable.value.excluded.join(', ') }}
          </p>
          <div class="designer-grid">
            <aside class="panel page-rail">
              <h2>{{ t('PDF pages') }}</h2>
              <ol>
                <li v-for="(item, index) in pages" :key="item.id" :class="{ selected: item.id === page?.id }">
                  <button class="page-name" @click="select(item.id)">
                    {{ index + 1 }} · {{ item.title }}<small v-if="item.isActive === false">{{ t('Inactive') }}</small>
                  </button>
                  <div class="actions">
                    <button
                      :disabled="index === 0 || editor.busy.value"
                      :aria-label="t('Move page up')"
                      @click="movePage(index, -1)"
                    >
                      ↑</button
                    ><button
                      :disabled="index === pages.length - 1 || editor.busy.value"
                      :aria-label="t('Move page down')"
                      @click="movePage(index, 1)"
                    >
                      ↓
                    </button>
                  </div>
                </li>
              </ol>
              <CatalogueSelect
                v-model="chosenProduct"
                :options="productOptions"
                :label="t('Choose a product')"
              /><CatalogueSelect v-model="layout" :options="layouts" :label="t('Page template')" /><button
                class="btn-outline"
                :disabled="editor.busy.value || pages.length >= 100"
                @click="addPage"
              >
                + {{ chosenProduct ? t('Add product page') : t('Add cover page') }}</button
              ><template v-if="page"
                ><label
                  >{{ t('Page title')
                  }}<input
                    :value="page.title"
                    maxlength="160"
                    class="field"
                    @change="editPage((item) => (item.title = ($event.target as HTMLInputElement).value))" /></label
                ><label class="check"
                  ><input
                    type="checkbox"
                    :checked="page.isActive !== false"
                    @change="editPage((item) => (item.isActive = ($event.target as HTMLInputElement).checked))"
                  />{{ t('Active page') }}</label
                >
                <div class="actions">
                  <button
                    class="btn-outline"
                    :disabled="editor.busy.value || pages.length >= 100"
                    @click="duplicatePage"
                  >
                    {{ t('Duplicate page') }}</button
                  ><button class="btn-ghost" :disabled="editor.busy.value" @click="removePage">
                    {{ t('Remove page') }}
                  </button>
                </div>
                <button class="btn-outline" :disabled="editor.busy.value" @click="randomize()">
                  {{ t('Randomize page images') }}
                </button>
                <p class="hint">
                  {{ t('Page dimensions') }}: {{ page.width.toFixed(1) }} × {{ page.height.toFixed(1) }} mm
                </p>
                <div class="numbers">
                  <label
                    >{{ t('Width')
                    }}<input
                      :value="page.width"
                      type="number"
                      min="10"
                      max="1000"
                      step=".1"
                      class="field"
                      @change="
                        editPage((item) => (item.width = Number(($event.target as HTMLInputElement).value)))
                      " /></label
                  ><label
                    >{{ t('Height')
                    }}<input
                      :value="page.height"
                      type="number"
                      min="10"
                      max="1000"
                      step=".1"
                      class="field"
                      @change="editPage((item) => (item.height = Number(($event.target as HTMLInputElement).value)))"
                  /></label>
                </div>
                <label
                  >{{ t('Page background')
                  }}<input
                    type="color"
                    :value="page.background"
                    @input="editPage((item) => (item.background = ($event.target as HTMLInputElement).value))" /></label
              ></template>
            </aside>
            <div class="canvas-panel">
              <div class="canvas-toolbar">
                <CatalogueSelect v-model="zoom" :options="zooms" :label="t('Zoom')" /><button
                  class="btn-outline"
                  :disabled="!page || editor.busy.value"
                  @click="addElement('text')"
                >
                  + {{ t('Text') }}</button
                ><button class="btn-outline" :disabled="!page || editor.busy.value" @click="addElement('shape')">
                  + {{ t('Shape') }}</button
                ><button
                  class="btn-outline"
                  :disabled="!page || !chosenProduct || editor.busy.value"
                  @click="addElement('image')"
                >
                  + {{ t('Image') }}
                </button>
              </div>
              <div class="canvas-scroll">
                <ResultEmpty v-if="!pages.length" :label="t('No pages yet. Add a page to start designing.')" />
                <div
                  v-if="page"
                  class="canvas-sheet"
                  :style="{ width: Math.round((720 * Number(zoom)) / 100) + 'px' }"
                  :lang="editor.snapshot.value.locale"
                >
                  <SkeletonStateSlot class="catalogue-value" :is-block="true"
                    ><DesignCanvas
                      :page="page"
                      :selected-id="selectedElement"
                      :editable="!editor.busy.value"
                      @select="selectedElement = $event"
                      @change="(elements) => editPage((item) => (item.elements = elements))"
                  /></SkeletonStateSlot>
                </div>
                <p v-else class="panel">{{ t('Add a page to begin designing your PDF.') }}</p>
              </div>
              <p class="hint">
                {{ t('Drag to move. Use the corner handle to resize. Arrow keys move 0.5 mm; Shift moves 5 mm.') }}
              </p>
            </div>
            <aside class="panel inspector">
              <h2>{{ t('Selected element') }}</h2>
              <p v-if="!element" class="hint">{{ t('Select text or an image on the page to edit it.') }}</p>
              <template v-else
                ><label
                  >{{ t('Label')
                  }}<input
                    :value="element.label"
                    maxlength="256"
                    class="field"
                    @change="editElement({ label: ($event.target as HTMLInputElement).value })" /></label
                ><label class="check"
                  ><input
                    type="checkbox"
                    :checked="element.locked"
                    @change="editElement({ locked: ($event.target as HTMLInputElement).checked })"
                  />{{ t('Lock element') }}</label
                >
                <div class="numbers">
                  <label v-for="key in ['x', 'y', 'width', 'height'] as const" :key="key"
                    >{{ t(key) }} (mm)<input
                      :value="Number(element[key].toFixed(2))"
                      type="number"
                      step=".1"
                      :min="key === 'width' || key === 'height' ? 0.1 : -1000"
                      max="2000"
                      class="field"
                      @change="editElement({ [key]: Number(($event.target as HTMLInputElement).value) })"
                  /></label>
                </div>
                <label
                  >{{ t('Rotation')
                  }}<input
                    :value="element.rotation ?? 0"
                    type="range"
                    min="-180"
                    max="180"
                    step="1"
                    @input="editElement({ rotation: Number(($event.target as HTMLInputElement).value) })" /></label
                ><label
                  >{{ t('Opacity')
                  }}<input
                    :value="element.opacity ?? 1"
                    type="range"
                    min="0"
                    max="1"
                    step=".05"
                    @input="editElement({ opacity: Number(($event.target as HTMLInputElement).value) })"
                /></label>
                <template v-if="element.type === 'text'"
                  ><label
                    >{{ t('Text')
                    }}<textarea
                      :value="element.text"
                      class="field"
                      rows="5"
                      maxlength="10000"
                      :disabled="!!element.binding"
                      @change="editElement({ text: ($event.target as HTMLTextAreaElement).value })"
                    />
                  </label>
                  <p v-if="element.binding" class="hint">
                    {{ t('This text is connected to catalogue or product fields.') }}
                    <RouterLink to="/">{{ t('Edit products') }}</RouterLink>
                  </p>
                  <CatalogueSelect
                    :model-value="element.fontFamily ?? 'sans'"
                    :options="fonts"
                    :label="t('Font')"
                    @update:model-value="(value) => editElement({ fontFamily: value as 'serif' | 'sans' })"
                  /><CatalogueSelect
                    :model-value="element.align ?? 'left'"
                    :options="alignments"
                    :label="t('Text alignment')"
                    @update:model-value="(value) => editElement({ align: value as 'left' | 'center' | 'right' })"
                  /><label class="check"
                    ><input
                      type="checkbox"
                      :checked="element.fontWeight === 'bold'"
                      @change="
                        editElement({ fontWeight: ($event.target as HTMLInputElement).checked ? 'bold' : 'normal' })
                      "
                    />{{ t('Bold') }}</label
                  ><label
                    >{{ t('Font size') }} (mm)<input
                      :value="element.fontSize ?? 4"
                      type="number"
                      min=".5"
                      max="200"
                      step=".1"
                      class="field"
                      @change="editElement({ fontSize: Number(($event.target as HTMLInputElement).value) })" /></label
                  ><label
                    >{{ t('Text color')
                    }}<input
                      type="color"
                      :value="element.color ?? '#302b25'"
                      @input="editElement({ color: ($event.target as HTMLInputElement).value })"
                  /></label>
                  <details>
                    <summary>{{ t('Connect product field') }}</summary>
                    <CatalogueSelect
                      v-model="bindingProduct"
                      :options="productOptions"
                      :label="t('Choose a product')"
                    /><CatalogueSelect
                      v-model="bindingField"
                      :options="fields"
                      :label="t('Product field')"
                    /><CatalogueSelect
                      v-if="['price', 'price-label'].includes(bindingField)"
                      v-model="bindingIndex"
                      :options="bindingTiers"
                      :label="t('Price tier')"
                    /><button class="btn-outline" :disabled="!bindingProduct" @click="bind">
                      {{ t('Connect field') }}</button
                    ><button
                      v-if="
                        element.binding &&
                        !(
                          element.binding.kind === 'product' &&
                          ['price', 'price-label', 'currency'].includes(element.binding.field)
                        )
                      "
                      class="btn-ghost"
                      @click="unlink"
                    >
                      {{ t('Use independent text') }}
                    </button>
                  </details></template
                >
                <template v-if="element.type === 'image'"
                  ><CatalogueSelect
                    :model-value="element.fit ?? 'contain'"
                    :options="fits"
                    :label="t('Image fit')"
                    @update:model-value="(value) => editElement({ fit: value as 'cover' | 'contain' })"
                  /><label
                    >{{ t('Image zoom')
                    }}<input
                      :value="element.zoom ?? 1"
                      type="range"
                      min="1"
                      max="5"
                      step=".05"
                      @input="editElement({ zoom: Number(($event.target as HTMLInputElement).value) })" /></label
                  ><label
                    >{{ t('Horizontal crop')
                    }}<input
                      :value="element.cropX ?? 50"
                      type="range"
                      min="0"
                      max="100"
                      @input="editElement({ cropX: Number(($event.target as HTMLInputElement).value) })" /></label
                  ><label
                    >{{ t('Vertical crop')
                    }}<input
                      :value="element.cropY ?? 50"
                      type="range"
                      min="0"
                      max="100"
                      @input="editElement({ cropY: Number(($event.target as HTMLInputElement).value) })" /></label
                  ><CatalogueSelect
                    :model-value="element.productId ?? ''"
                    :options="productOptions"
                    :label="t('Image product')"
                    @update:model-value="(value) => editElement({ productId: value || undefined })"
                  />
                  <div class="image-picker">
                    <button
                      v-for="image in availableImages"
                      :key="image.id"
                      :class="{ chosen: element.src === mediaUrl(image) }"
                      :title="image.alt || image.fileName"
                      @click="replace(image.sha256)"
                    >
                      <img :src="mediaUrl(image)" :alt="image.alt || image.fileName" />
                    </button>
                  </div>
                  <p v-if="!availableImages.length" class="hint">
                    {{ t('No active images in this product group.') }}
                  </p></template
                >
                <label v-if="element.type === 'shape'"
                  >{{ t('Fill color')
                  }}<input
                    type="color"
                    :value="element.background ?? '#d1c3af'"
                    @input="editElement({ background: ($event.target as HTMLInputElement).value })"
                /></label>
                <div class="actions">
                  <button class="btn-outline" @click="duplicateElement">{{ t('Duplicate') }}</button
                  ><button class="btn-ghost" @click="removeElement">{{ t('Remove') }}</button
                  ><button class="btn-ghost" @click="layer(1)">{{ t('Bring forward') }}</button
                  ><button class="btn-ghost" @click="layer(-1)">{{ t('Send backward') }}</button>
                </div>
              </template>
            </aside>
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
.export-bar {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
  margin: 18px 0;
}
.excluded {
  font-size: 13px;
  color: var(--color-muted-foreground);
  margin-bottom: 16px;
}
.designer-grid {
  display: grid;
  grid-template-columns: 230px minmax(0, 1fr) 250px;
  gap: 16px;
}
.page-rail,
.inspector {
  display: grid;
  gap: 12px;
  align-content: start;
  padding: 14px;
  min-width: 0;
}
.page-rail h2,
.inspector h2 {
  font:
    21px Georgia,
    serif;
}
.page-rail ol {
  display: grid;
  gap: 7px;
  max-height: 440px;
  overflow: auto;
}
.page-rail li {
  border: 1px solid var(--color-border);
  border-radius: 7px;
  padding: 8px;
  display: flex;
  gap: 6px;
}
.page-rail li.selected {
  outline: 2px solid var(--color-primary);
}
.page-name {
  text-align: start;
  flex: 1;
  font-size: 12px;
  overflow-wrap: anywhere;
}
.page-name small {
  display: block;
}
.actions {
  display: flex;
  gap: 7px;
  flex-wrap: wrap;
}
.actions button {
  font-size: 12px;
}
.canvas-panel {
  min-width: 0;
}
.canvas-toolbar {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 12px;
}
.canvas-scroll {
  background: var(--color-muted);
  padding: 25px;
  overflow: auto;
  max-height: 1100px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}
.canvas-sheet {
  margin: auto;
  min-width: 300px;
  max-width: none;
}
.hint {
  font-size: 12px;
  line-height: 1.6;
  color: var(--color-muted-foreground);
}
label {
  display: grid;
  gap: 5px;
  font-size: 12px;
}
.check {
  display: flex;
  gap: 7px;
  align-items: center;
}
.numbers {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.numbers input {
  width: 100%;
  min-width: 0;
}
.image-picker {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 6px;
}
.image-picker button {
  border: 1px solid var(--color-border);
  border-radius: 5px;
  padding: 3px;
}
.image-picker button.chosen {
  outline: 2px solid var(--color-primary);
}
.image-picker img {
  height: 70px;
  width: 100%;
  object-fit: contain;
}
details {
  font-size: 12px;
}
details > * + * {
  margin-top: 9px;
}
input[type='color'] {
  height: 30px;
  width: 70px;
}
input[type='range'] {
  width: 100%;
}
@media (max-width: 1250px) {
  .designer-grid {
    grid-template-columns: 210px minmax(0, 1fr);
  }
  .inspector {
    grid-column: 1/-1;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  }
  .inspector h2 {
    grid-column: 1/-1;
  }
}
@media (max-width: 800px) {
  .designer-grid {
    grid-template-columns: 1fr;
  }
  .page-rail {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .page-rail h2,
  .page-rail ol {
    grid-column: 1/-1;
  }
  .page-rail ol {
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  }
  .canvas-scroll {
    padding: 16px;
  }
  .inspector {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
