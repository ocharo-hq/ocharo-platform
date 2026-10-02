<script setup lang="ts">
import { requestConfirmation, notifyError } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { catalogueMetadata, eligibleCataloguePdf } from '@/domain/workspace/CatalogueDocuments';
import { useUiLocale } from '@/application/shared/UseUiLocale';
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

const { t } = useUiLocale();

defineOptions({ name: 'CataloguePublication' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; document: DesignDocument | null; dirty: boolean; disabled: boolean }>();
const emit = defineEmits<{ busy: [value: boolean]; inspect: [issue: DesignPreflightIssue] }>();
const preflight = useDesignPreflight();
const api = useCommerce(props.access), controller = new AbortController();
const brandId = ref(''), current = ref<CommerceDraft | null>(null), busy = ref(false), loading = ref(false);
const error = ref(''), message = ref(''), note = ref(''), confirmed = ref(false); let sequence = 0;
const edition = computed(() => current.value?.snapshot.catalogueEditions?.filter(item => item.sourceDesignId === props.document?.id).sort((a,b)=>b.sourceRevision-a.sourceRevision)[0]);
const matching = computed(() => !!edition.value && edition.value.sourceDesignId === props.document?.id && edition.value.sourceRevision === props.document?.revision);
const blocked = computed(() => props.disabled || busy.value || loading.value || props.dirty || !props.document);
watch(busy, value => emit('busy', value), { flush: 'sync' });
watch(error,value=>{if(value)notifyError(value);});
watch(() => props.workspace.brands.value, brands => { if (!brandId.value && brands?.length) brandId.value = props.document?.snapshot.catalogueSettings?.brandId ?? brands[0]!.id; }, { immediate: true });
watch(brandId, () => { void load(); }, { immediate: true });
watch(() => props.document?.id, () => { confirmed.value = false; note.value = ''; });
onScopeDispose(() => { controller.abort(); emit('busy', false); });
async function load(): Promise<void> {
  const request = ++sequence; confirmed.value = false; error.value = ''; message.value = '';
  if (!brandId.value) { current.value=null; return; } if(current.value?.brandId!==brandId.value)current.value=null; loading.value = true;
  try {
    const result = await api.get(props.workspace.organization.value!.id, brandId.value, controller.signal);
    if (controller.signal.aborted || request !== sequence) return;
    if (result.ok) current.value = result.value; else error.value = failureMessage(result.failure);
  } catch { if(!controller.signal.aborted&&request===sequence)error.value=t('Unable to load edition status.'); } finally { if (request === sequence) loading.value = false; }
}
async function upload(): Promise<void> {
  if (blocked.value || !props.document || !current.value) return;
  const saved = props.document, brand = brandId.value, organization = props.workspace.organization.value!.id;
  const expectedRevision = current.value.revision;
  busy.value = true; error.value = ''; confirmed.value = false;
  try {
    message.value = 'Checking all edition pages…';
    const metadata=catalogueMetadata(saved.snapshot);
    const body = new FormData(); body.append('metadata', JSON.stringify({ expectedRevision, sourceDesignId: saved.id, sourceRevision: saved.revision }));
    if(metadata.outputs.pdf){
      const eligible=eligibleCataloguePdf(saved.snapshot,[current.value]);
      if(!eligible.snapshot.pages.length)throw new Error(t('No active, eligible PDF pages are available.'));
      if(eligible.excluded.length&&!await requestConfirmation(t('These pages will be excluded: {pages}. Continue with the remaining pages?',{pages:eligible.excluded.map(page=>page.title).join(', ')})))return;
      if (!await preflight.check(eligible.snapshot, { actionLabel: 'Prepare buyer edition' })) { message.value = 'Edition preparation cancelled. Your saved design is unchanged.'; return; }
      const bundle = await renderCatalogueBundle(eligible.snapshot, saved.title, (page, total) => { message.value = t('Preparing page {page} of {total}…',{page,total}); }, controller.signal);
      body.append('pdf', bundle.pdf, 'pdf');
      for (const page of bundle.pages) { body.append(`page-${page.id}.png`, page.png, `page-${page.id}.png`); body.append(`page-${page.id}.pdf`, page.pdf, `page-${page.id}.pdf`); }
    }
    message.value = 'Saving the private edition…';
    const result = await props.access.write(signal => api.uploadEdition(organization, brand, body, signal), controller.signal);
    if (result.ok) { await load(); message.value = 'Edition saved privately. Review the complete pages before publishing.'; }
    else { error.value = failureMessage(result.failure); message.value = ''; }
  } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Unable to prepare this edition.'; message.value = ''; }
  finally { busy.value = false; }
}
async function publish(): Promise<void> {
  if (blocked.value || !current.value || !props.document || !matching.value || !confirmed.value || !note.value.trim() || !props.workspace.canApprove.value) return;
  busy.value = true; error.value = '';
  try {
    const result = await props.access.write(signal => api.publishEditionById(props.workspace.organization.value!.id, brandId.value,
      edition.value!.id, current.value!.revision, note.value.trim(), signal), controller.signal);
    if (!result.ok) { error.value = failureMessage(result.failure); return; }
    await load(); message.value = t('Catalogue edition published in release {revision}. Buyers receive these exact saved pages.',{revision:result.value.revision});
  } finally { busy.value = false; }
}
</script>

<template>
  <section class="catalogue-publication" :aria-label="t('Buyer catalogue edition')">
    <div><p class="eyebrow">{{ t("Buyer catalogue") }}</p><h2>{{ t("Publish this exact edition.") }}</h2><p>{{ t("Prepare the enabled Web and PDF outputs from this saved revision.") }}</p></div>
    <label class="field-label">{{ t("Publication brand") }}<select v-model="brandId" :aria-label="t('Publication brand')" :disabled="busy || loading"><option value="">{{ t("Choose brand") }}</option><option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">{{ brand.name }}</option></select></label>
    <p v-if="!document || dirty" class="edition-notice">{{ t("Save this design before preparing its buyer edition.") }}</p>
    <p v-else-if="error&&!current" class="edition-notice">{{ t('Edition status is unavailable. Retry before preparing or publishing.') }}</p>
    <ContentSkeleton v-else-if="loading" :cards="1" :label="t('Loading edition status')"/><p v-else-if="!current" class="edition-notice">{{ t("Save the brand’s products in Products & releases before attaching an edition.") }}</p>
    <div class="edition-actions"><Button variant="outline" :is-disabled="blocked || !current || !workspace.canEdit.value" @click="upload">{{ busy ? t("Working…") : t("Prepare buyer edition") }}</Button><button type="button" class="quiet-button" :disabled="busy || loading" @click="load">{{ t("Reload edition status") }}</button></div>
    <p v-if="edition">{{ t("Saved edition:") }} {{ edition.title }} {{ t("· design revision") }} {{ edition.sourceRevision }} · {{ edition.pages.length }} {{ t("pages ·") }} {{ edition.approvedForPublication ? t('approved') : t("awaiting review") }}</p>
    <div v-if="matching && workspace.canApprove.value" class="edition-review">
      <label class="edition-confirm"><input v-model="confirmed" type="checkbox" :disabled="blocked"> {{ t("I reviewed these exact pages, images, contact details and all quantity prices for public download.") }}</label>
      <label class="field-label">{{ t("Publication note") }}<textarea v-model="note" :aria-label="t('Catalogue publication note')" rows="2" maxlength="1000" :disabled="blocked" /></label>
      <Button :is-disabled="blocked || !confirmed || !note.trim()" @click="publish">{{ t("Publish buyer edition") }}</Button>
    </div>
    <DesignPreflightReview v-if="preflight.review.value" :review="preflight.review.value" @resolve="preflight.resolve" @inspect="emit('inspect', $event)" />
    <p v-if="message" role="status">{{ t(message) }}</p>
  </section>
</template>

<style scoped>
.catalogue-publication { background: var(--color-card,#fffdf8); border: 1px solid var(--color-border,#e3ddd2); border-radius: 12px; padding: 24px; margin-top: 24px; display: grid; gap: 12px; }.catalogue-publication h2 { font: 25px Georgia,serif; margin: 7px 0; }.catalogue-publication p { font-size: 13px; line-height: 1.6; margin: 4px 0; }.catalogue-publication>.field-label { max-width: 430px; }.edition-actions { display: flex; flex-wrap: wrap; gap: 8px; }.edition-review { border-top: 1px solid var(--color-border,#e3ddd2); padding-top: 16px; }.edition-confirm { display: flex; gap: 10px; align-items: start; font-size: 13px; line-height: 1.6; }.edition-confirm input { margin-top: 4px; }.edition-notice { color: var(--color-muted-foreground,#7b613e); }
</style>
