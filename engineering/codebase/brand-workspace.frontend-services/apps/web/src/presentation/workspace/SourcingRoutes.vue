<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge } from '@wow-two-beta/fe/presentation/display';

import { notifyError, requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { saveOutcome, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import { channelLabels, routeHref, safeHttpUrl, type ContactRoute, type RouteInput, type Supplier } from '@/domain/workspace/Sourcing';

import SourcingRouteForm from './SourcingRouteForm.vue';

/** Lists the recorded ways to reach a supplier. Links open the operator's own apps; Desk sends nothing. */
defineOptions({ name: 'SourcingRoutes' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; supplier: Supplier; reload: () => Promise<boolean>; refreshing: boolean }>();
const { t } = useUiLocale();
const editing = ref<string | null>(null);
const editSnapshot = shallowRef<ContactRoute | null>(null);
const missingEdit = computed(() => !!editing.value && editing.value !== 'new' && !props.supplier.routes.some(item => item.id === editing.value));
function edit(item: ContactRoute): void {
  editSnapshot.value = { ...item };
  editing.value = item.id;
}
const formVersion = ref(0);
const routes = computed(() => {
  const visible = missingEdit.value && editSnapshot.value ? [...props.supplier.routes, editSnapshot.value] : props.supplier.routes;
  return [...visible].sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary));
});
const personName = (id: string | null) => props.supplier.people.find(person => person.id === id)?.name ?? null;

async function save(route: ContactRoute | null, input: RouteInput, expectedRevision?: number): Promise<SaveOutcome> {
  if (route && expectedRevision === undefined) return 'failed';
  const result = route
    ? await props.sourcing.updateRoute(props.supplier.id, route.id, expectedRevision!, input)
    : await props.sourcing.createRoute(props.supplier.id, input);
  const outcome = saveOutcome(result);
  if (outcome === 'saved') editing.value = null;
  return outcome;
}
async function reloadForm(): Promise<void> {
  if (await props.reload() && !missingEdit.value) formVersion.value += 1;
}
async function remove(route: ContactRoute): Promise<void> {
  if (!await requestConfirmation('Delete this contact route? This cannot be undone.', { confirmLabel: 'Delete' })) return;
  const result = await props.sourcing.deleteRoute(props.supplier.id, route.id, route.revision);
  if (result.ok) return;
  if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  if (result.failure.status === 409 || result.failure.status === 404) await props.sourcing.refreshSupplier(props.supplier.id);
}
</script>

<template>
  <section class="workspace-card space-y-4 p-5 sm:p-6" aria-labelledby="sourcing-routes-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h3 id="sourcing-routes-title" class="text-lg font-semibold">{{ t("Contacts") }}</h3>
      <Button v-if="sourcing.canEdit.value && editing === null" size="sm" variant="outline" :is-disabled="access.busy.value" @click="editing = 'new'">{{ t("Add route") }}</Button>
    </div>
    <SourcingRouteForm
      v-if="editing === 'new'" :key="`new-${formVersion}`" :sourcing="sourcing" dirty-key="route-new" :people="supplier.people"
      :disabled="access.busy.value || refreshing" :save="input => save(null, input)" @cancel="editing = null" @reload="reloadForm"
    />
    <ul class="divide-y divide-border">
      <li v-for="route in routes" :key="route.id" class="space-y-2 py-3 first:pt-0 last:pb-0">
        <SourcingRouteForm
          v-if="editing === route.id" :key="`${route.id}-${formVersion}`" :sourcing="sourcing" :dirty-key="`route-${route.id}`"
          :people="supplier.people" :initial="route" :unavailable="missingEdit" :disabled="access.busy.value || refreshing" :save="(input, revision) => save(route, input, revision)"
          @cancel="editing = null" @reload="reloadForm"
        />
        <template v-else>
          <div class="flex flex-wrap items-center gap-2">
            <Badge size="sm" variant="outline">{{ t(channelLabels[route.channel]) }}</Badge>
            <Badge v-if="route.isPrimary" size="sm" variant="brand">{{ t("Primary") }}</Badge>
            <Badge v-if="route.verified" size="sm" variant="success">{{ t("Verified") }}</Badge>
            <span v-else class="text-xs text-muted-foreground">{{ t("Not verified") }}</span>
          </div>
          <p class="min-w-0 break-all">
            <a
              v-if="routeHref(route.channel, route.destination)" :href="routeHref(route.channel, route.destination)!"
              :target="routeHref(route.channel, route.destination)!.startsWith('http') ? '_blank' : undefined" rel="noopener noreferrer"
              class="font-medium text-primary underline-offset-4 hover:underline"
            >{{ route.destination }}</a>
            <span v-else class="font-medium">{{ route.destination }}</span>
          </p>
          <p v-if="route.label || personName(route.personId)" class="text-sm text-muted-foreground">
            {{ [personName(route.personId), route.label].filter(Boolean).join(' · ') }}
          </p>
          <p v-if="route.notes" class="whitespace-pre-wrap break-words text-sm">{{ route.notes }}</p>
          <div class="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
            <a v-if="safeHttpUrl(route.sourceUrl)" :href="safeHttpUrl(route.sourceUrl)!" target="_blank" rel="noopener noreferrer" class="text-muted-foreground underline underline-offset-4">{{ t("Where this contact was found") }}</a>
            <template v-if="sourcing.canEdit.value && editing === null">
              <Button size="sm" variant="ghost" :is-disabled="access.busy.value" @click="edit(route)">{{ t("Edit") }}</Button>
              <Button size="sm" variant="ghost" tone="danger" :is-disabled="access.busy.value" @click="remove(route)">{{ t("Delete") }}</Button>
            </template>
          </div>
        </template>
      </li>
    </ul>
    <p v-if="!routes.length && editing !== 'new'" class="text-sm text-muted-foreground">{{ t("No contact routes recorded yet.") }}</p>
    <p class="text-xs leading-5 text-muted-foreground">{{ t("Links open your own apps. Desk never sends messages to suppliers.") }}</p>
  </section>
</template>
