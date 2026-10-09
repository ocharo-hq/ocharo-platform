<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, onMounted, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { failureMessage } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { useDesignPreflight } from '@/application/workspace/UseDesignPreflight';
import type { DesignPreflightIssue } from '@/domain/workspace/DesignPreflight';
import DesignPreflightReview from './DesignPreflightReview.vue';
import DesignRecovery from './DesignRecovery.vue';
import DesignRevisionHistory from './DesignRevisionHistory.vue';
import { designEditorUrl } from '@/domain/workspace/EditorNavigation';
import { useDesignDocument } from '@/application/workspace/DocumentStore';
import { downloadBlob, exportDesignPdf, exportDesignPng, safeFilename } from '@/application/workspace/ExportDocument';
import { fitTextFrame } from '@/domain/workspace/FitTextFrame';
import { renumberCataloguePages } from '@/domain/workspace/CatalogueLayout';
import { cloneSnapshot, validateSnapshot, type DesignElement, type DesignKind, type DesignSnapshot } from '@/domain/workspace/EditorModels';
import { brandAssets, createCatalogueTemplate, createCardTemplate } from '@/domain/workspace/Templates';
import DesignCanvas from './DesignCanvas.vue';
import DesignPrintPreview from './DesignPrintPreview.vue';
import CataloguePublication from './CataloguePublication.vue';
import CatalogueTools from './CatalogueTools.vue';
import CatalogueSettingsPanel from './CatalogueSettingsPanel.vue';
import { requestConfirmation } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { useCommerce } from '@/application/workspace/UseCommerce';
import { eligibleCataloguePdf } from '@/domain/workspace/CatalogueDocuments';
import type { CommerceDraft } from '@/domain/workspace/Commerce';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import CatalogueBuilder from './CatalogueBuilder.vue';
import { appendCataloguePages, createCatalogueCover, removeCataloguePage } from '@/domain/workspace/CatalogueComposer';
import type { DesignPage } from '@/domain/workspace/EditorModels';
import CardTools from './CardTools.vue';

defineOptions({ name: 'DesignEditorPage' });
const { t } = useUiLocale();
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; kind: DesignKind; initialDocumentId?: string | null; initialPageId?: string | null; navigationError?: string | null }>();
const store = useDesignDocument(props.access, props.workspace.organization.value!.id, props.kind);
const preflight = useDesignPreflight();
const catalogueApi=useCommerce(props.access);
const exportExclusions=ref<string[]>([]);
const pageId = ref('');
const selectedId = ref<string | null>(null);
const locale = ref<'uz'|'ru'|'en'>('uz');
const exporting = ref(false);
const preparingEdition = ref(false);
const exportStatus = ref('');
const zoom = ref(100);
const printPreview = ref(false);
const pdfScope = ref<'all' | 'current' | 'selected'>('all');
const selectedPages = ref<string[]>([]);
const library = ref(true);
const librarySearch = ref('');
const routeReady = ref(false);
const navigationNotice = ref('');
const savedDesigns = computed(() => store.documents.value.filter(doc => !doc.archived && doc.title.toLocaleLowerCase().includes(librarySearch.value.trim().toLocaleLowerCase())));
const navigationLocked = computed(() => store.busy.value || store.loading.value || props.access.busy.value || exporting.value || preparingEdition.value || preflight.busy.value);
const savedLink = computed(() => store.record.value ? designEditorUrl(props.workspace.organization.value!.id, props.kind, store.record.value.id, page.value?.id) : null);
const builder = ref(false);
const stageSurface = ref<HTMLElement>();
const stageSize = ref({width: 800, height: 700});
const fileInput = ref<HTMLInputElement>();
const photoInput = ref<HTMLInputElement>();
const page = computed(() => store.snapshot.value?.pages.find((p) => p.id === pageId.value) ?? store.snapshot.value?.pages[0]);
const artboardWidth = computed(() => {
  if (!page.value) return 600;
  const width = Math.max(160, stageSize.value.width - 56);
  const height = Math.max(200, stageSize.value.height - 44);
  return Math.min(width, height * page.value.width / page.value.height) * zoom.value / 100;
});
watch(stageSurface, (surface, _previous, onCleanup) => {
  if (!surface || typeof ResizeObserver === 'undefined') return;
  const observer = new ResizeObserver(() => { stageSize.value = {width:surface.clientWidth,height:surface.clientHeight}; });
  observer.observe(surface); onCleanup(() => observer.disconnect());
}, {flush:'post'});
const selected = computed(() => page.value?.elements.find((e) => e.id === selectedId.value));
const disabled = computed(() => !props.workspace.canEdit.value || store.busy.value || props.access.busy.value || exporting.value || preparingEdition.value || preflight.busy.value || store.loading.value);
const templateCover = computed(() => props.kind === 'catalogue' ? createCatalogueTemplate(locale.value).pages[0]?.elements.find(e => e.type === 'image')?.src : brandAssets.cardBackground);
const appName = computed(() => props.kind === 'catalogue' ? 'Catalogue' : 'Business cards');
watch(() => store.snapshot.value?.pages.map(item => item.id), ids => { selectedPages.value = selectedPages.value.filter(id => ids?.includes(id)); });
watch(page, (value) => { if (value && pageId.value !== value.id) pageId.value = value.id; });
onMounted(async () => {
  await store.list();
  if (props.navigationError) navigationNotice.value = props.navigationError;
  else if (props.initialDocumentId) {
    if (await store.load(props.initialDocumentId)) {
      library.value = false;
      if (props.initialPageId) {
        if (store.snapshot.value?.pages.some(item => item.id === props.initialPageId)) pageId.value = props.initialPageId;
        else navigationNotice.value = 'This page is no longer in the saved design. The first page is open.';
      }
    } else navigationNotice.value = 'This catalogue is unavailable.';
  }
  routeReady.value = true;
});
watch([() => store.record.value?.id, () => page.value?.id, routeReady], () => {
  if (!routeReady.value || !store.snapshot.value) return;
  const path = designEditorUrl(props.workspace.organization.value!.id, props.kind, store.record.value?.id, page.value?.id, props.kind==='catalogue'?'pdf':null);
  window.history.replaceState(window.history.state, '', path);
}, { flush: 'post' });
async function openDesignFromLink(event: MouseEvent, id: string): Promise<void> {
  if (navigationLocked.value) { event.preventDefault(); return; }
  if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) return;
  event.preventDefault();
  if (await store.load(id)) { library.value = false; selectedId.value = null; pageId.value = store.snapshot.value?.pages[0]?.id ?? ''; navigationNotice.value = ''; }
}
async function restoreRevision(value: { snapshot: DesignSnapshot; title: string; revision: number }): Promise<void> {
  if (disabled.value) return;
  if (await store.create(value.snapshot, `${value.title} · restored revision ${value.revision}`.slice(0, 160))) {
    selectedId.value = null; pageId.value = store.snapshot.value?.pages[0]?.id ?? ''; library.value = false;
    navigationNotice.value = t('Revision {revision} opened as a new unsaved copy. Save design to keep it.', {revision:value.revision});
  }
}
async function connectCatalogueCopy(snapshot: DesignSnapshot): Promise<void> {
  if (disabled.value) return;
  if (await store.create(snapshot, `${store.title.value || 'Ocharo catalogue'} · connected`.slice(0, 160))) {
    library.value = false;
    navigationNotice.value = t('Connected as a new unsaved copy. Save design to keep it.');
  }
}
function updateSnapshot(next: DesignSnapshot): void { if (!disabled.value) store.replace(next); }
async function create(): Promise<void> {
  if (disabled.value) return;
  await store.create(props.kind === 'catalogue' ? createCatalogueTemplate(locale.value) : createCardTemplate('labelled', locale.value), props.kind === 'catalogue' ? `Ocharo · 2026 collection · ${locale.value.toUpperCase()}` : 'Ocharo · business card');
  pageId.value = store.snapshot.value?.pages[0]?.id ?? ''; selectedId.value = null;
}
async function insertPages(pages: DesignPage[], brandName: string): Promise<void> {
  if (disabled.value) return;
  try {
    if (store.snapshot.value) store.replace(appendCataloguePages(store.snapshot.value, pages, page.value?.id));
    else await store.create(appendCataloguePages(createCatalogueCover(locale.value, brandName), pages), `${brandName} · catalogue · ${locale.value.toUpperCase()}`);
    pageId.value = pages[0]?.id ?? ''; selectedId.value = null; builder.value = false;
  } catch (cause) { store.error.value = cause instanceof Error ? cause.message : 'Unable to add pages.'; }
}
async function removePage(): Promise<void> {
  if (!store.snapshot.value || !page.value || disabled.value) return;
  if (!await requestConfirmation(t('Remove “{title}” from this edition? You can undo this change.', {title:page.value.title}))) return;
  try { store.replace(removeCataloguePage(store.snapshot.value, page.value.id)); selectedId.value = null; }
  catch (cause) { store.error.value = cause instanceof Error ? cause.message : 'Unable to remove page.'; }
}
function renamePage(event: Event): void {
  if (!store.snapshot.value || !page.value) return;
  const next = cloneSnapshot(store.snapshot.value), target = next.pages.find(item => item.id === page.value!.id);
  if (target) { target.title = text(event).trim(); updateSnapshot(next); }
}
function changeElements(elements: DesignElement[]): void {
  if (!store.snapshot.value || !page.value || disabled.value) return;
  const next = cloneSnapshot(store.snapshot.value);
  next.pages.find((p) => p.id === page.value!.id)!.elements = elements;
  store.replace(next);
}
function patchElement(patch: Partial<DesignElement>): void {
  if (!selected.value || !page.value || (selected.value.binding && typeof patch.text === 'string')) return;
  const fitted = props.kind === 'business-card' && typeof patch.text === 'string'
    ? fitTextFrame(selected.value, page.value, patch.text) : selected.value;
  changeElements(page.value.elements.map((e) => e.id === selectedId.value ? { ...fitted, ...patch } : e));
}
function numeric(event: Event, key: 'x'|'y'|'width'|'height'|'fontSize'|'cropX'|'cropY'|'zoom'|'rotation'): void {
  const value = Number((event.target as HTMLInputElement).value);
  if (Number.isFinite(value)) patchElement({ [key]: value });
}
function text(event: Event): string { return (event.target as HTMLInputElement).value; }
function reorderPage(direction: -1|1): void {
  if (!store.snapshot.value || !page.value) return;
  const next = cloneSnapshot(store.snapshot.value), index = next.pages.findIndex((p) => p.id === page.value!.id), target = index + direction;
  if (target < 0 || target >= next.pages.length) return;
  [next.pages[index], next.pages[target]] = [next.pages[target]!, next.pages[index]!]; store.replace(renumberCataloguePages(next));
}
function duplicatePage(): void {
  if (!store.snapshot.value || !page.value) return;
  const next = cloneSnapshot(store.snapshot.value), copy = JSON.parse(JSON.stringify(page.value)) as typeof page.value;
  copy.id = crypto.randomUUID(); copy.title += ' copy'; next.pages.splice(next.pages.findIndex((p) => p.id === page.value!.id) + 1, 0, copy);
  store.replace(renumberCataloguePages(next)); pageId.value = copy.id;
}
function addText(): void {
  if (!page.value) return;
  const id = crypto.randomUUID();
  changeElements([...page.value.elements, { id, type: 'text', label: 'New text', x: 10, y: 10, width: Math.min(page.value.width - 20, 80), height: 12, text: 'Your text', fontSize: 4, color: '#282820', fontFamily: 'sans' }]); selectedId.value = id;
}
function removeElement(): void { if (page.value) { changeElements(page.value.elements.filter((e) => e.id !== selectedId.value)); selectedId.value = null; } }
function moveLayer(direction: -1|1): void {
  if (!page.value) return;
  const elements = [...page.value.elements], index = elements.findIndex((e) => e.id === selectedId.value), target = index + direction;
  if (target < 0 || target >= elements.length) return;
  [elements[index], elements[target]] = [elements[target]!, elements[index]!]; changeElements(elements);
}
async function upload(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement, file = input.files?.[0]; input.value = '';
  if (!file || disabled.value || !page.value) return;
  if (!['image/png','image/jpeg'].includes(file.type) || file.size > 5 * 1024 * 1024) { store.error.value = 'Choose a PNG or JPEG image up to 5 MB.'; return; }
  const targetPageId = page.value.id;
  const targetImageId = selected.value?.type === 'image' ? selected.value.id : null;
  const result = await props.workspace.uploadMedia(file, 'Uploaded in the design editor by the signed-in operator.', new AbortController().signal);
  if (!result.ok) { store.error.value = failureMessage(result.failure); return; }
  const src = `/api/organizations/${props.workspace.organization.value!.id}/media/${result.value.id}/content`;
  if (!store.snapshot.value) return;
  const next = cloneSnapshot(store.snapshot.value), target = next.pages.find(p => p.id === targetPageId);
  if (!target) { store.error.value = 'The photo was uploaded. Choose it from the media library to add it to a page.'; return; }
  const image = target.elements.find(e => e.id === targetImageId);
  if (image) { image.src = src; image.label = file.name; }
  else {
    const id = crypto.randomUUID(); target.elements.push({ id, type: 'image', label: file.name, src, x: 10, y: 10, width: Math.min(60, target.width - 20), height: Math.min(80, target.height - 20), fit: 'cover', cropX: 50, cropY: 50, zoom: 1 });
    if (pageId.value === targetPageId) selectedId.value = id;
  }
  store.replace(next);
}
async function importFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement, file = input.files?.[0]; input.value = '';
  if (!file || disabled.value) return;
  try {
    if (file.size > 1_048_576) throw new Error('The design file exceeds 1 MB.');
    const data = JSON.parse(await file.text()) as { title?: string; snapshot?: unknown };
    const snapshot = validateSnapshot(data.snapshot ?? data);
    if (snapshot.kind !== props.kind) throw new Error('Open this file in the other editor.');
    await store.create(snapshot, data.title?.slice(0,160) || 'Imported design');
  } catch (error) { store.error.value = error instanceof Error ? error.message : 'Unable to import this design.'; }
}
function exportEditable(): void {
  if (store.snapshot.value) downloadBlob(new Blob([JSON.stringify({ title: store.title.value, snapshot: store.snapshot.value }, null, 2)], {type:'application/json'}), `${safeFilename(store.title.value)}.ocharo.json`);
}
function inspectIssue(issue: DesignPreflightIssue): void {
  pageId.value = issue.pageId; selectedId.value = issue.elementId; printPreview.value = false;
}
async function exportSnapshot(source:DesignSnapshot):Promise<DesignSnapshot>{
  if(source.kind!=='catalogue')return source;
  const brands=[...new Set([source.catalogueSettings?.brandId,...source.pages.map(page=>page.catalogueSource?.brandId)].filter((id):id is string=>!!id))];
  const drafts:CommerceDraft[]=[];
  for(const brand of brands){const result=await catalogueApi.get(props.workspace.organization.value!.id,brand,new AbortController().signal);if(!result.ok)throw new Error(failureMessage(result.failure));if(result.value)drafts.push(result.value);}
  const result=eligibleCataloguePdf(source,drafts);exportExclusions.value=result.excluded.map(item=>item.title);
  if(!result.snapshot.pages.length)throw new Error(t('No active, eligible PDF pages are available.'));
  return result.snapshot;
}
function togglePage():void{if(!page.value||!store.snapshot.value||disabled.value)return;const next=cloneSnapshot(store.snapshot.value),target=next.pages.find(item=>item.id===page.value!.id)!;target.isActive=target.isActive===false;store.replace(next);}
async function checkPages(): Promise<void> {
  if (!store.snapshot.value || exporting.value || preflight.busy.value) return;
  try { await preflight.check(await exportSnapshot(store.snapshot.value), { actionLabel: 'Done', inspectionOnly: true }); }
  catch (cause) { store.error.value = cause instanceof Error ? cause.message : 'Unable to check pages.'; }
}
async function exportFile(format: 'pdf'|'png'): Promise<void> {
  if (!store.snapshot.value || !page.value || exporting.value || preparingEdition.value || preflight.busy.value) return;
  let snapshot = cloneSnapshot(store.snapshot.value); const title = store.title.value, currentPage = snapshot.pages.find(item => item.id === page.value!.id)!;
  let ids = format === 'png' || pdfScope.value === 'current' ? [currentPage.id] : pdfScope.value === 'selected' ? [...selectedPages.value] : undefined;
  const unsaved = store.dirty.value;
  exporting.value = true; store.error.value = ''; exportStatus.value = 'Checking export pages…';
  try {
    snapshot=await exportSnapshot(snapshot);
    if(ids){ids=ids.filter(id=>snapshot.pages.some(page=>page.id===id));if(!ids.length)throw new Error(t('The selected pages are inactive or unavailable in this language.'));}
    if(exportExclusions.value.length&&!await requestConfirmation(t('These pages will be excluded: {pages}. Continue with the remaining pages?',{pages:exportExclusions.value.join(', ')})))return;
    const allowed = await preflight.check(snapshot, { ...(ids ? { pageIds: ids } : {}), actionLabel: t('Download {format}',{format:format.toUpperCase()}), unsaved });
    if (!allowed) { exportStatus.value = 'Export cancelled. Your edits are retained.'; return; }
    if (format === 'pdf') await exportDesignPdf(snapshot, title, (current,total) => { exportStatus.value = t('Rendering page {current} of {total}…',{current,total}); }, ids);
    else await exportDesignPng(currentPage, title);
    exportStatus.value = t(unsaved ? 'Downloaded {format} from unsaved edits.' : 'Downloaded {format}.', {format:format.toUpperCase()});
  } catch (error) { store.error.value = error instanceof Error ? error.message : 'Export failed. Retry when images are available.'; exportStatus.value = ''; }
  finally { exporting.value = false; }
}
</script>

<template>
  <section class="design-app">
    <header class="design-heading"><div><p class="eyebrow">{{ t("Ocharo / design atelier") }}</p><h1>{{ t(appName) }}</h1><p class="design-subtitle">{{ kind === 'catalogue' ? t("Shape the collection. Keep every detail yours.") : t("A small introduction, beautifully yours.") }}</p></div><button class="quiet-button" @click="library = !library">{{ library ? t("Hide") : t("Saved") }} {{ t("designs") }} <span>{{ store.documents.value.length }}</span></button></header>
    <div v-if="kind === 'catalogue'" class="flex flex-wrap gap-3"><Button variant="outline" :is-disabled="disabled" @click="builder = !builder">{{ builder ? t("Hide product builder") : t("Build from products") }}</Button><span class="template-note">{{ t("Product pages · continuation pages · photo collages") }}</span></div>
    <CatalogueBuilder v-if="kind === 'catalogue' && builder" :access="access" :workspace="workspace" :locale="store.snapshot.value?.locale ?? locale" :disabled="disabled" :has-document="!!store.snapshot.value" :initial-brand-id="page?.catalogueSource?.brandId ?? ''" @pages="insertPages" @close="builder = false" />
    <p v-if="navigationNotice" class="editor-alert" role="status">{{ t(navigationNotice) }}</p>
    <DesignRecovery :recovery="store.recovery" :disabled="disabled" />
    <section v-if="library" class="saved-library catalogue-library" :aria-label="t('Saved designs')">
      <div class="library-heading"><div><h2>{{ t("Saved") }} {{ kind === 'catalogue' ? t('catalogues') : t("business cards") }}</h2><p>{{ t("Continue a saved design, or create a new one below.") }}</p></div><label class="field-label">{{ t("Find a saved design") }}<input v-model="librarySearch" type="search" :placeholder="t('Search by name')" maxlength="160"></label></div>
      <Button variant="outline" :is-disabled="navigationLocked" :is-loading="store.loading.value" @click="store.list()">{{t("Refresh")}}</Button>
      <ContentSkeleton v-if="store.loading.value&&!store.listLoaded.value" variant="rows" :cards="3" :label="t('Loading saved designs…')"/>
      <p v-else-if="store.listLoaded.value&&!savedDesigns.length" class="query-label">{{ librarySearch ? t("No saved designs match this name.") : t("No saved designs yet.") }}</p><p v-else-if="!store.loading.value&&!store.listLoaded.value" class="query-label" role="status">{{t("Content unavailable. Try refreshing.")}}</p>
      <a v-for="doc in savedDesigns" :key="doc.id" class="saved-design" :href="designEditorUrl(workspace.organization.value!.id, kind, doc.id)" :aria-disabled="navigationLocked || undefined" @click="openDesignFromLink($event, doc.id)"><b>{{ doc.title }}</b><small>{{ t("Revision") }} {{ doc.revision }} · {{ new Date(doc.updatedAtUtc).toLocaleDateString() }}</small><span>{{ t("Open in editor →") }}</span></a>
    </section>
    <div v-if="!store.snapshot.value" class="template-launch">
      <div class="template-preview"><div class="template-preview-inner" :class="{'is-card':kind === 'business-card'}" :style="{backgroundImage:`linear-gradient(0deg,#20190f30,#20190f10),url(${templateCover})`}"><img class="template-wordmark" :src="brandAssets.whiteLogo" alt="Ocharo"><span>{{ kind === 'catalogue' ? t("2026 COLLECTION") : t("THE ART OF INTRODUCTION") }}</span></div><span class="template-caption">{{ kind === 'catalogue' ? t("Printed collection · 16 editable pages") : t("Signature card · front & back · 85 × 55 mm") }}</span></div>
      <div class="template-launch-copy"><p class="eyebrow">{{ t("Your existing design, ready to edit") }}</p><h2>{{ kind === 'catalogue' ? t("The collection continues.") : t("Make your mark.") }}</h2><p>{{ kind === 'catalogue' ? t("Start with your printed catalogue. Change prices, rearrange garments, crop photos, and make each page your own.") : t("Edit your name, contact details, colors, and artwork. Keep multiple versions and export whenever you need them.") }}</p><label class="field-label">{{ t("Template language") }}<DeskSelect v-model="locale" :label="t('Template language')" :options="[({ value: 'uz' as const, label: 'O‘zbekcha' }), ({ value: 'ru' as const, label: 'Русский' }), ({ value: 'en' as const, label: 'English' })]" /></label><div class="flex flex-wrap gap-3"><Button :is-disabled="disabled" @click="create">{{ t("Edit current template") }}</Button><Button variant="outline" :is-disabled="disabled" @click="fileInput?.click()">{{ t("Import design") }}</Button></div><p class="template-note">{{ t("The original stays safe. Your edits become a new saved design.") }}</p><p v-if="!workspace.canEdit.value" class="template-note">{{ t("Your current role can view saved designs.") }}</p><div v-if="store.documents.value.length" class="recent-designs"><h3>{{ t("Continue working") }}</h3><a v-for="doc in store.documents.value.filter(item => !item.archived).slice(0,3)" :key="doc.id" class="saved-design" :href="designEditorUrl(workspace.organization.value!.id, kind, doc.id)" :aria-disabled="navigationLocked || undefined" @click="openDesignFromLink($event, doc.id)"><b>{{ doc.title }}</b><small>{{ t("Revision") }} {{ doc.revision }} →</small></a></div></div>
    </div>
    <template v-else>
      <div class="design-toolbar"><div class="design-name"><input v-model="store.title.value" :aria-label="t('Design name')" maxlength="160" :disabled="disabled"><span :class="{'unsaved':store.dirty.value}" role="status">{{ store.dirty.value ? t("Unsaved changes") : t(store.status.value) || t("Saved") }}</span></div><div class="toolbar-actions"><button class="quiet-button" :disabled="disabled || !store.history.value.length" @click="store.undo">{{ t("Undo") }}</button><button class="quiet-button" :disabled="disabled || !store.future.value.length" @click="store.redo">{{ t("Redo") }}</button><Button variant="outline" :is-disabled="disabled" @click="store.save(true)">{{ t("Save copy") }}</Button><Button :is-disabled="disabled || !store.dirty.value" @click="store.save()">{{ store.busy.value ? t("Saving…") : t("Save design") }}</Button></div></div>
      <CatalogueSettingsPanel v-if="kind === 'catalogue'" :access="access" :workspace="workspace" :snapshot="store.snapshot.value" :page-id="pageId" :selected-id="selectedId" :disabled="disabled" @change="updateSnapshot" @connect="connectCatalogueCopy" />
      <div class="editor-layout">
        <aside class="page-rail"><div class="rail-heading"><h2>{{ kind === 'catalogue' ? t("Pages") : t("Sides") }}</h2><span>{{ store.snapshot.value.pages.length }}</span></div><div v-for="(item,index) in store.snapshot.value.pages" :key="item.id" class="page-entry"><button class="page-tile" :class="{active:item.id === page?.id}" :aria-current="item.id === page?.id ? 'page' : undefined" @click="pageId = item.id; selectedId = null"><span class="page-number">{{ String(index+1).padStart(2,'0') }}</span><div class="page-miniature" :lang="store.snapshot.value.locale"><DesignCanvas :page="item" :editable="false" /></div><span>{{ item.title }}{{item.isActive===false?` · ${t('Inactive')}`:''}}</span></button><label class="page-export-choice"><input v-model="selectedPages" type="checkbox" :value="item.id" :aria-label="t('Select page {page} for PDF', {page:index+1})"> {{ t("Export page") }} {{ index + 1 }}</label></div><div class="page-rail-actions"><button class="quiet-button" :disabled="disabled" @click="reorderPage(-1)">{{ t("Move up") }}</button><button class="quiet-button" :disabled="disabled" @click="reorderPage(1)">{{ t("Move down") }}</button><button class="quiet-button" :disabled="disabled || store.snapshot.value.pages.length >= 100" @click="duplicatePage">{{ t("Duplicate page") }}</button><button v-if="kind === 'catalogue'" class="quiet-button" :disabled="disabled || store.snapshot.value.pages.length < 2" @click="removePage">{{ t("Remove page") }}</button></div></aside>
        <section class="design-stage" :aria-label="t('Design canvas and export')"><div class="stage-toolbar"><span>{{ page?.title }}</span><label v-if="kind==='catalogue'&&page"><input type="checkbox" :checked="page.isActive!==false" :disabled="disabled" @change="togglePage">{{t('Active page')}}</label><button type="button" class="preview-toggle" :aria-pressed="printPreview" @click="printPreview = !printPreview">{{ printPreview ? t("Edit page") : t("PDF preview") }}</button><label>{{ t("Zoom") }} <DeskSelect v-model="zoom" :label="t('Canvas zoom')" :options="[({ value: 65, label: '65%' }), ({ value: 85, label: '85%' }), ({ value: 100, label: (t('Fit')) }), ({ value: 125, label: '125%' }), ({ value: 150, label: '150%' })]" /></label></div><div ref="stageSurface" class="design-artboard-scroll"><div class="design-artboard" :lang="store.snapshot.value.locale" :style="{width: `${artboardWidth}px`}"><DesignPrintPreview v-if="page && printPreview" :page="page" /><DesignCanvas v-else-if="page" :page="page" :selected-id="selectedId" :editable="!disabled" @select="selectedId = $event" @change="changeElements" /></div></div><p class="canvas-hint">{{ printPreview ? t("Exact PDF renderer · fixed page proportions · zoom never reflows content") : t("Select any element · Drag to move · Use the corner to resize") }}</p><div class="export-tray"><div><b>{{ t("Ready to share?") }}</b><span>{{ Number(page?.width.toFixed(3)) }} × {{ Number(page?.height.toFixed(3)) }} mm · {{ pdfScope === 'all' ? (kind === 'catalogue' ? t("Whole catalogue PDF") : t("Two-sided card PDF")) : pdfScope === 'current' ? t("Current page PDF") : t('{count} selected pages',{count:selectedPages.length}) }}</span></div><label class="pdf-scope">{{ t("PDF pages") }}<DeskSelect v-model="pdfScope" :label="t('PDF export scope')" :disabled="exporting" :options="[({ value: 'all' as const, label: (t('Whole edition')) }), ({ value: 'current' as const, label: (t('Current page')) }), ({ value: 'selected' as const, label: (t('Selected pages (')) + (selectedPages.length) + ')' })]" /></label><button class="quiet-button" :disabled="exporting || preparingEdition || preflight.busy.value" @click="checkPages">{{ t("Check pages") }}</button><button class="quiet-button" :disabled="exporting || preparingEdition || preflight.busy.value" @click="exportEditable">{{ t("Editable file") }}</button><button class="quiet-button" :disabled="exporting || preparingEdition || preflight.busy.value" @click="exportFile('png')">{{ t("Page PNG") }}</button><Button variant="outline" :is-disabled="exporting || preparingEdition || preflight.busy.value || (pdfScope === 'selected' && !selectedPages.length)" @click="exportFile('pdf')">{{ exporting ? t("Exporting…") : t("Export PDF") }}</Button></div><p v-if="exportExclusions.length" class="edition-notice">{{t('Pages excluded from export:')}} {{exportExclusions.join(', ')}}</p><p v-if="exportStatus" class="export-status" role="status">{{ t(exportStatus) }}</p></section>
        <aside class="design-inspector"><fieldset :disabled="disabled"><label class="field-label">{{ t("Select element") }}<DeskSelect :label="t('Select page element')" :model-value="selectedId ?? ''" @update:model-value="selectedId = $event || null" :disabled="disabled" :options="[({ value: '' as const, label: (t('Page settings')) }), ...(page?.elements ?? []).map(element => ({ value: element.id, label: (element.label) + ' · ' + (element.type) }))]" /></label><div class="inspector-heading"><p class="eyebrow">{{ selected ? t("Selected element") : t("Page settings") }}</p><h2>{{ selected?.label || page?.title }}</h2></div><template v-if="selected"><label v-if="selected.type === 'text'" class="field-label">{{ t("Text") }}<textarea :value="selected.text" :readonly="!!selected.binding" rows="4" @change="patchElement({text:text($event)})" /></label><p v-if="selected.binding" class="inspector-help">{{ t(selected.binding.kind === 'product' ? 'Edit this value in Products, then refresh saved product values.' : 'Edit this value in Shared text and prices.') }}</p><div class="geometry-grid"><label v-for="key in ['x','y','width','height'] as const" :key="key" class="field-label">{{ t(key) }} (mm)<input type="number" step="0.5" :min="key === 'width' || key === 'height' ? 0.5 : -1000" max="1000" :value="Number(selected[key].toFixed(2))" @change="numeric($event,key)"></label></div><template v-if="selected.type === 'text'"><div class="geometry-grid"><label class="field-label">{{ t("Size (mm)") }}<input type="number" min="0.5" max="200" step="0.25" :value="selected.fontSize ?? 3" @change="numeric($event,'fontSize')"></label><label class="field-label">{{ t("Text color") }}<input type="color" :value="selected.color ?? '#282820'" @input="patchElement({color:text($event)})"></label></div><label class="field-label">{{ t("Typeface") }}<DeskSelect :model-value="selected.fontFamily ?? 'sans'" @update:model-value="patchElement({fontFamily:$event as 'serif'|'sans'})" :label="t('Typeface')" :disabled="disabled" :options="[({ value: 'serif' as const, label: (t('Georgia · editorial')) }), ({ value: 'sans' as const, label: (t('Arial · simple')) })]" /></label><label class="field-label">{{ t("Alignment") }}<DeskSelect :model-value="selected.align ?? 'left'" @update:model-value="patchElement({align:$event as 'left'|'center'|'right'})" :label="t('Alignment')" :disabled="disabled" :options="[({ value: 'left' as const, label: (t('left')) }), ({ value: 'center' as const, label: (t('center')) }), ({ value: 'right' as const, label: (t('right')) })]" /></label></template><template v-if="selected.type === 'image'"><button v-if="!selected.productId" class="inspector-action" @click="photoInput?.click()">{{ t("Replace photo") }}</button><p v-else class="inspector-help">{{ t('Choose another product image in Page photographs.') }}</p><label class="field-label">{{ t("Fit") }}<DeskSelect :model-value="selected.fit ?? 'cover'" @update:model-value="patchElement({fit:$event as 'cover'|'contain'})" :label="t('Fit')" :disabled="disabled" :options="[({ value: 'cover' as const, label: (t('Fill frame')) }), ({ value: 'contain' as const, label: (t('Show whole image')) })]" /></label><label class="field-label">{{ t("Crop horizontally") }}<input type="range" min="0" max="100" :value="selected.cropX ?? 50" @change="numeric($event,'cropX')"></label><label class="field-label">{{ t("Crop vertically") }}<input type="range" min="0" max="100" :value="selected.cropY ?? 50" @change="numeric($event,'cropY')"></label><label class="field-label">{{ t("Photo zoom") }}<input type="range" min="1" max="5" step="0.05" :value="selected.zoom ?? 1" @change="numeric($event,'zoom')"></label></template><label v-if="selected.type === 'shape'" class="field-label">{{ t("Fill") }}<input type="color" :value="selected.background ?? selected.color ?? '#ffffff'" @input="patchElement({background:text($event)})"></label><div class="inspector-actions"><button @click="moveLayer(-1)">{{ t("Send backward") }}</button><button @click="moveLayer(1)">{{ t("Bring forward") }}</button><button @click="removeElement">{{ t("Remove element") }}</button></div></template><template v-else><p class="inspector-help">{{ t("Click a photo, price, or text on the page to edit it.") }}</p><label v-if="page" class="field-label">{{ t("Page name") }}<input :value="page.title" maxlength="160" @change="renamePage"></label><label v-if="page" class="field-label">{{ t("Page background") }}<input type="color" :value="page.background" @input="updateSnapshot({...store.snapshot.value,pages:store.snapshot.value.pages.map(p=>p.id === page?.id ? {...p,background:text($event)} : p)})"></label></template><div class="inspector-add"><button class="inspector-action" @click="addText">{{ t("+ Add text") }}</button><button class="inspector-action" @click="selectedId = null; photoInput?.click()">{{ t("+ Add photo") }}</button></div><CatalogueTools v-if="kind === 'catalogue'" :snapshot="store.snapshot.value" :page-id="pageId" :selected-id="selectedId" @update:snapshot="updateSnapshot" @update:page-id="pageId = $event" @update:selected-id="selectedId = $event" /><CardTools v-else :snapshot="store.snapshot.value" :page-id="pageId" :selected-id="selectedId" @update:snapshot="updateSnapshot" @update:page-id="pageId = $event" @update:selected-id="selectedId = $event" /></fieldset><div class="inspector-footer"><button class="quiet-button" :disabled="disabled" @click="create">{{ t("New from template") }}</button><button class="quiet-button" :disabled="disabled" @click="fileInput?.click()">{{ t("Import design") }}</button></div></aside>
      </div>
    </template>
    <div v-if="savedLink" class="saved-link-note"><a :href="savedLink" target="_blank" rel="noopener">{{ t("Open saved page in a new tab ↗") }}</a><span>{{ t("Bookmarks open the latest saved revision. Unsaved edits stay in this editor.") }}</span></div>
    <DesignRevisionHistory :access="access" :organization-id="workspace.organization.value!.id" :document="store.record.value" :can-edit="workspace.canEdit.value" :disabled="navigationLocked" @restore="restoreRevision" />
    <CataloguePublication v-if="kind === 'catalogue' && store.snapshot.value" :access="access" :workspace="workspace" :document="store.record.value" :dirty="store.dirty.value" :disabled="!workspace.canEdit.value || store.busy.value || access.busy.value || exporting || preflight.busy.value || store.loading.value" @inspect="inspectIssue" @busy="preparingEdition = $event" />
    <DesignPreflightReview v-if="preflight.review.value" :review="preflight.review.value" @resolve="preflight.resolve" @inspect="inspectIssue" />
    <input ref="fileInput" type="file" accept=".json,application/json" class="sr-only" :aria-label="t('Import editable design')" @change="importFile"><input ref="photoInput" type="file" accept="image/png,image/jpeg" class="sr-only" :aria-label="t('Upload design photo')" @change="upload">
  </section>
</template>

<style scoped>
.catalogue-library { align-items: stretch; }.library-heading { flex-basis: 100%; display: flex; justify-content: space-between; gap: 16px; align-items: end; }.library-heading .field-label { width: min(100%, 300px); margin: 0; }.saved-design { text-decoration: none; min-width: 0; max-width: 100%; overflow-wrap: anywhere; }.saved-design:focus-visible { outline: 2px solid #51674a; outline-offset: 3px; }.saved-design span { font-size: 11px; color: #51674a; }.saved-link-note { display: flex; flex-wrap: wrap; gap: 8px 18px; font-size: 12px; color: #6c6255; margin-top: 18px; }.saved-link-note a { text-decoration: underline; color: #51674a; }
@media(max-width:600px) { .library-heading { flex-direction: column; align-items: stretch; }.library-heading .field-label { width: 100%; } .catalogue-library .saved-design { width: 100%; } }

.page-entry { margin-bottom: 14px; }.page-entry .page-tile { margin-bottom: 3px; }.page-export-choice { display: flex; align-items: center; gap: 5px; font-size: 10px; padding: 0 8px; color: #6c6255; }.page-export-choice input { accent-color: #51674a; }.preview-toggle { border: 1px solid #bfb6a4; border-radius: 5px; padding: 4px 7px; cursor: pointer; font: inherit; color: #514a37; }.stage-toolbar { align-items: center; gap: 8px; flex-wrap: wrap; }.stage-toolbar>span { overflow-wrap: anywhere; }.pdf-scope { display: grid; gap: 4px; font-size: 10px; color: #6c6255; }
@media(max-width:800px) { .page-export-choice { font-size: 8px; padding: 0 2px; }.stage-toolbar { justify-content: flex-start; } }
</style>
