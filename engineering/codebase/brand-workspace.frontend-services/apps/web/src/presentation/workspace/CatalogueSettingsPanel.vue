<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { useErrorToast } from "@/application/shared/UseFeedback";
import ContentSkeleton from "@/presentation/shared/ContentSkeleton.vue";
import { Button } from "@wow-two-beta/fe/presentation/actions";
import { computed, onScopeDispose, ref, watch } from "vue";
import { useCommerce } from "@/application/workspace/UseCommerce";
import {
  failureMessage,
  type AccessControls,
} from "@/application/workspace/UseAccess";
import type { WorkspaceControls } from "@/application/workspace/UseWorkspace";
import { translateDesignerNotice } from "@/domain/shared/DesignerUiMessages";
import { useUiLocale } from "@/application/shared/UseUiLocale";
import {
  catalogueLocales,
  type CatalogueLocale,
  type CommerceDraft,
} from "@/domain/workspace/Commerce";
import {
  cloneSnapshot,
  type CatalogueBinding,
  type CatalogueCopy,
  type CatalogueSettings,
  type DesignSnapshot,
} from "@/domain/workspace/EditorModels";
import {
  bindCatalogueElement,
  catalogueBrandId,
  catalogueCopy,
  catalogueCopyFields,
  catalogueMedia,
  catalogueMediaGroups,
  catalogueSettings,
  connectCatalogueProducts,
  localizeUnboundTemplate,
  randomizeCatalogue,
  refreshCatalogue,
  replaceCataloguePhoto,
} from "@/domain/workspace/CatalogueEditing";

defineOptions({ name: "CatalogueSettingsPanel" });
const props = defineProps<{
  access: AccessControls;
  workspace: WorkspaceControls;
  snapshot: DesignSnapshot;
  pageId: string;
  selectedId: string | null;
  disabled: boolean;
}>();
const emit = defineEmits<{
  change: [snapshot: DesignSnapshot];
  connect: [snapshot: DesignSnapshot];
}>();
const { t } = useUiLocale();
const api = useCommerce(props.access),
  controller = new AbortController();
const drafts = ref<CommerceDraft[]>([]),
  loading = ref(false),
  errors = ref<string[]>([]),
  warnings = ref<string[]>([]);
const requestError = ref<string | null>(null);
const hasLoaded = ref(false);
useErrorToast(requestError);
const brandId = ref(""),
  bindingProduct = ref(""),
  bindingField = ref<CatalogueBinding["field"]>("name"),
  bindingKind = ref<"product" | "shared">("product"),
  bindingIndex = ref(0);
const settings = computed(() => catalogueSettings(props.snapshot));
const copy = computed(() => catalogueCopy(props.snapshot));
const page = computed(() =>
  props.snapshot.pages.find((item) => item.id === props.pageId),
);
const selected = computed(() =>
  page.value?.elements.find((item) => item.id === props.selectedId),
);
const source = computed(() =>
  drafts.value.find(
    (item) =>
      item.brandId ===
      (page.value
        ? (catalogueBrandId(props.snapshot, page.value) ?? brandId.value)
        : brandId.value),
  ),
);
const photos = computed(
  () =>
    page.value?.elements.filter(
      (item) => item.role === "photo" && item.type === "image",
    ) ?? [],
);
const missingMediaProducts = computed(
  () => photos.value.filter((item) => !item.productId).length,
);
const unlinkedText = computed(
  () =>
    props.snapshot.pages
      .flatMap((item) => item.elements)
      .filter(
        (item) =>
          item.type === "text" && item.role !== "page-number" && !item.binding,
      ).length,
);
const productFields = [
  "name",
  "fabric",
  "decoration",
  "description",
  "details",
  "notes",
  "sizes",
  "price",
  "price-label",
  "currency",
] as const;
const copyLabels: Record<keyof CatalogueCopy, string> = {
  collection: "Collection heading",
  coverNote: "Cover note",
  header: "Page header",
  footer: "Page footer",
  sizes: "Sizes label",
  pieces: "Quantity unit",
  priceOnRequest: "Price on request",
  prices: "Prices heading",
  materials: "Fabric label",
  decoration: "Decoration label",
};
const priceLabels: Record<keyof CatalogueSettings["priceDisplay"], string> = {
  show: "Show price block",
  showCurrency: "Show currency",
  showQuantity: "Show quantity ranges",
  showRetail: "Show retail prices",
  showWholesale: "Show wholesale prices",
};
let sequence = 0;
watch(
  () => props.workspace.brands.value,
  (brands) => {
    if (!brandId.value)
      brandId.value =
        props.snapshot.catalogueSettings?.brandId ?? brands?.[0]?.id ?? "";
  },
  { immediate: true },
);
watch(
  () =>
    [
      brandId.value,
      ...props.snapshot.pages.map(
        (item) => item.catalogueSource?.brandId ?? "",
      ),
    ].join("|"),
  () => {
    void load();
  },
  { immediate: true },
);
watch(selected, (element) => {
  if (element?.binding) {
    bindingKind.value = element.binding.kind;
    bindingField.value = element.binding.field;
    if (element.binding.kind === "product") {
      bindingProduct.value =
        element.binding.productId ?? element.productId ?? "";
      bindingIndex.value = element.binding.index ?? 0;
    }
  } else bindingProduct.value = element?.productId ?? "";
});
onScopeDispose(() => controller.abort());
async function load(): Promise<boolean> {
  const request = ++sequence;
  const organization = props.workspace.organization.value;
  if (!organization) return false;
  loading.value = true;
  requestError.value = null;
  const ids = [
    ...new Set(
      [
        brandId.value,
        props.snapshot.catalogueSettings?.brandId,
        ...props.snapshot.pages.map((item) => item.catalogueSource?.brandId),
      ].filter((id): id is string => !!id),
    ),
  ];
  try {
    const results = await Promise.all(
      ids.map(async (id) => ({
        id,
        result: await api.get(organization.id, id, controller.signal),
      })),
    );
    if (request !== sequence || controller.signal.aborted) return false;
    const retained = new Map(
      drafts.value
        .filter((item) => ids.includes(item.brandId))
        .map((item) => [item.brandId, item]),
    );
    for (const { id, result } of results) {
      if (result.ok) {
        if (result.value) retained.set(id, result.value);
        else retained.delete(id);
      } else requestError.value = failureMessage(result.failure);
    }
    drafts.value = [...retained.values()];
    if (results.every(({ result }) => result.ok)) hasLoaded.value = true;
    return results.every(({ result }) => result.ok);
  } catch {
    if (request === sequence && !controller.signal.aborted)
      requestError.value =
        "The service could not be reached. Your edits have been retained.";
    return false;
  } finally {
    if (request === sequence) loading.value = false;
  }
}
function apply(next: DesignSnapshot, locale = next.locale): void {
  const result = refreshCatalogue(next, drafts.value, locale);
  warnings.value = result.warnings;
  emit("change", result.snapshot);
}
function copyField(key: keyof CatalogueCopy, event: Event): void {
  const next = cloneSnapshot(props.snapshot),
    value = (event.target as HTMLInputElement).value;
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.copyByLocale[next.locale] = {
    ...copy.value,
    [key]: value,
  };
  apply(next);
}
function priceField(
  key: keyof CatalogueSettings["priceDisplay"],
  event: Event,
): void {
  const next = cloneSnapshot(props.snapshot);
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.priceDisplay[key] = (
    event.target as HTMLInputElement
  ).checked;
  apply(next);
}
function language(locale: CatalogueLocale): void {
  const local = localizeUnboundTemplate(props.snapshot, locale),
    result = refreshCatalogue(local.snapshot, drafts.value, locale);
  warnings.value = [...local.warnings, ...result.warnings];
  emit("change", result.snapshot);
}
function group(value: CatalogueSettings["mediaGroup"]): void {
  const next = cloneSnapshot(props.snapshot);
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.mediaGroup = value;
  const result = randomizeCatalogue(
    next,
    drafts.value,
    props.workspace.organization.value!.id,
  );
  warnings.value = result.warnings;
  emit("change", result.snapshot);
}
function randomize(pageId?: string): void {
  const result = randomizeCatalogue(
    props.snapshot,
    drafts.value,
    props.workspace.organization.value!.id,
    pageId,
  );
  warnings.value = result.warnings;
  emit("change", result.snapshot);
}
async function refresh(): Promise<void> {
  const before = JSON.stringify(props.snapshot);
  if (!(await load())) return;
  if (before !== JSON.stringify(props.snapshot)) {
    errors.value = [
      t("The design changed while products loaded. Refresh again."),
    ];
    return;
  }
  apply(props.snapshot);
}
function connect(): void {
  const draft = drafts.value.find((item) => item.brandId === brandId.value);
  if (!draft) return;
  const result = connectCatalogueProducts(props.snapshot, draft);
  warnings.value = result.warnings;
  emit("connect", result.snapshot);
}
function bind(): void {
  if (!selected.value || !page.value) return;
  let binding: CatalogueBinding;
  if (bindingKind.value === "shared")
    binding = {
      kind: "shared",
      field: bindingField.value as keyof CatalogueCopy,
    };
  else
    binding = {
      kind: "product",
      field: bindingField.value as Extract<
        CatalogueBinding,
        { kind: "product" }
      >["field"],
      productId: bindingProduct.value,
      ...(["price", "price-label", "currency"].includes(bindingField.value)
        ? { index: bindingIndex.value }
        : {}),
    };
  try {
    const next = bindCatalogueElement(
      props.snapshot,
      page.value.id,
      selected.value.id,
      binding,
    );
    next.catalogueSettings = catalogueSettings(next);
    next.catalogueSettings.brandId ??= brandId.value;
    apply(next);
  } catch (cause) {
    errors.value = [
      cause instanceof Error
        ? cause.message
        : t("Unable to connect this field."),
    ];
  }
}
function unbind(): void {
  if (page.value && selected.value)
    emit(
      "change",
      bindCatalogueElement(
        props.snapshot,
        page.value.id,
        selected.value.id,
        null,
      ),
    );
}
function linkPhoto(productId: string): void {
  if (!selected.value || selected.value.type !== "image" || !page.value) return;
  const next = cloneSnapshot(props.snapshot),
    target = next.pages
      .find((item) => item.id === page.value!.id)!
      .elements.find((item) => item.id === selected.value!.id)!;
  if (productId) target.productId = productId;
  else delete target.productId;
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.brandId ??= brandId.value;
  emit("change", next);
}
function mediaOptions(productId?: string) {
  const product = source.value?.snapshot.products.find(
    (item) =>
      item.document.id === productId && item.document.status !== "archived",
  );
  return product ? catalogueMedia(product, settings.value.mediaGroup) : [];
}
function photo(elementId: string, hash: string): void {
  if (!page.value) return;
  const frame = page.value.elements.find((item) => item.id === elementId);
  const image = mediaOptions(frame?.productId).find(
    (item) => item.sha256 === hash,
  );
  if (!image) return;
  try {
    emit(
      "change",
      replaceCataloguePhoto(
        props.snapshot,
        page.value.id,
        elementId,
        image,
        drafts.value,
        props.workspace.organization.value!.id,
      ),
    );
  } catch (cause) {
    errors.value = [
      cause instanceof Error
        ? cause.message
        : t("Unable to replace this photo."),
    ];
  }
}
</script>

<template>
  <section
    class="catalogue-settings"
    :aria-label="t('Catalogue content settings')"
  >
    <div class="settings-toolbar"><Button variant="outline" :is-disabled="disabled || loading" :is-loading="loading" @click="load">{{t("Refresh")}}</Button></div>
    <fieldset :disabled="disabled || loading">
      <div class="settings-toolbar">
        <label
          >{{ t("Catalogue language")
          }}<DeskSelect :model-value="snapshot.locale" @update:model-value="language" :label="t('Catalogue language')" :disabled="disabled || loading" :options="[...(catalogueLocales).map(locale => ({ value: locale.code, label: (locale.name) }))]" /></label
        >
        <label
          >{{ t("Photo group for all products")
          }}<DeskSelect :model-value="settings.mediaGroup" @update:model-value="group" :label="t('Photo group for all products')" :disabled="disabled || loading" :options="[...(catalogueMediaGroups).map(group => ({ value: group, label: (t(group)) }))]" /></label
        >
        <button type="button" @click="randomize()">
          {{ t("Randomize whole catalogue") }}
        </button>
        <button type="button" @click="refresh">
          {{ t("Refresh saved product values") }}
        </button>
      </div>
      <p>
        {{ t("Interface language and catalogue language are independent.") }}
      </p>
      <details>
        <summary>{{ t("Shared text and prices") }}</summary>
        <div class="copy-grid">
          <label v-for="key in catalogueCopyFields" :key="key"
            >{{ t(copyLabels[key])
            }}<textarea
              :value="copy[key]"
              maxlength="1000"
              rows="2"
              :lang="snapshot.locale"
              :dir="
                catalogueLocales.find((item) => item.code === snapshot.locale)
                  ?.dir
              "
              @change="copyField(key, $event)"
            />
          </label>
        </div>
        <div class="price-options">
          <label v-for="(label, key) in priceLabels" :key="key"
            ><input
              type="checkbox"
              :checked="settings.priceDisplay[key]"
              @change="priceField(key, $event)"
            />{{ t(label) }}</label
          >
        </div>
        <p>
          {{
            t(
              "Names, materials and prices are edited in Products. Refresh brings saved values into this design.",
            )
          }}
        </p>
      </details>
      <details>
        <summary>{{ t("Page photographs") }}</summary>
        <button
          type="button"
          :disabled="!photos.some((item) => item.productId)"
          @click="randomize(pageId)"
        >
          {{ t("Randomize this page") }}
        </button>
        <div class="photo-choices">
          <label v-for="frame in photos" :key="frame.id"
            >{{ frame.label
            }}<DeskSelect :model-value="frame.src?.split('/').at(-1) ?? null" @update:model-value="photo(frame.id, $event)" :label="frame.label" :disabled="(!frame.productId) || disabled || loading" :options="[({ value: '' as const, label: (t('Choose product image')) }), ...(mediaOptions(frame.productId)).map(image => ({ value: image.sha256, label: (image.alt || image.fileName) }))]" /><span v-if="!frame.productId">{{
              t("Select this frame to connect its product.")
            }}</span></label
          >
        </div>
        <p v-if="missingMediaProducts">
          {{ missingMediaProducts }}
          {{ t("photo frames need a product connection.") }}
        </p>
      </details>
      <details>
        <summary>{{ t("Connect the existing layout to products") }}</summary>
        <label
          >{{ t("Product brand")
          }}<DeskSelect v-model="brandId" :label="t('Product brand')" :disabled="disabled || loading" :options="[({ value: '' as const, label: (t('Choose brand')) }), ...(workspace.brands.value ?? []).map(brand => ({ value: brand.id, label: (brand.name) }))]" /></label
        >
        <button type="button" :disabled="!brandId" @click="connect">
          {{ t("Connect products in a new copy") }}
        </button>
        <p>
          {{
            t(
              "Known template fields are connected by their original IDs. Custom text remains editable.",
            )
          }}
        </p>
        <p>
          {{ unlinkedText }}
          {{
            t(
              "text frames remain unlinked. Review them when changing catalogue language.",
            )
          }}
        </p>
        <template v-if="selected?.type === 'text'">
          <h3>{{ selected.label }}</h3>
          <div class="binding-fields">
            <label
              >{{ t("Text source")
              }}<DeskSelect v-model="bindingKind" @update:model-value="bindingField = $event === 'shared' ? 'header' : 'name'" :label="t('Text source')" :disabled="disabled || loading" :options="[({ value: 'product' as const, label: (t('Product')) }), ({ value: 'shared' as const, label: (t('Shared catalogue text')) })]" /></label
            ><label
              >{{ t("Field")
              }}<DeskSelect v-model="bindingField" :label="t('Field')" :disabled="disabled || loading" :options="[...(bindingKind === 'shared' ? catalogueCopyFields : productFields).map(field => ({ value: field, label: (t(field)) }))]" /></label
            ><label v-if="bindingKind === 'product'"
              >{{ t("Product")
              }}<DeskSelect v-model="bindingProduct" :label="t('Product')" :disabled="disabled || loading" :options="[({ value: '' as const, label: (t('Choose product')) }), ...(source?.snapshot.products.filter( (item) => item.document.status !== 'archived', ) ?? []).map(product => ({ value: product.document.id, label: (product.document.id) }))]" /></label
            ><label
              v-if="
                bindingKind === 'product' &&
                ['price', 'price-label', 'currency'].includes(bindingField)
              "
              >{{ t("Price tier")
              }}<input
                v-model.number="bindingIndex"
                type="number"
                min="0"
                max="49"
            /></label>
          </div>
          <button
            type="button"
            :disabled="bindingKind === 'product' && !bindingProduct"
            @click="bind"
          >
            {{ t("Connect selected text") }}</button
          ><button
            v-if="
              selected.binding &&
              !(
                selected.binding.kind === 'product' &&
                ['price', 'price-label', 'currency'].includes(
                  selected.binding.field,
                )
              )
            "
            type="button"
            @click="unbind"
          >
            {{ t("Make this text independent") }}
          </button>
        </template>
        <label
          v-else-if="selected?.type === 'image' && selected.role === 'photo'"
          >{{ t("Product for selected photo")
          }}<DeskSelect :model-value="selected.productId ?? ''" @update:model-value="linkPhoto" :label="t('Product for selected photo')" :disabled="disabled || loading" :options="[({ value: '' as const, label: (t('Unconnected photo')) }), ...(source?.snapshot.products.filter( (item) => item.document.status !== 'archived', ) ?? []).map(product => ({ value: product.document.id, label: (product.document.id) }))]" /></label
        >
        <p v-else>
          {{
            t(
              "Select a text or photo frame on the canvas to connect it manually.",
            )
          }}
        </p>
      </details>
    </fieldset>
    <ContentSkeleton
      v-if="loading && !hasLoaded"
      variant="rows"
      :cards="2"
      :label="t('Loading saved products…')"
    />
    <p v-else-if="requestError && !source" class="query-label" role="status">{{ t("Saved products unavailable") }}</p>
    <p v-for="error in errors" :key="error" class="editor-alert" role="alert">
      {{ translateDesignerNotice(error, t) }}
    </p>
    <details v-if="warnings.length" open class="catalogue-warnings">
      <summary>{{ warnings.length }} {{ t("items need review") }}</summary>
      <ul>
        <li v-for="warning in warnings" :key="warning">
          {{ translateDesignerNotice(warning, t) }}
        </li>
      </ul>
    </details>
  </section>
</template>

<style scoped>
.catalogue-settings {
  border: 1px solid var(--color-border, #d8d0c2);
  background: var(--color-surface, #fbf9f3);
  padding: 16px;
  border-radius: 10px;
  margin: 16px 0;
  font-size: 13px;
}
.catalogue-settings fieldset {
  border: 0;
  padding: 0;
  display: grid;
  gap: 12px;
}
.settings-toolbar,
.price-options,
.binding-fields {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: end;
}
label {
  display: grid;
  gap: 5px;
  min-width: 150px;
  max-width: 100%;
}
.price-options label {
  display: flex;
  align-items: center;
  gap: 5px;
}
input,
textarea {
  border: 1px solid var(--color-border, #d8d0c2);
  padding: 7px;
  border-radius: 5px;
  background: var(--color-surface, #fffdf8);
  color: inherit;
  font: inherit;
  max-width: 100%;
  box-sizing: border-box;
}
button {
  border: 1px solid var(--color-border, #b6aa96);
  border-radius: 5px;
  padding: 8px 10px;
  cursor: pointer;
  background: var(--color-surface, #fffdf8);
  color: inherit;
  font: inherit;
}
button:disabled,
fieldset:disabled {
  opacity: 0.65;
}
details {
  border-top: 1px solid var(--color-border, #d8d0c2);
  padding-top: 10px;
}
summary {
  cursor: pointer;
  font-weight: 600;
  margin-bottom: 10px;
}
p {
  margin: 5px 0;
  line-height: 1.5;
}
.copy-grid,
.photo-choices {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 12px;
  margin: 12px 0;
}
.catalogue-warnings {
  max-height: 190px;
  overflow: auto;
  color: var(--color-warning, #78551a);
}
ul {
  margin: 0;
  padding-left: 20px;
}
.binding-fields {
  margin: 10px 0;
}
textarea {
  resize: vertical;
}
.photo-choices span {
  font-size: 11px;
}
</style>
