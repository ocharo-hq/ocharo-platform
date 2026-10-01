<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { useDesignPreflight } from '@/application/workspace/UseDesignPreflight';
import type { DesignPreflightIssue } from '@/domain/workspace/DesignPreflight';
import DesignPreflightReview from './DesignPreflightReview.vue';
import { useCommerce } from '@/application/workspace/UseCommerce';
import { renderCatalogueBundle } from '@/application/workspace/ExportDocument';
import type { CommerceDraft } from '@/domain/workspace/Commerce';
import type { DesignDocument } from '@/domain/workspace/EditorModels';

defineOptions({ name: 'CataloguePublication' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; document: DesignDocument | null; dirty: boolean; disabled: boolean }>();
const emit = defineEmits<{ busy: [value: boolean]; inspect: [issue: DesignPreflightIssue] }>();
const preflight = useDesignPreflight();
const api = useCommerce(props.access), controller = new AbortController();
const brandId = ref(''), current = ref<CommerceDraft | null>(null), busy = ref(false), loading = ref(false);
const error = ref(''), message = ref(''), note = ref(''), confirmed = ref(false); let sequence = 0;
const edition = computed(() => current.value?.snapshot.catalogueEditions?.find(item => item.locale === props.document?.snapshot.locale));
const matching = computed(() => !!edition.value && edition.value.sourceDesignId === props.document?.id && edition.value.sourceRevision === props.document?.revision);
const blocked = computed(() => props.disabled || busy.value || loading.value || props.dirty || !props.document);
watch(busy, value => emit('busy', value), { flush: 'sync' });
watch(() => props.workspace.brands.value, brands => { if (!brandId.value && brands?.length) brandId.value = brands[0]!.id; }, { immediate: true });
watch(brandId, () => { void load(); }, { immediate: true });
watch(() => props.document?.id, () => { confirmed.value = false; note.value = ''; });
onScopeDispose(() => { controller.abort(); emit('busy', false); });
async function load(): Promise<void> {
  const request = ++sequence; current.value = null; confirmed.value = false; error.value = ''; message.value = '';
  if (!brandId.value) return; loading.value = true;
  try {
    const result = await api.get(props.workspace.organization.value!.id, brandId.value, controller.signal);
    if (controller.signal.aborted || request !== sequence) return;
    if (result.ok) current.value = result.value; else error.value = failureMessage(result.failure);
  } finally { if (request === sequence) loading.value = false; }
}
async function upload(): Promise<void> {
  if (blocked.value || !props.document || !current.value) return;
  const saved = props.document, brand = brandId.value, organization = props.workspace.organization.value!.id;
  const expectedRevision = current.value.revision;
  busy.value = true; error.value = ''; confirmed.value = false;
  try {
    message.value = 'Checking all edition pages…';
    if (!await preflight.check(saved.snapshot, { actionLabel: 'Prepare buyer edition' })) { message.value = 'Edition preparation cancelled. Your saved design is unchanged.'; return; }
    const bundle = await renderCatalogueBundle(saved.snapshot, saved.title, (page, total) => { message.value = `Preparing page ${page} of ${total}…`; }, controller.signal);
    const body = new FormData(); body.append('metadata', JSON.stringify({ expectedRevision, sourceDesignId: saved.id, sourceRevision: saved.revision }));
    body.append('pdf', bundle.pdf, 'pdf');
    for (const page of bundle.pages) { body.append(`page-${page.id}.png`, page.png, `page-${page.id}.png`); body.append(`page-${page.id}.pdf`, page.pdf, `page-${page.id}.pdf`); }
    message.value = 'Saving the private edition…';
    const result = await props.access.write(signal => api.uploadEdition(organization, brand, body, signal), controller.signal);
    if (result.ok) { current.value = result.value; message.value = 'Edition saved privately. Review the complete pages before publishing.'; }
    else { error.value = failureMessage(result.failure); message.value = ''; }
  } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Unable to prepare this edition.'; message.value = ''; }
  finally { busy.value = false; }
}
async function publish(): Promise<void> {
  if (blocked.value || !current.value || !props.document || !matching.value || !confirmed.value || !note.value.trim() || !props.workspace.canApprove.value) return;
  busy.value = true; error.value = '';
  try {
    const result = await props.access.write(signal => api.publishEdition(props.workspace.organization.value!.id, brandId.value,
      props.document!.snapshot.locale, current.value!.revision, note.value.trim(), signal), controller.signal);
    if (!result.ok) { error.value = failureMessage(result.failure); return; }
    await load(); message.value = `Catalogue edition published in release ${result.value.revision}. Buyers receive these exact saved pages.`;
  } finally { busy.value = false; }
}
</script>

<template>
  <section class="catalogue-publication" aria-label="Buyer catalogue edition">
    <div><p class="eyebrow">Buyer catalogue</p><h2>Publish this exact edition.</h2><p>Buyers see the same pages and can download the complete PDF or individual pages.</p></div>
    <label class="field-label">Publication brand<select v-model="brandId" aria-label="Publication brand" :disabled="busy || loading"><option value="">Choose brand</option><option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">{{ brand.name }}</option></select></label>
    <p v-if="!document || dirty" class="edition-notice">Save this design before preparing its buyer edition.</p>
    <p v-else-if="!current" class="edition-notice">Save the brand’s products in Products &amp; releases before attaching an edition.</p>
    <div class="edition-actions"><Button variant="outline" :is-disabled="blocked || !current || !workspace.canEdit.value" @click="upload">{{ busy ? 'Working…' : 'Prepare buyer edition' }}</Button><button type="button" class="quiet-button" :disabled="busy || loading" @click="load">Reload edition status</button></div>
    <p v-if="edition">Saved edition: {{ edition.title }} · design revision {{ edition.sourceRevision }} · {{ edition.pages.length }} pages · {{ edition.approvedForPublication ? 'approved' : 'awaiting review' }}</p>
    <div v-if="matching && workspace.canApprove.value" class="edition-review">
      <label class="edition-confirm"><input v-model="confirmed" type="checkbox" :disabled="blocked"> I reviewed these exact pages, images, contact details and all quantity prices for public download.</label>
      <label class="field-label">Publication note<textarea v-model="note" aria-label="Catalogue publication note" rows="2" maxlength="1000" :disabled="blocked" /></label>
      <Button :is-disabled="blocked || !confirmed || !note.trim()" @click="publish">Publish buyer edition</Button>
    </div>
    <DesignPreflightReview v-if="preflight.review.value" :review="preflight.review.value" @resolve="preflight.resolve" @inspect="emit('inspect', $event)" />
    <p v-if="message" role="status">{{ message }}</p><p v-if="error" role="alert" class="editor-alert">{{ error }}</p>
  </section>
</template>

<style scoped>
.catalogue-publication { background: #fffdf8; border: 1px solid #e3ddd2; border-radius: 12px; padding: 24px; margin-top: 24px; display: grid; gap: 12px; }.catalogue-publication h2 { font: 25px Georgia,serif; margin: 7px 0; }.catalogue-publication p { font-size: 13px; line-height: 1.6; margin: 4px 0; }.catalogue-publication>.field-label { max-width: 430px; }.edition-actions { display: flex; flex-wrap: wrap; gap: 8px; }.edition-review { border-top: 1px solid #e3ddd2; padding-top: 16px; }.edition-confirm { display: flex; gap: 10px; align-items: start; font-size: 13px; line-height: 1.6; }.edition-confirm input { margin-top: 4px; }.edition-notice { color: #7b613e; }
</style>
