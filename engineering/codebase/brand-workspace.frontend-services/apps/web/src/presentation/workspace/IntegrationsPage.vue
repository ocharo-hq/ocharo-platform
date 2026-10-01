<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { connectedApps, connectionUrl, type ConnectedApp } from '@/domain/workspace/ConnectedApps';
import type { CreativeImport, Product } from '@/domain/workspace/Commerce';
import { useCommerce } from '@/application/workspace/UseCommerce';

defineOptions({ name: 'IntegrationsPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const api = useCommerce(props.access);
const controller = new AbortController();
const apps = connectedApps(window.location.origin);
const error = ref('');
const connecting = ref('');
const brandId = ref('');
const imports = ref<CreativeImport[]>([]);
const loading = ref(false);
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
  if ((manifestFile.value || imageFile.value) && !window.confirm('Discard the selected import files and open this app?')) return;
  const organization = props.workspace.organization.value?.id;
  if (!organization || blocked.value) return;
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
  imports.value = [];
  const [result, draft] = await Promise.all([api.imports(organization, selected, controller.signal), api.get(organization, selected, controller.signal)]);
  if (controller.signal.aborted || sequence !== loadSequence) return;
  loading.value = false;
  products.value = draft.ok ? draft.value?.snapshot.products ?? [] : [];
  productId.value = '';
  if (result.ok) imports.value = result.value;
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
    const imported = await api.importPackage(props.workspace, organization, brandId.value, productId.value || null, manifestFile.value, imageFile.value, controller.signal);
    if (controller.signal.aborted) return;
    imports.value.unshift(imported);
    manifestFile.value = null; imageFile.value = null; fileSelection.value += 1;
    message.value = 'Studio image imported for review. Attach it from Media to a product, then review its provenance before publication.';
  } catch (failure) { if (!controller.signal.aborted) error.value = failure instanceof Error ? failure.message : 'The Studio import could not complete.'; }
  finally { importing.value = false; }
}
</script>

<template>
  <section class="space-y-7">
    <header class="space-y-2"><p class="eyebrow">Your creative workspace</p><h1 class="page-title">Connected apps</h1><p class="max-w-2xl text-muted-foreground">Open an app with your current organization. Access lasts five minutes; reconnect here when it expires.</p></header>
    <p v-if="error" role="alert" class="form-error">{{ error }}</p>
    <p v-if="message" role="status" class="rounded-lg bg-muted p-4 text-sm">{{ message }}</p>
    <div class="grid gap-5 lg:grid-cols-3">
      <article v-for="app in apps" :key="app.id" class="workspace-card flex flex-col gap-4 p-6">
        <h2 class="text-xl font-semibold">{{ app.name }}</h2><p class="flex-1 text-sm text-muted-foreground">{{ app.description }}</p>
        <p class="text-xs text-muted-foreground">{{ workspace.organization.value?.name }} · {{ workspace.organization.value?.role }}</p>
        <Button :is-disabled="blocked" @click="connect(app)">{{ connecting === app.id ? 'Connecting…' : `Connect to ${app.name}` }}</Button>
      </article>
    </div>
    <p v-if="!apps.length" role="status" class="workspace-card p-5">Connected apps are available from the configured local Ocharo workspace.</p>
    <section class="space-y-4 border-t border-border pt-6">
      <div class="flex flex-wrap items-end justify-between gap-4"><div><h2 class="text-xl font-semibold">Creative imports</h2><p class="mt-1 text-sm text-muted-foreground">Studio renders arrive as unreviewed images. Review their source and rights before adding them to your catalogue.</p></div>
        <label>Brand<select v-model="brandId" :disabled="blocked" class="native-select mt-2"><option value="">Choose a brand</option><option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">{{ brand.name }}</option></select></label>
      </div>
      <form v-if="workspace.canEdit.value && brandId" class="workspace-card grid gap-4 p-5 sm:grid-cols-2" @submit.prevent="importFiles">
        <label class="flex min-w-0 flex-col gap-2">Studio manifest<input :key="`manifest-${fileSelection}`" type="file" accept="application/json,.json" :disabled="blocked" class="block w-full min-w-0 rounded-lg border border-border bg-background p-3 text-sm file:mr-3" @change="selectFile($event, 'manifest')" /></label>
        <label class="flex min-w-0 flex-col gap-2">Exported image<input :key="`image-${fileSelection}`" type="file" accept="image/png,image/jpeg" :disabled="blocked" class="block w-full min-w-0 rounded-lg border border-border bg-background p-3 text-sm file:mr-3" @change="selectFile($event, 'image')" /></label>
        <label>Product (optional)<select v-model="productId" :disabled="blocked" class="native-select mt-2"><option value="">Assign during review</option><option v-for="product in products" :key="product.document.id" :value="product.document.id">{{ product.document.locales.en.name || product.document.id }}</option></select></label>
        <div class="flex items-end"><Button type="submit" :is-disabled="blocked || !manifestFile || !imageFile">{{ importing ? 'Importing…' : 'Import Studio image' }}</Button></div>
        <p class="text-xs text-muted-foreground sm:col-span-2">Choose both files downloaded by Studio. The image fingerprint must match. Studio's author submission does not approve catalogue publication.</p>
      </form>
      <p v-if="loading" role="status">Loading creative imports…</p>
      <p v-else-if="!imports.length" class="empty-state">No Studio renders have been imported for this brand.</p>
      <div v-else class="grid gap-4 sm:grid-cols-2"><article v-for="item in imports" :key="item.id" class="workspace-card space-y-3 p-5">
        <img :src="`/api/organizations/${workspace.organization.value!.id}/media/${item.input.derivative.mediaId}/content`" alt="Unreviewed Studio visualization" class="h-48 w-full rounded-lg bg-muted object-contain"/>
        <h3 class="font-semibold">{{ item.input.productId || 'Unassigned render' }}</h3><p class="text-sm">{{ item.status }} · {{ new Date(item.createdAt).toLocaleString() }}</p>
        <p class="text-xs text-muted-foreground">Look {{ item.input.source.lookId }} · Revision {{ item.input.source.revision }}</p>
        <p class="text-sm">{{ item.input.assetReleases.length }} asset releases · {{ item.input.materials.length }} materials recorded</p>
        <Button variant="outline" size="sm" @click="access.navigate(`/organizations/${workspace.organization.value!.id}/products`)">Review in Products</Button>
      </article></div>
    </section>
  </section>
</template>
