<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import McpAccessPanel from './McpAccessPanel.vue';
import SourceDirectoryPanel from './SourceDirectoryPanel.vue';
import { requestConfirmation, useErrorToast } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { connectedApps, connectionUrl, type ConnectedApp } from '@/domain/workspace/ConnectedApps';
import type { CreativeImport, Product } from '@/domain/workspace/Commerce';
import { useCommerce } from '@/application/workspace/UseCommerce';

const { t, uiLocale } = useUiLocale();

defineOptions({ name: 'IntegrationsPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const api = useCommerce(props.access);
const controller = new AbortController();
const apps = connectedApps(window.location.origin);
const error = ref('');
useErrorToast(error);
let loadedBrand = '';
const connecting = ref('');
const brandId = ref('');
const imports = ref<CreativeImport[]>([]);
const loading = ref(false);
const hasLoaded = ref(false);
const products = ref<Product[]>([]);
const productId = ref('');
const manifestFile = ref<File | null>(null);
const imageFile = ref<File | null>(null);
const importing = ref(false);
const message = ref('');
const fileSelection = ref(0);
let loadSequence = 0;
const blocked = computed(() => props.access.busy.value || connecting.value !== '' || importing.value);
watch(() => manifestFile.value !== null || imageFile.value !== null, dirty => props.access.setDirty(dirty), { flush: 'sync' });
onScopeDispose(() => { controller.abort(); props.access.setDirty(false); });
watch(() => props.workspace.brands.value, brands => {
  if (!brandId.value && brands?.length) brandId.value = brands[0]!.id;
}, { immediate: true });
watch(brandId, () => void loadImports(), { immediate: true });
async function connect(app: ConnectedApp): Promise<void> {
  if ((manifestFile.value || imageFile.value) && !await requestConfirmation(t('Discard the selected import files and open this app?'), {confirmLabel:t('Discard changes')})) return;
  const organization = props.workspace.organization.value?.id;
  if (!organization || blocked.value || controller.signal.aborted) return;
  error.value = '';

  connecting.value = app.id;
  try {
    const result = await props.access.write(signal => api.ticket(organization, app.audience, signal), controller.signal);
    if (controller.signal.aborted) return;
    if (!result.ok) { error.value = failureMessage(result.failure); return; }
    if (props.workspace.organization.value?.id !== organization) return;
    window.location.assign(connectionUrl(app.id, window.location.origin, result.value.token));
  } finally { connecting.value = ''; }
}
async function loadImports(): Promise<void> {
  const organization = props.workspace.organization.value?.id;
  if (!organization || !brandId.value) return;
  const selected = brandId.value;
  const sequence = ++loadSequence;
  loading.value = true;
  if (loadedBrand !== selected) { imports.value = []; products.value = []; productId.value = ''; loadedBrand = selected; hasLoaded.value = false; }
  const [result, draft] = await Promise.all([api.imports(organization, selected, controller.signal), api.get(organization, selected, controller.signal)]);
  if (controller.signal.aborted || sequence !== loadSequence) return;
  loading.value = false;
  if (draft.ok) products.value = draft.value?.snapshot.products ?? [];
  else error.value = failureMessage(draft.failure);
  if (!products.value.some(item => item.document.id === productId.value)) productId.value = '';
  if (result.ok) { imports.value = result.value; hasLoaded.value = true; }
  else error.value = failureMessage(result.failure);
}
function selectFile(event: Event, kind: 'manifest' | 'image'): void {
  const file = (event.target as HTMLInputElement).files?.[0] ?? null;
  if (kind === 'manifest') manifestFile.value = file; else imageFile.value = file;
  message.value = '';
}
async function importFiles(): Promise<void> {
  const organization = props.workspace.organization.value?.id;
  if (!organization || !brandId.value || !manifestFile.value || !imageFile.value || blocked.value) return;
  importing.value = true;
  error.value = '';

  try {
    await api.importPackage(props.workspace, organization, brandId.value, productId.value || null, manifestFile.value, imageFile.value, controller.signal);
    if (controller.signal.aborted) return;
    await loadImports();
    manifestFile.value = null; imageFile.value = null; fileSelection.value += 1;
    message.value = 'Studio image imported for review. Attach it from Media to a product, then review its provenance before publication.';
  } catch {
    if (!controller.signal.aborted) {
      error.value = 'The Studio import could not complete.';
    }
  }
  finally { importing.value = false; }
}
</script>

<template>
  <section class="space-y-7">
    <header class="space-y-2"><p class="eyebrow">{{ t("Your creative workspace") }}</p><h1 class="page-title">{{ t("Integrations") }}</h1><p class="max-w-2xl text-muted-foreground">{{ t("Open an app with your current organization. Access lasts five minutes; reconnect here when it expires.") }}</p></header>

    <SourceDirectoryPanel />
    <McpAccessPanel :access="access" :workspace="workspace" />

    <p v-if="message" role="status" class="rounded-lg bg-muted p-4 text-sm">{{ t(message) }}</p>
    <div class="grid gap-5 lg:grid-cols-3">
      <article v-for="app in apps" :key="app.id" class="workspace-card flex flex-col gap-4 p-6">
        <h2 class="text-xl font-semibold">{{ app.name }}</h2><p class="flex-1 text-sm text-muted-foreground">{{ t(app.description) }}</p>
        <p class="text-xs text-muted-foreground">{{ workspace.organization.value?.name }} · {{ t(workspace.organization.value?.role ?? '') }}</p>
        <Button :is-disabled="blocked" @click="connect(app)">{{ connecting === app.id ? t('Connecting…') : t('Connect to {name}', {name:app.name}) }}</Button>
      </article>
    </div>
    <p v-if="!apps.length" role="status" class="workspace-card p-5">{{ t("Connected apps are available from the configured local Ocharo workspace.") }}</p>
    <section class="space-y-4 border-t border-border pt-6">
      <div class="flex flex-wrap items-end justify-between gap-4"><div><h2 class="text-xl font-semibold">{{ t("Creative imports") }}</h2><p class="mt-1 text-sm text-muted-foreground">{{ t("Studio renders arrive as unreviewed images. Review their source and rights before adding them to your catalogue.") }}</p></div>
        <Button variant="outline" :is-disabled="blocked||loading||!brandId" :is-loading="loading" @click="loadImports">{{t("Refresh")}}</Button><label>{{ t("Brand") }}<DeskSelect v-model="brandId" :disabled="blocked" class="mt-2" :label="t('Brand')" :options="[({ value: '' as const, label: (t('Choose a brand')) }), ...(workspace.brands.value ?? []).map(brand => ({ value: brand.id, label: (brand.name) }))]" /></label>
      </div>
      <form v-if="workspace.canEdit.value && brandId" class="workspace-card grid gap-4 p-5 sm:grid-cols-2" @submit.prevent="importFiles">
        <label class="flex min-w-0 flex-col gap-2">{{ t("Studio manifest") }}<input :key="`manifest-${fileSelection}`" type="file" accept="application/json,.json" :disabled="blocked" class="block w-full min-w-0 rounded-lg border border-border bg-background p-3 text-sm file:mr-3" @change="selectFile($event, 'manifest')" /></label>
        <label class="flex min-w-0 flex-col gap-2">{{ t("Exported image") }}<input :key="`image-${fileSelection}`" type="file" accept="image/png,image/jpeg" :disabled="blocked" class="block w-full min-w-0 rounded-lg border border-border bg-background p-3 text-sm file:mr-3" @change="selectFile($event, 'image')" /></label>
        <label>{{ t("Product (optional)") }}<DeskSelect v-model="productId" :disabled="blocked" class="mt-2" :label="t('Product (optional)')" :options="[({ value: '' as const, label: (t('Assign during review')) }), ...(products).map(product => ({ value: product.document.id, label: (product.document.locales.en.name || product.document.id) }))]" /></label>
        <div class="flex items-end"><Button type="submit" :is-disabled="blocked || !manifestFile || !imageFile">{{ t(importing ? 'Importing…' : 'Import Studio image') }}</Button></div>
        <p class="text-xs text-muted-foreground sm:col-span-2">{{ t("Choose both files downloaded by Studio. The image fingerprint must match. Studio's author submission does not approve catalogue publication.") }}</p>
      </form>
      <ContentSkeleton v-if="loading&&!hasLoaded" variant="imports" :cards="2" :label="t('Loading creative imports…')"/>
      <p v-else-if="!hasLoaded" role="status" class="query-label">{{t("Content unavailable. Try refreshing.")}}</p>
      <p v-else-if="!imports.length" class="empty-state">{{ t("No Studio renders have been imported for this brand.") }}</p>
      <div v-else :aria-busy="loading" class="grid gap-4 sm:grid-cols-2"><article v-for="item in imports" :key="item.id" class="workspace-card space-y-3 p-5">
        <img :src="`/api/organizations/${workspace.organization.value!.id}/media/${item.input.derivative.mediaId}/content`" :alt="t('Unreviewed Studio visualization')" class="h-48 w-full rounded-lg bg-muted object-contain"/>
        <h3 class="font-semibold">{{ item.input.productId || t('Unassigned render') }}</h3><p class="text-sm">{{ t(item.status) }} · {{ new Date(item.createdAt).toLocaleString(uiLocale) }}</p>
        <p class="text-xs text-muted-foreground">{{ t("Look") }} {{ item.input.source.lookId }} {{ t("· Revision") }} {{ item.input.source.revision }}</p>
        <p class="text-sm">{{ item.input.assetReleases.length }} {{ t("asset releases ·") }} {{ item.input.materials.length }} {{ t("materials recorded") }}</p>
        <Button variant="outline" size="sm" @click="access.navigate(`/organizations/${workspace.organization.value!.id}/products`)">{{ t("Review in Products") }}</Button>
      </article></div>
    </section>
  </section>
</template>
