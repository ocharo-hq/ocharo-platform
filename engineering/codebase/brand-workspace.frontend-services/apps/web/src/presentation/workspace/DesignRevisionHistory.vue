<script setup lang="ts">
import { useErrorToast } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed, onScopeDispose, ref, shallowRef, useId, watch } from 'vue';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { cloneSnapshot, type DesignDocument } from '@/domain/workspace/EditorModels';
import type { DesignRevision, DesignRevisionCopy } from '@/domain/workspace/DesignRevisions';
import DesignPrintPreview from './DesignPrintPreview.vue';

const { t } = useUiLocale();

defineOptions({ name: 'DesignRevisionHistory' });
const props = defineProps<{ access: AccessControls; organizationId: string; document: DesignDocument | null; canEdit: boolean; disabled?: boolean }>();
const emit = defineEmits<{ restore: [copy: DesignRevisionCopy] }>();
const panelId = useId(), pageId = useId();
const expanded = ref(false), loading = ref(false), previewLoading = ref(false);
const hasLoaded = ref(false);
const revisions = ref<DesignRevision[]>([]), preview = shallowRef<DesignDocument | null>(null);
const selected = ref<number | null>(null), pageIndex = ref(0), hasMore = ref(false);
const listError = ref<string | null>(null), previewError = ref<string | null>(null);
useErrorToast(listError); useErrorToast(previewError);
const locked = computed(() => !!props.disabled || props.access.busy.value);
const page = computed(() => preview.value?.snapshot.pages[pageIndex.value]);
const metadata = computed(() => revisions.value.find(item => item.revision === preview.value?.revision));
let listController: AbortController | undefined, previewController: AbortController | undefined;
let listSequence = 0, previewSequence = 0, nextOffset = 0, disposed = false;
const pageSize = 20;

function cancel(): void {
  listSequence++; previewSequence++;
  listController?.abort(); previewController?.abort();
  loading.value = false; previewLoading.value = false; hasLoaded.value = false;
}
function clear(): void {
  cancel(); revisions.value = []; preview.value = null; selected.value = null;
  pageIndex.value = 0; nextOffset = 0; hasMore.value = false;
  listError.value = ''; previewError.value = '';
}
watch(() => [props.organizationId, props.document?.id, props.document?.revision], () => {
  clear();
  if (!props.document) expanded.value = false;
  else if (expanded.value && !locked.value) void load();
}, { flush: 'sync' });
watch(() => props.access.invalidated.value, invalidated => { if (invalidated) { clear(); expanded.value = false; } });
onScopeDispose(() => { disposed = true; cancel(); });
function toggle(): void {
  if (locked.value || !props.document) return;
  expanded.value = !expanded.value;
  clear();
  if (expanded.value) void load();
}
async function load(older = false): Promise<void> {
  if (!props.document || locked.value || loading.value || !expanded.value) return;
  const requestedOffset = older ? nextOffset : 0;
  const documentId = props.document.id, organizationId = props.organizationId;
  const request = ++listSequence;
  listController?.abort(); const controller = new AbortController(); listController = controller;
  loading.value = true; listError.value = '';
  try {
    const result = await props.access.api.getDesignRevisions(organizationId, documentId, controller.signal, requestedOffset, pageSize);
    if (disposed || controller.signal.aborted || request !== listSequence) return;
    if (result.ok) hasLoaded.value = true;
    if (!result.ok) { listError.value = failureMessage(result.failure); return; }
    // Offset advances over server rows; de-duplication tolerates another editor appending a revision.
    nextOffset = requestedOffset + result.value.length;
    const unique = new Map((older ? revisions.value : []).map(item => [item.revision, item]));
    for (const item of result.value) unique.set(item.revision, item);
    revisions.value = [...unique.values()].sort((a, b) => b.revision - a.revision);
    hasMore.value = result.value.length === pageSize;
  } catch {
    if (!controller.signal.aborted && request === listSequence && !disposed) listError.value = 'Unable to load saved revisions. Your current design is unchanged. Retry when ready.';
  } finally { if (request === listSequence) loading.value = false; }
}
async function inspect(revision: number): Promise<void> {
  if (!props.document || locked.value || !expanded.value) return;
  const source = props.document, organizationId = props.organizationId, request = ++previewSequence;
  previewController?.abort(); const controller = new AbortController(); previewController = controller;
  selected.value = revision; preview.value = null; previewError.value = ''; pageIndex.value = 0; previewLoading.value = true;
  try {
    const result = await props.access.api.getDesignRevision(organizationId, source.id, revision, controller.signal);
    if (disposed || controller.signal.aborted || request !== previewSequence) return;
    if (!result.ok) { previewError.value = failureMessage(result.failure); return; }
    const value = result.value;
    if (value.id !== source.id || value.organizationId !== organizationId || value.kind !== source.kind || value.revision !== revision) {
      previewError.value = 'The service returned a different revision. Retry before restoring a copy.'; return;
    }
    preview.value = { ...value, snapshot: cloneSnapshot(value.snapshot) };
  } catch {
    if (!controller.signal.aborted && request === previewSequence && !disposed) previewError.value = 'Unable to load this revision. Your current design is unchanged. Retry when ready.';
  } finally { if (request === previewSequence) previewLoading.value = false; }
}
function restore(): void {
  if (!preview.value || locked.value || !props.canEdit || previewLoading.value || loading.value || selected.value !== preview.value.revision) return;
  emit('restore', { snapshot: cloneSnapshot(preview.value.snapshot), title: preview.value.title, revision: preview.value.revision });
}
function date(value: string): string { return new Date(value).toLocaleString(); }
</script>

<template>
  <section class="revision-history" :aria-label="t('Saved design history')">
    <button type="button" class="history-toggle" :aria-expanded="expanded" :aria-controls="panelId" :disabled="locked || !document" @click="toggle">
      <span>{{ t("Saved revision history") }}</span><span aria-hidden="true">{{ expanded ? '−' : '+' }}</span>
    </button>
    <p v-if="!document" class="history-hint">{{ t("Save a design to start its revision history.") }}</p>
    <div v-if="expanded" :id="panelId" class="history-content">
      <p class="history-hint">{{ t("Inspect earlier saved pages. Restoring creates a separate unsaved copy for editing.") }}</p>
      <div class="history-actions"><button type="button" class="quiet-button" :disabled="locked || loading" @click="load()">{{ t("Refresh history") }}</button></div>
      <ContentSkeleton v-if="loading&&!hasLoaded" variant="rows" label="Loading revisions…" :cards="3" />
      <p v-else-if="listError && !hasLoaded" class="query-label" role="status">{{ t("Saved revision history is unavailable. Refresh to retry.") }}</p>
      <p v-if="hasLoaded && !revisions.length" class="query-label">{{ t("No saved revisions were returned. Refresh to try again.") }}</p>
      <div v-if="revisions.length" class="history-layout" :aria-busy="loading">
        <div class="history-list">
          <ol :aria-label="t('Saved revisions')">
            <li v-for="revision in revisions" :key="revision.revision">
              <button type="button" class="revision-choice" :aria-pressed="selected === revision.revision" :disabled="locked" @click="inspect(revision.revision)">
                <strong>{{ t("Revision") }} {{ revision.revision }}<span v-if="revision.revision === document?.revision"> {{ ' ' + t('· loaded') }}</span></strong>
                <span>{{ revision.title }}</span>
                <time :datetime="revision.savedAtUtc">{{ date(revision.savedAtUtc) }}</time>
                <span>{{ t("Saved by") }} {{ revision.savedBy }}<span v-if="revision.archived"> {{ ' ' + t('· archived') }}</span></span>
              </button>
            </li>
          </ol>
          <button v-if="hasMore" type="button" class="quiet-button" :disabled="locked || loading" @click="load(true)">{{ t("Load older revisions") }}</button>
        </div>
        <div class="history-preview" :aria-busy="previewLoading">
          <ContentSkeleton v-if="previewLoading" variant="canvas" label="Loading revision" :cards="1" />
          <div v-if="previewError"><p class="history-hint">{{ t("Preview unavailable") }}</p><button v-if="selected" type="button" class="quiet-button" :disabled="locked" @click="inspect(selected)">{{ t("Retry revision") }}</button></div>
          <template v-if="preview">
            <h3>{{ preview.title }}</h3>
            <p>{{ t("Revision") }} {{ preview.revision }} · {{ preview.snapshot.locale.toUpperCase() }} · {{ preview.snapshot.pages.length }} {{ t("pages") }}</p>
            <p v-if="metadata">{{ t("Saved") }} <time :datetime="metadata.savedAtUtc">{{ date(metadata.savedAtUtc) }}</time> {{ t("by") }} {{ metadata.savedBy }}</p>
            <label :for="pageId" class="field-label">{{ t("Preview page") }}</label>
            <select :id="pageId" v-model="pageIndex" :aria-label="t('Historical preview page')">
              <option v-for="(item, index) in preview.snapshot.pages" :key="item.id" :value="index">{{ index + 1 }}. {{ item.title }}</option>
            </select>
            <div v-if="page" class="history-page" :lang="preview.snapshot.locale"><DesignPrintPreview :key="`${preview.revision}-${page.id}`" :page="page" /></div>
            <button type="button" class="restore-button" :disabled="locked || !canEdit || loading || previewLoading" @click="restore">{{ t("Restore as new unsaved copy") }}</button>
            <p v-if="!canEdit" class="history-hint">{{ t("An editor or owner can restore a copy.") }}</p>
          </template>
          <p v-else-if="!previewLoading && !previewError" class="history-hint">{{ t("Choose a revision to preview its pages.") }}</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.revision-history { margin-top: 20px; border: 1px solid #e3ddd2; border-radius: 12px; background: #fffdf8; min-width: 0; overflow: hidden; }
.history-toggle { display: flex; justify-content: space-between; gap: 16px; width: 100%; border: 0; background: transparent; padding: 18px 20px; text-align: left; font: 600 14px/1.5 Arial,sans-serif; color: #34372b; cursor: pointer; }
.history-toggle:disabled { cursor: default; opacity: .6; }.history-content { padding: 0 20px 20px; }.history-hint { font-size: 13px; line-height: 1.6; color: #68675e; }.revision-history>.history-hint { padding: 0 20px 12px; margin: 0; }
.history-actions { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin: 12px 0; font-size: 13px; }.history-layout { display: grid; grid-template-columns: minmax(200px, 280px) minmax(0, 1fr); gap: 24px; align-items: start; }
.history-list { min-width: 0; }.history-list ol { margin: 0 0 12px; padding: 0; list-style: none; display: grid; gap: 8px; max-height: 540px; overflow: auto; }.revision-choice { width: 100%; display: grid; gap: 5px; padding: 12px; border: 1px solid #e3ddd2; border-radius: 8px; background: #fff; text-align: left; color: #34372b; cursor: pointer; font-size: 12px; line-height: 1.5; overflow-wrap: anywhere; }.revision-choice[aria-pressed="true"] { border-color: #756b50; background: #f5f1e6; }.revision-choice time { color: #68675e; }
.history-preview { min-width: 0; }.history-preview h3 { font: 23px/1.3 Georgia,serif; overflow-wrap: anywhere; margin: 0 0 8px; }.history-preview p { font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }.history-preview select { width: 100%; max-width: 430px; }.history-page { width: min(100%, 430px); margin: 16px 0; }.restore-button { padding: 12px 16px; border: 1px solid #514a37; border-radius: 7px; background: #514a37; color: #fff; font: 600 13px/1.4 Arial,sans-serif; cursor: pointer; }.restore-button:disabled { opacity: .55; cursor: default; }
@media (max-width: 700px) { .history-layout { grid-template-columns: minmax(0,1fr); }.history-list ol { max-height: 240px; }.history-content { padding: 0 14px 16px; }.restore-button { width: 100%; } }
</style>
