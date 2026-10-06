<script setup lang="ts">
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { requestConfirmation } from '../../application/common/UseFeedback';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { onBeforeRouteLeave, RouterLink, useRoute, useRouter } from 'vue-router';
import { ArrowDown, ArrowLeft, ArrowUp, History, ImagePlus, RotateCcw, Save, Trash2, Upload } from 'lucide-vue-next';
import { Modal, ModalContent } from '@wow-two-beta/ui-vue/presentation/overlays';
import ProductsPage from './ProductsPage.vue';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueStore, useProductEditor } from '../../application/catalogue';
import type { MediaRole, MediaView } from '../../domain/catalogue/CatalogueModels';
import { roleLabels } from '../../domain/catalogue/CatalogueRules';
import { productReviewFieldLabel } from '../../domain/catalogue/ProductReviewFields';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import {
  catalogueLanguages,
  ensureProductLocale,
  translationReview,
  type CatalogueLanguage,
} from '../../domain/catalogue/CatalogueLanguages';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import PriceTiersEditor from './components/PriceTiersEditor.vue';
import StatusBadge from './components/StatusBadge.vue';
import TagInput from './components/TagInput.vue';
import ProductStatusPicker from './components/ProductStatusPicker.vue';
import { formatWhen } from '../../domain/catalogue/UzDates';

/** One product: basics, three-language copy, pricing, images, review notes and history. */
defineOptions({ name: 'ProductEditorPage' });
const route = useRoute();
const router = useRouter();
const store = useCatalogueStore();
const editor = useProductEditor();
const { t, uiLocale } = useUiLocale();
const activeLocale = ref<CatalogueLanguage>(uiLocale.value);
const activeCopy = computed(() =>
  editor.draft.value ? ensureProductLocale(editor.draft.value.locales, activeLocale.value) : null,
);
const reviews = computed(() => (editor.draft.value ? translationReview(editor.draft.value.locales) : []));
const languageOptions = catalogueLanguages.map((language) => ({ value: language.code, label: language.label }));
const languageDirection = computed(
  () => catalogueLanguages.find((language) => language.code === activeLocale.value)?.dir ?? 'ltr',
);
function chooseLanguage(value: string) {
  if (catalogueLanguages.some((language) => language.code === value)) activeLocale.value = value as CatalogueLanguage;
}
function invalidateTranslation() {
  if (activeCopy.value) activeCopy.value.status = 'draft';
}
const uploadRole = ref<MediaRole>('cutout');
const fileInput = ref<HTMLInputElement | null>(null);
const summary = ref('');
const loaded = ref(false);
const roles = Object.keys(roleLabels) as MediaRole[];
const mediaNames: Record<MediaRole, string> = {
  cutout: 'Cutout',
  model: 'Model photo',
  reference: 'Reference',
  photo: 'Product photo',
  detail: 'Detail photo',
  fabric: 'Fabric',
};
const roleOptions = computed(() => roles.map((value) => ({ value, label: t(mediaNames[value]) })));
const groupOptions = computed(() =>
  [
    ['product', 'Product images'],
    ['model', 'Model images'],
    ['detail', 'Detail images'],
    ['fabric', 'Fabric'],
  ].map(([value, label]) => ({ value: value!, label: t(label!) })),
);
const productId = computed(() => (typeof route.params.id === 'string' ? route.params.id : null));
const mediaById = computed(
  () => new Map((editor.detail.value?.media ?? []).map((m) => [m.media.id, m] as [string, MediaView])),
);
const detached = computed(() =>
  (editor.detail.value?.media ?? []).filter((m) => !editor.draft.value?.media?.some((x) => x.id === m.media.id)),
);
const openIssues = computed(
  () =>
    (editor.draft.value?.issues.filter((i) => i.status === 'open').length ?? 0) +
    reviews.value.filter((review) => !review.approved).length,
);
const activeImages = computed(() => editor.draft.value?.media?.filter((media) => media.isActive !== false).length ?? 0);
function changeMedia(index: number, remove = false) {
  const media = editor.draft.value?.media;
  const item = media?.[index];
  if (!item || !media) return;
  if (item.isActive !== false && activeImages.value <= 1 && editor.draft.value?.status === 'active') {
    store.fail(t('An active product needs at least one active image.'));
    return;
  }
  if (remove) media.splice(index, 1);
  else item.isActive = item.isActive === false;
}

const { refresh, refreshing } = useRefresh(async () => {
  if (productId.value) await editor.load(productId.value);
});
onMounted(async () => {
  await store.loadSettings();
  if (productId.value) await editor.load(productId.value);
  else
    editor.startNew(
      typeof route.query.type === 'string' ? route.query.type : (store.garmentTypes.value[0]?.key ?? 'nimcha'),
    );
  loaded.value = true;
});
function warnUnsaved(event: BeforeUnloadEvent) {
  if (editor.dirty.value) event.preventDefault();
}
window.addEventListener('beforeunload', warnUnsaved);
onBeforeUnmount(() => window.removeEventListener('beforeunload', warnUnsaved));
onBeforeRouteLeave(
  async () =>
    !editor.saving.value &&
    !editor.uploading.value &&
    (!editor.dirty.value ||
      (await requestConfirmation(t('Discard unsaved changes?'), { confirmLabel: t('Discard changes') }))),
);

async function save() {
  const wasNew = editor.isNew.value;
  const id = await editor.save(summary.value || undefined);
  if (id) summary.value = '';
  if (id && wasNew) await router.replace(`/products/${id}`);
}
function formatHistoryWhen(value: string) {
  if (uiLocale.value === 'uz') return formatWhen(value);
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return '—';
  return new Intl.DateTimeFormat(uiLocale.value === 'ru' ? 'ru-RU' : 'en-GB', {
    timeZone: 'Asia/Tashkent',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(date);
}
function move(index: number, delta: number) {
  const list = editor.draft.value?.media;
  if (!list) return;
  const target = index + delta;
  if (target < 0 || target >= list.length) return;
  const [item] = list.splice(index, 1);
  if (item) list.splice(target, 0, item);
}
function reattach(view: MediaView) {
  editor.draft.value?.media?.push({
    id: view.media.id,
    role: view.media.role,
    alt: view.media.alt,
    isActive: view.media.isActive ?? true,
    group: view.media.group,
  });
}
function addIssue() {
  editor.draft.value?.issues.push({ id: '', field: 'general', status: 'open', question: '' });
}
async function onFiles(event: Event) {
  const files = (event.target as HTMLInputElement).files;
  if (files?.length) await editor.upload(files, uploadRole.value);
  if (fileInput.value) fileInput.value.value = '';
}
</script>

<template>
  <div aria-hidden="true" inert><ProductsPage /></div>
  <Modal
    :open="true"
    :dismiss-on-escape="!editor.saving.value && !editor.uploading.value"
    :dismiss-on-outside-click="!editor.saving.value && !editor.uploading.value"
    @update:open="
      (value) => {
        if (!value) void router.push('/');
      }
    "
    ><ModalContent
      :is-blurred="true"
      padding="none"
      v-bind="{ 'aria-label': t('Product editor') }"
      class="catalogue-edit-modal flex h-[94dvh] w-[min(90rem,calc(100vw-3rem))] max-h-[94dvh] flex-col overflow-hidden bg-background text-foreground"
    >
      <section v-if="editor.draft.value" class="flex min-h-0 flex-1 flex-col">
        <fieldset
          :disabled="editor.saving.value || editor.uploading.value"
          class="flex min-h-0 min-w-0 flex-1 flex-col border-0 p-0"
        >
          <div class="catalogue-product-body min-h-0 flex-1 overflow-y-auto p-5 sm:p-6">
            <RouterLink to="/" class="btn-ghost -ml-3 mb-3"
              ><Icon :icon="ArrowLeft" :size="15" />{{ t('Products') }}</RouterLink
            >
            <div class="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p class="text-xs uppercase tracking-[0.2em] text-muted-foreground">
                  {{ store.garmentName(editor.draft.value.garmentType, uiLocale) }}
                </p>
                <h1 class="mt-1 font-editorial text-4xl tracking-tight">
                  {{
                    editor.draft.value.locales[uiLocale].name ||
                    (editor.isNew.value ? t('New product') : editor.detail.value?.document.id)
                  }}
                </h1>
                <p class="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                  <span v-if="editor.detail.value"
                    >{{ editor.detail.value.document.id }} · {{ t('Revision') }}
                    {{ editor.detail.value.revision }}</span
                  >
                  <StatusBadge :status="editor.draft.value.status" />
                  <span v-if="openIssues" class="chip bg-warning-soft text-warning-soft-foreground">{{
                    t('{count} open questions', { count: openIssues })
                  }}</span>
                  <span
                    v-for="c in editor.detail.value?.collectionIds ?? []"
                    :key="c"
                    class="chip bg-secondary text-secondary-foreground"
                    >{{ c }}</span
                  >
                </p>
              </div>
            </div>

            <section class="panel mt-5" :aria-label="t('Translation review')">
              <div class="flex flex-wrap items-center gap-2">
                <span class="mr-2 text-sm font-semibold">{{ t('Translation review') }}</span>
                <label
                  v-for="review in reviews"
                  :key="review.code"
                  :class="[
                    'chip gap-2 border px-3 py-2',
                    review.approved
                      ? 'border-success/30 bg-success-soft text-success-soft-foreground'
                      : 'border-warning/30 bg-warning-soft text-warning-soft-foreground',
                  ]"
                >
                  <input
                    type="checkbox"
                    :checked="review.approved"
                    :disabled="!ensureProductLocale(editor.draft.value.locales, review.code).name?.trim()"
                    @change="
                      ensureProductLocale(editor.draft.value.locales, review.code).status = (
                        $event.target as HTMLInputElement
                      ).checked
                        ? 'approved'
                        : 'draft'
                    "
                  />
                  <button type="button" @click="activeLocale = review.code">
                    {{ review.label }} · {{ review.approved ? t('Confirmed') : t('Needs review') }}
                  </button>
                </label>
              </div>
            </section>

            <ul
              v-if="editor.problems.value.length"
              class="mt-5 space-y-1 rounded-xl border border-warning/30 bg-warning-soft px-5 py-3 text-sm text-warning-soft-foreground"
            >
              <li v-for="p in editor.problems.value" :key="p">{{ p }}</li>
            </ul>

            <div class="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(340px,1fr)]">
              <div class="space-y-6">
                <section class="panel" aria-labelledby="basics-heading">
                  <h2 id="basics-heading" class="font-editorial text-2xl">{{ t('Basics') }}</h2>
                  <div class="mt-4 grid gap-4 sm:grid-cols-2">
                    <div>
                      <label class="label" for="garment">{{ t('Garment type') }}</label>
                      <CatalogueSelect
                        id="garment"
                        v-model="editor.draft.value.garmentType"
                        :disabled="!editor.isNew.value"
                        :label="t('Garment type')"
                        :options="store.garmentTypes.value.map((g) => ({ value: g.key, label: g[uiLocale] }))"
                      />
                    </div>
                    <div>
                      <span class="label">{{ t('Status') }}</span>
                      <ProductStatusPicker v-model="editor.draft.value.status" />
                    </div>
                    <div v-if="editor.isNew.value">
                      <label class="label" for="variant">{{ t('Variant code (blank = automatic)') }}</label>
                      <input id="variant" v-model="editor.draft.value.variantCode" class="field" placeholder="v17" />
                    </div>
                    <div>
                      <label class="label" for="sort">{{ t('Display order') }}</label>
                      <input id="sort" v-model.number="editor.draft.value.sortOrder" type="number" class="field" />
                    </div>
                    <div>
                      <label class="label" for="design">{{ t('Design family') }}</label>
                      <input
                        id="design"
                        v-model="editor.draft.value.designId"
                        class="field"
                        :placeholder="t('Optional')"
                      />
                    </div>
                  </div>
                  <div class="mt-4 grid gap-4 sm:grid-cols-2">
                    <TagInput
                      v-model="editor.draft.value.sizes"
                      :label="t('Sizes')"
                      :suggestions="store.settings.value?.sizePreset"
                      :ordered-sizes="true"
                    />
                    <TagInput
                      v-model="editor.draft.value.tags"
                      :label="t('Tags')"
                      :placeholder="t('spring, wedding, new…')"
                    />
                    <TagInput v-model="editor.draft.value.fabricCodes" :label="t('Fabric codes')" />
                    <TagInput v-model="editor.draft.value.decorationCodes" :label="t('Decoration codes')" />
                  </div>
                  <div class="mt-4">
                    <label class="label" for="notes">{{ t('Internal note (not printed)') }}</label>
                    <textarea id="notes" v-model="editor.draft.value.notes" rows="2" class="field" />
                  </div>
                </section>

                <section class="panel" aria-labelledby="copy-heading">
                  <div class="flex flex-wrap items-center justify-between gap-3">
                    <h2 id="copy-heading" class="font-editorial text-2xl">{{ t('Product text') }}</h2>
                    <CatalogueSelect
                      :model-value="activeLocale"
                      :options="languageOptions"
                      :label="t('Content language')"
                      class="w-52"
                      @update:model-value="chooseLanguage"
                    />
                  </div>
                  <div v-if="activeCopy" class="mt-4 space-y-4">
                    <label class="flex items-center gap-2 rounded-xl border border-border bg-background p-3 text-sm">
                      <input
                        type="checkbox"
                        :checked="activeCopy.status === 'approved'"
                        :disabled="!activeCopy.name?.trim()"
                        @change="activeCopy.status = ($event.target as HTMLInputElement).checked ? 'approved' : 'draft'"
                      />
                      {{ t('I confirm the translation') }} ·
                      {{ catalogueLanguages.find((language) => language.code === activeLocale)?.label }}
                    </label>
                    <p class="text-xs text-muted-foreground">
                      {{ t('Unconfirmed copy is excluded from this language’s web catalogue and PDF.') }}
                    </p>
                    <div :dir="languageDirection" class="grid gap-4">
                      <label
                        ><span class="label">{{ t('Name') }}</span
                        ><input v-model="activeCopy.name" class="field" @input="invalidateTranslation"
                      /></label>
                      <div class="grid gap-4 sm:grid-cols-2">
                        <label
                          ><span class="label">{{ t('Fabric') }}</span
                          ><input v-model="activeCopy.fabric" class="field" @input="invalidateTranslation"
                        /></label>
                        <label
                          ><span class="label">{{ t('Decoration') }}</span
                          ><input v-model="activeCopy.decoration" class="field" @input="invalidateTranslation"
                        /></label>
                      </div>
                      <label
                        ><span class="label">{{ t('Description') }}</span
                        ><textarea
                          v-model="activeCopy.description"
                          rows="4"
                          class="field"
                          @input="invalidateTranslation"
                        />
                      </label>
                    </div>
                  </div>
                  <details
                    v-if="editor.detail.value?.document.sourceVerbatim"
                    class="mt-5 rounded-xl bg-muted p-4 text-sm"
                  >
                    <summary class="cursor-pointer font-semibold">{{ t('Original source text') }}</summary>
                    <p class="mt-2 whitespace-pre-line">{{ editor.detail.value.document.sourceVerbatim }}</p>
                  </details>
                </section>

                <section class="panel" aria-labelledby="price-heading">
                  <h2 id="price-heading" class="mb-4 font-editorial text-2xl">{{ t('Price') }}</h2>
                  <PriceTiersEditor v-model="editor.draft.value.pricing" />
                </section>

                <section class="panel" aria-labelledby="issues-heading">
                  <div class="flex items-center justify-between">
                    <h2 id="issues-heading" class="font-editorial text-2xl">{{ t('Unresolved product facts') }}</h2>
                    <button class="btn-outline" @click="addIssue">{{ t('Add question') }}</button>
                  </div>
                  <p class="mt-3 text-sm text-muted-foreground">
                    {{ t('Internal fact checks, not customer FAQs. Open questions block publication.') }}
                  </p>
                  <p v-if="!editor.draft.value.issues.length" class="mt-3 text-sm text-muted-foreground">
                    {{ t('No questions.') }}
                  </p>
                  <ul class="mt-4 space-y-3">
                    <li
                      v-for="(issue, index) in editor.draft.value.issues"
                      :key="issue.id || index"
                      class="rounded-xl border border-border p-3"
                    >
                      <div class="flex flex-wrap items-center gap-2">
                        <span class="text-sm font-semibold">{{ t(productReviewFieldLabel(issue.field)) }}</span>
                        <button
                          :class="[
                            'chip',
                            issue.status === 'open'
                              ? 'bg-warning-soft text-warning-soft-foreground'
                              : 'bg-success-soft text-success-soft-foreground',
                          ]"
                          @click="issue.status = issue.status === 'open' ? 'resolved' : 'open'"
                        >
                          {{ issue.status === 'open' ? t('Open') : t('Resolved') }}
                        </button>
                        <button
                          class="btn-ghost ml-auto"
                          :aria-label="t('Delete question')"
                          @click="editor.draft.value.issues.splice(index, 1)"
                        >
                          <Icon :icon="Trash2" :size="14" />
                        </button>
                      </div>
                      <textarea v-model="issue.question" rows="2" class="field mt-2" :aria-label="t('Question text')" />
                    </li>
                  </ul>
                </section>
              </div>

              <div class="space-y-6">
                <section class="panel" aria-labelledby="images-heading">
                  <div class="flex items-center justify-between gap-3">
                    <h2 id="images-heading" class="font-editorial text-2xl">{{ t('Images') }}</h2>
                    <div class="flex items-center gap-2">
                      <CatalogueSelect
                        v-model="uploadRole"
                        :label="t('Image type')"
                        class="w-32"
                        :options="roleOptions"
                      />
                      <button
                        class="btn-outline"
                        :disabled="!editor.detail.value || editor.uploading.value"
                        @click="fileInput?.click()"
                      >
                        <Icon :icon="editor.uploading.value ? Upload : ImagePlus" :size="15" />{{
                          editor.uploading.value ? t('Uploading…') : t('Add')
                        }}
                      </button>
                      <input
                        ref="fileInput"
                        type="file"
                        accept="image/png,image/jpeg,image/webp"
                        multiple
                        class="hidden"
                        @change="onFiles"
                      />
                    </div>
                  </div>
                  <p v-if="!editor.detail.value" class="mt-3 text-sm text-muted-foreground">
                    {{ t('Save the product before adding images.') }}
                  </p>
                  <ul class="mt-4 grid grid-cols-[repeat(auto-fit,minmax(min(100%,14rem),1fr))] gap-4">
                    <li
                      v-for="(item, index) in editor.draft.value.media ?? []"
                      :key="item.id"
                      class="min-w-0 rounded-xl border border-border bg-background p-3"
                    >
                      <div
                        class="flex aspect-[3/4] items-center justify-center rounded-lg bg-[radial-gradient(circle_at_50%_35%,#fffdf8,#ece7dc)]"
                      >
                        <img
                          v-if="mediaById.get(item.id)"
                          :src="mediaById.get(item.id)!.previewUrl ?? mediaById.get(item.id)!.url"
                          :alt="item.alt ?? item.role"
                          class="h-full w-full object-contain p-2"
                          loading="lazy"
                        />
                      </div>
                      <div class="mt-3 flex flex-wrap items-center gap-1">
                        <CatalogueSelect
                          v-model="item.role"
                          :label="t('Image type')"
                          :options="roleOptions"
                          class="min-w-0 flex-1"
                        />
                        <button
                          class="btn-ghost px-1.5"
                          :aria-label="t('Move earlier')"
                          :disabled="index === 0"
                          @click="move(index, -1)"
                        >
                          <Icon :icon="ArrowUp" :size="13" />
                        </button>
                        <button
                          class="btn-ghost px-1.5"
                          :aria-label="t('Move later')"
                          :disabled="index === (editor.draft.value.media?.length ?? 0) - 1"
                          @click="move(index, 1)"
                        >
                          <Icon :icon="ArrowDown" :size="13" />
                        </button>
                        <button
                          class="btn-ghost px-1.5 text-destructive"
                          :aria-label="t('Remove image')"
                          @click="changeMedia(index, true)"
                        >
                          <Icon :icon="Trash2" :size="13" />
                        </button>
                      </div>
                      <label class="mt-2 flex items-center gap-2 text-xs"
                        ><input
                          type="checkbox"
                          :checked="item.isActive !== false"
                          :disabled="
                            item.isActive !== false && activeImages <= 1 && editor.draft.value.status === 'active'
                          "
                          @change="changeMedia(index)"
                        />{{ t('Active image') }}</label
                      >
                      <CatalogueSelect
                        :model-value="
                          item.group ?? (['model', 'detail', 'fabric'].includes(item.role) ? item.role : 'product')
                        "
                        :options="groupOptions"
                        :label="t('Image group')"
                        class="mt-2"
                        @update:model-value="item.group = $event"
                      />
                      <label class="mt-3 block">
                        <span class="label">{{ t('Alt text') }}</span>
                        <textarea
                          v-model="item.alt"
                          rows="2"
                          class="field resize-y"
                          :placeholder="t('Describe the product and view shown')"
                        />
                      </label>
                      <p
                        v-if="index === 0"
                        class="mt-1 text-[10px] font-semibold uppercase tracking-wider text-primary-soft-foreground"
                      >
                        {{ t('Primary image') }}
                      </p>
                    </li>
                  </ul>
                  <div
                    v-if="detached.length"
                    class="mt-4 rounded-xl border border-dashed border-border p-3 text-xs text-muted-foreground"
                  >
                    <p>{{ t('Removed images leave the product list when saved; their files remain stored.') }}</p>
                    <div class="mt-2 flex flex-wrap gap-2">
                      <button
                        v-for="view in detached"
                        :key="view.media.id"
                        class="chip bg-muted"
                        @click="reattach(view)"
                      >
                        ↺ {{ view.media.fileName }}
                      </button>
                    </div>
                  </div>
                </section>

                <section v-if="editor.history.value.length" class="panel" aria-labelledby="history-heading">
                  <h2 id="history-heading" class="flex items-center gap-2 font-editorial text-2xl">
                    <Icon :icon="History" :size="18" />{{ t('History') }}
                  </h2>
                  <ol class="mt-4 max-h-96 space-y-2 overflow-y-auto pr-1 text-sm">
                    <li
                      v-for="entry in editor.history.value"
                      :key="entry.revision"
                      class="flex items-start justify-between gap-3 rounded-lg border border-border px-3 py-2"
                    >
                      <div>
                        <p class="font-semibold">v{{ entry.revision }} · {{ entry.summary }}</p>
                        <p class="text-xs text-muted-foreground">{{ formatHistoryWhen(entry.savedAt) }}</p>
                      </div>
                      <button
                        v-if="entry.revision !== editor.detail.value?.revision"
                        class="btn-ghost shrink-0 px-2 text-xs"
                        :disabled="editor.dirty.value"
                        :title="
                          editor.dirty.value ? t('Save or cancel your changes first.') : t('Restore this revision')
                        "
                        @click="editor.restore(entry.revision)"
                      >
                        <Icon :icon="RotateCcw" :size="13" />{{ t('Restore') }}
                      </button>
                    </li>
                  </ol>
                </section>

                <section v-if="editor.detail.value?.document.source" class="panel text-xs text-muted-foreground">
                  <p class="label">{{ t('Source') }}</p>
                  <p class="break-all">{{ editor.detail.value.document.source.path }}</p>
                  <p class="mt-1 break-all font-mono">sha256 {{ editor.detail.value.document.source.sha256 }}</p>
                </section>
              </div>
            </div>
          </div>
          <footer class="catalogue-product-footer no-print shrink-0 border-t border-border bg-card">
            <div
              class="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 px-5 py-4 sm:flex sm:flex-wrap sm:px-6"
            >
              <span :class="['text-sm', editor.dirty.value ? 'font-semibold text-warning' : 'text-muted-foreground']">
                {{ editor.dirty.value ? t('Unsaved changes') : t('All changes saved') }}
              </span>
              <input
                v-model="summary"
                class="field col-span-3 row-start-1 min-w-0 sm:col-auto sm:row-auto sm:max-w-xs sm:flex-1"
                :placeholder="t('Change note (optional)')"
                :aria-label="t('Change note')"
              />
              <button
                class="btn-ghost ml-auto"
                :disabled="!editor.dirty.value || editor.isNew.value || editor.saving.value || editor.uploading.value"
                @click="editor.reset()"
              >
                {{ t('Cancel') }}
              </button>
              <button
                class="btn-primary"
                :disabled="
                  (!editor.dirty.value && !editor.isNew.value) || editor.saving.value || editor.uploading.value
                "
                :aria-busy="editor.saving.value"
                @click="save"
              >
                <Icon :icon="Save" :size="15" />{{ editor.saving.value ? t('Saving…') : t('Save') }}
              </button>
            </div>
          </footer>
        </fieldset>
      </section>
      <div v-else class="overflow-y-auto p-5 sm:p-6">
        <div class="flex justify-end"><RefreshButton :busy="!loaded || refreshing" @refresh="refresh" /></div>
        <ContentSkeleton v-if="!loaded || refreshing" :label="t('Loading product')" variant="product-form" />
        <ResultEmpty v-else :label="t('Content unavailable')" />
      </div> </ModalContent
  ></Modal>
</template>
