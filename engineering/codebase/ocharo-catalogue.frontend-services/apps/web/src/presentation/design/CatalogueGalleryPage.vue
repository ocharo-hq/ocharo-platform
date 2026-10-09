<script setup lang="ts">
import { useSsoSession } from '../../application/common/UseSsoSession';
const { canEdit: ssoCanEdit } = useSsoSession();
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/ui-vue/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, onMounted, onScopeDispose, ref } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/ui-vue/presentation/overlays';
import { designApi, describeDesignFailure } from '../../application/design/UseCatalogueDocument';
import { notifyError, requestConfirmation } from '../../application/common/UseFeedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { catalogueLanguages, type CatalogueLanguage } from '../../domain/catalogue/CatalogueLanguages';
import { catalogueSlug, createCatalogue, emptyMetadata } from '../../domain/design/CatalogueDesign';
import {
  cloneSnapshot,
  validCatalogueSlug,
  type CatalogueMetadata,
  type DesignDocument,
  type DesignSummary,
} from '../../domain/design/EditorModels';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import CatalogueSelect from '../common/CatalogueSelect.vue';
defineOptions({ name: 'CatalogueGalleryPage' });
const router = useRouter(),
  { t, uiLocale } = useUiLocale(),
  controller = new AbortController();
const documents = ref<DesignSummary[]>([]),
  loading = ref(true),
  loaded = ref(false),
  busy = ref(false),
  query = ref(''),
  loadFailed = ref(false),
  pendingAction = ref('');
const selected = ref<DesignDocument | null>(null),
  name = ref(''),
  locale = ref<CatalogueLanguage>('uz'),
  metadata = ref<CatalogueMetadata>(emptyMetadata()),
  dialogOpen = ref(false),
  baseline = ref(''),
  slugError = ref('');
let loadGeneration = 0;
const { refresh, refreshing } = useRefresh(load);
const resultBusy = computed(() => loading.value || refreshing.value);
const languageOptions = catalogueLanguages.map((item) => ({ value: item.code, label: item.label }));
const serialize = () => JSON.stringify({ name: name.value, locale: locale.value, metadata: metadata.value });
const dirty = computed(() => dialogOpen.value && serialize() !== baseline.value),
  locked = computed(() => busy.value);
const visible = computed(() =>
  documents.value.filter(
    (item) =>
      !item.archived && `${item.title} ${item.catalogue?.slug ?? ''}`.toLowerCase().includes(query.value.toLowerCase()),
  ),
);
function fail(cause: unknown) {
  const message = describeDesignFailure(cause, t, 'Unable to load catalogues.');
  notifyError(message);
  return message;
}
async function load() {
  const generation = ++loadGeneration;
  loading.value = true;
  loadFailed.value = false;
  try {
    const next = await designApi.list(controller.signal);
    if (generation !== loadGeneration || controller.signal.aborted) return;
    documents.value = next;
    loaded.value = true;
  } catch (cause) {
    if (generation === loadGeneration && !controller.signal.aborted) {
      loadFailed.value = true;
      fail(cause);
    }
  } finally {
    if (generation === loadGeneration) loading.value = false;
  }
}
async function close() {
  if (busy.value) return;
  if (
    dirty.value &&
    !(await requestConfirmation(t('Discard unsaved catalogue settings?'), { confirmLabel: t('Discard changes') }))
  )
    return;
  dialogOpen.value = false;
}
async function edit(item?: DesignSummary) {
  if (busy.value) return;
  busy.value = true;
  try {
    selected.value = item ? await designApi.get(item.id) : null;
    name.value = selected.value?.title ?? '';
    locale.value = selected.value?.snapshot.locale ?? 'uz';
    metadata.value = selected.value?.snapshot.catalogue
      ? (JSON.parse(JSON.stringify(selected.value.snapshot.catalogue)) as CatalogueMetadata)
      : emptyMetadata();
    slugError.value = '';
    baseline.value = serialize();
    dialogOpen.value = true;
  } catch (cause) {
    fail(cause);
  } finally {
    busy.value = false;
    pendingAction.value = '';
  }
}
function nameChanged() {
  if (!selected.value) metadata.value.slug = catalogueSlug(name.value);
}
const formats = (item: DesignSummary) => item.catalogue?.outputs ?? { web: false, pdf: true };
function open(id: string, mode: 'web' | 'pdf') {
  void router.push(`/catalogues/${id}/${mode}`);
}
async function save() {
  if (busy.value) return;
  slugError.value = validCatalogueSlug(metadata.value.slug)
    ? ''
    : t(
        'Use 1–100 lowercase letters or numbers, separated by single hyphens. This slug must not be a reserved site path.',
      );
  if (slugError.value) return;
  if (!name.value.trim() || (!metadata.value.outputs.web && !metadata.value.outputs.pdf)) {
    notifyError(t('Choose a name and at least one output.'));
    return;
  }
  const snapshot = selected.value
    ? cloneSnapshot(selected.value.snapshot)
    : createCatalogue(name.value.trim(), locale.value, metadata.value);
  snapshot.catalogue = JSON.parse(JSON.stringify(metadata.value)) as CatalogueMetadata;
  if (metadata.value.outputs.pdf && !snapshot.pages.length)
    snapshot.pages = createCatalogue(name.value.trim(), snapshot.locale, metadata.value).pages;
  busy.value = true;
  pendingAction.value = 'save';
  try {
    await designApi.save(selected.value?.id ?? null, name.value.trim(), snapshot, selected.value?.revision);
    dialogOpen.value = false;
    await load();
  } catch (cause) {
    fail(cause);
  } finally {
    busy.value = false;
    pendingAction.value = '';
  }
}
async function duplicate(item: DesignSummary) {
  if (busy.value) return;
  busy.value = true;
  pendingAction.value = `duplicate:${item.id}`;
  try {
    await designApi.duplicate(item.id);
    await load();
  } catch (cause) {
    fail(cause);
  } finally {
    busy.value = false;
    pendingAction.value = '';
  }
}
async function activate(item: DesignSummary) {
  if (busy.value) return;
  busy.value = true;
  pendingAction.value = `activate:${item.id}`;
  try {
    const detail = await designApi.get(item.id);
    const snapshot = cloneSnapshot(detail.snapshot);
    snapshot.catalogue!.isActive = !snapshot.catalogue!.isActive;
    await designApi.save(item.id, detail.title, snapshot, detail.revision);
    await load();
  } catch (cause) {
    fail(cause);
  } finally {
    busy.value = false;
    pendingAction.value = '';
  }
}
async function remove(item: DesignSummary) {
  if (
    busy.value ||
    !(await requestConfirmation(
      t('Remove “{name}” from the gallery? Public links will stop working. Saved history is retained.', {
        name: item.title,
      }),
      { confirmLabel: t('Remove catalogue') },
    ))
  )
    return;
  busy.value = true;
  pendingAction.value = `remove:${item.id}`;
  try {
    await designApi.archive(item.id, item.revision);
    await load();
  } catch (cause) {
    fail(cause);
  } finally {
    busy.value = false;
    pendingAction.value = '';
  }
}
onBeforeRouteLeave(
  async () =>
    !busy.value &&
    (!dirty.value ||
      (await requestConfirmation(t('Discard unsaved catalogue settings?'), { confirmLabel: t('Discard changes') }))),
);
function beforeUnload(event: BeforeUnloadEvent) {
  if (dirty.value) {
    event.preventDefault();
    event.returnValue = '';
  }
}
onMounted(() => {
  void load();
  window.addEventListener('beforeunload', beforeUnload);
});
onScopeDispose(() => {
  controller.abort();
  window.removeEventListener('beforeunload', beforeUnload);
});
</script>
<template>
  <section class="catalogue-gallery">
    <header>
      <div>
        <p class="eyebrow">{{ t('Your catalogues') }}</p>
        <h1>{{ t('A collection, in every format.') }}</h1>
        <p>{{ t('Create one catalogue and design its website, PDF, or both.') }}</p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <RefreshButton :busy="resultBusy" :disabled="locked" @refresh="refresh" /><button
          :disabled="!ssoCanEdit || locked"
          @click="edit()"
        >
          {{ t('New catalogue') }}
        </button>
      </div>
    </header>
    <label class="search"
      >{{ t('Find a catalogue') }}<input v-model="query" type="search" :placeholder="t('Name or slug')"
    /></label>
    <div class="catalogue-result-region">
      <ContentSkeleton v-if="resultBusy && !loaded" :cards="3" variant="catalogue" :label="t('Loading catalogues')" />
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading catalogues')">
        <ResultEmpty
          v-if="!visible.length"
          :label="
            loaded
              ? query
                ? t('No catalogues match this search.')
                : t('Create your first catalogue.')
              : t('Content unavailable')
          "
        />
        <div v-else class="catalogue-grid">
          <article v-for="item in visible" :key="item.id">
            <div class="catalogue-cover">
              <span>Ocharo</span><strong>{{ item.title }}</strong
              ><small>{{ item.locale?.toUpperCase() ?? 'PDF' }}</small>
            </div>
            <div class="card-body">
              <div class="card-heading">
                <h2>{{ item.title }}</h2>
                <SkeletonStateSlot class="catalogue-value"
                  ><span>{{
                    (item.catalogue?.isActive ?? true) ? t('Active') : t('Inactive')
                  }}</span></SkeletonStateSlot
                >
              </div>
              <p v-if="item.catalogue?.slug" class="slug">/{{ item.catalogue.slug }}</p>
              <SkeletonStateSlot class="catalogue-value" :is-block="true"
                ><p>{{ item.catalogue?.description }}</p></SkeletonStateSlot
              >
              <small
                >{{ t('Last edited') }} ·
                <SkeletonStateSlot class="catalogue-value">{{
                  new Date(item.updatedAtUtc).toLocaleString(uiLocale)
                }}</SkeletonStateSlot></small
              >
              <div class="designer-actions">
                <button v-if="formats(item).web" @click="open(item.id, 'web')">{{ t('Web designer') }}</button
                ><button v-if="formats(item).pdf" @click="open(item.id, 'pdf')">{{ t('PDF designer') }}</button>
              </div>
              <div class="card-actions">
                <button :disabled="locked" @click="edit(item)">{{ t('Settings') }}</button
                ><button
                  :disabled="!ssoCanEdit || locked"
                  :aria-busy="pendingAction === `duplicate:${item.id}`"
                  @click="duplicate(item)"
                >
                  {{ pendingAction === `duplicate:${item.id}` ? t('Working…') : t('Duplicate') }}</button
                ><button
                  :disabled="!ssoCanEdit || locked"
                  :aria-busy="pendingAction === `activate:${item.id}`"
                  @click="activate(item)"
                >
                  {{
                    pendingAction === `activate:${item.id}`
                      ? t('Working…')
                      : (item.catalogue?.isActive ?? true)
                        ? t('Deactivate')
                        : t('Activate')
                  }}</button
                ><button
                  :disabled="!ssoCanEdit || locked"
                  :aria-busy="pendingAction === `remove:${item.id}`"
                  @click="remove(item)"
                >
                  {{ pendingAction === `remove:${item.id}` ? t('Working…') : t('Remove') }}
                </button>
              </div>
            </div>
          </article>
        </div>
      </SkeletonStateGroup>
    </div>
    <p class="catalogue-note">
      {{
        t(
          'Changes affect the working catalogue. Published editions change only after explicit preparation and publication.',
        )
      }}
    </p>
    <Modal
      :open="dialogOpen"
      :dismiss-on-escape="!busy"
      :dismiss-on-outside-click="!busy"
      @update:open="
        (value) => {
          if (!value) void close();
        }
      "
      ><ModalContent v-if="dialogOpen" :is-blurred="true" class="catalogue-settings-modal"
        ><form @submit.prevent="save">
          <header>
            <ModalTitle>{{ selected ? t('Catalogue settings') : t('New catalogue') }}</ModalTitle
            ><button type="button" :disabled="busy" @click="close()">{{ t('Close') }}</button>
          </header>
          <fieldset :disabled="!ssoCanEdit || busy">
            <label>{{ t('Name') }}<input v-model="name" maxlength="160" required @input="nameChanged" /></label
            ><label
              >{{ t('Slug')
              }}<input
                v-model="metadata.slug"
                maxlength="100"
                pattern="[a-z0-9]+(-[a-z0-9]+)*"
                :aria-invalid="!!slugError"
                :aria-describedby="slugError ? 'slug-error' : undefined"
                required
              /><span v-if="slugError" id="slug-error" role="alert">{{ slugError }}</span></label
            ><label>{{ t('Description') }}<textarea v-model="metadata.description" maxlength="2000" rows="3" /></label>
            <label v-if="!selected"
              >{{ t('Catalogue language')
              }}<CatalogueSelect
                :model-value="locale"
                :options="languageOptions"
                :label="t('Catalogue language')"
                @update:model-value="(value) => (locale = value as CatalogueLanguage)"
            /></label>
            <div class="output-options">
              <label><input v-model="metadata.outputs.web" type="checkbox" />{{ t('Web catalogue') }}</label
              ><label><input v-model="metadata.outputs.pdf" type="checkbox" />{{ t('PDF catalogue') }}</label
              ><label><input v-model="metadata.isActive" type="checkbox" />{{ t('Active') }}</label>
            </div>
          </fieldset>
          <button type="submit" :disabled="!ssoCanEdit || busy" :aria-busy="pendingAction === 'save'">
            {{ busy ? t('Saving…') : t('Save catalogue') }}
          </button>
        </form></ModalContent
      ></Modal
    >
  </section>
</template>
<style scoped>
.catalogue-gallery {
  display: grid;
  gap: 24px;
  color: var(--color-foreground);
}
header,
.card-heading,
.designer-actions,
.card-actions,
.output-options {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
}
h1 {
  font:
    42px/1.1 Georgia,
    serif;
  margin: 8px 0;
}
h2 {
  font-size: 19px;
  margin: 0;
}
p {
  font-size: 14px;
  line-height: 1.6;
}
.search {
  max-width: 420px;
}
.catalogue-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 24px;
}
.catalogue-grid article {
  border: 1px solid var(--color-border, #d9d3c7);
  border-radius: 12px;
  overflow: hidden;
  background: var(--color-card, #fffdf8);
}
.catalogue-cover {
  min-height: 230px;
  background: var(--color-muted, #e9e4d8);
  padding: 30px;
  color: var(--color-foreground, #34342c);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 24px;
}
.catalogue-cover span {
  font:
    24px Georgia,
    serif;
}
.catalogue-cover strong {
  font:
    32px/1.1 Georgia,
    serif;
  overflow-wrap: anywhere;
}
.card-body {
  padding: 20px;
  display: grid;
  gap: 14px;
}
.slug,
small,
.catalogue-note {
  font-size: 12px;
  color: var(--color-muted-foreground, #6e6b61);
}
.designer-actions {
  justify-content: start;
}
.card-actions {
  border-top: 1px solid var(--color-border, #ded8cc);
  padding-top: 12px;
  justify-content: start;
}
.card-actions button {
  font-size: 12px;
  background: transparent;
  padding: 6px;
}
.output-options {
  justify-content: start;
}
.output-options label {
  display: flex;
  gap: 7px;
  align-items: center;
}
label {
  display: grid;
  gap: 7px;
  font-size: 13px;
}
input,
select,
textarea,
button {
  font: inherit;
  color: inherit;
  border: 1px solid var(--color-border, #c9c1b1);
  border-radius: 6px;
  padding: 9px;
  background: var(--color-card, #fffdf8);
}
button {
  cursor: pointer;
}
button:disabled {
  opacity: 0.5;
  cursor: default;
}
.catalogue-settings-modal {
  width: min(550px, calc(100vw - 32px));
  max-height: 90dvh;
  overflow: auto;
  border: 1px solid #ccc4b5;
  border-radius: 14px;
  padding: 24px;
  background: var(--color-card, #fffdf8);
  color: inherit;
}
.catalogue-settings-modal form,
fieldset {
  display: grid;
  gap: 18px;
}
fieldset {
  border: 0;
  padding: 0;
}
input,
textarea,
select {
  box-sizing: border-box;
  max-width: 100%;
  min-width: 0;
}
@media (max-width: 600px) {
  h1 {
    font-size: 32px;
  }
  .catalogue-settings-modal {
    width: 100%;
    max-width: none;
    min-height: 100dvh;
    max-height: 100dvh;
    border: 0;
    border-radius: 0;
    margin: 0;
    box-sizing: border-box;
  }
}
</style>
