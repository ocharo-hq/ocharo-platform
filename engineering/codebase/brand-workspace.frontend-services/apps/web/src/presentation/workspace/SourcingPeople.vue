<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { notifyError, requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { saveOutcome, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import type { PersonInput, Supplier, SupplierPerson } from '@/domain/workspace/Sourcing';

import SourcingPersonForm from './SourcingPersonForm.vue';

/** Lists the named people behind a supplier's routes. */
defineOptions({ name: 'SourcingPeople' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; supplier: Supplier; reload: () => Promise<boolean>; refreshing: boolean }>();
const { t } = useUiLocale();
const editing = ref<string | null>(null);
const editSnapshot = shallowRef<SupplierPerson | null>(null);
const missingEdit = computed(() => !!editing.value && editing.value !== 'new' && !props.supplier.people.some(item => item.id === editing.value));
function edit(item: SupplierPerson): void {
  editSnapshot.value = { ...item };
  editing.value = item.id;
}
const formVersion = ref(0);
const people = computed(() => missingEdit.value && editSnapshot.value ? [...props.supplier.people, editSnapshot.value] : props.supplier.people);
const routeCount = (person: SupplierPerson) => props.supplier.routes.filter(route => route.personId === person.id).length;

async function save(person: SupplierPerson | null, input: PersonInput, expectedRevision?: number): Promise<SaveOutcome> {
  if (person && expectedRevision === undefined) return 'failed';
  const result = person
    ? await props.sourcing.updatePerson(props.supplier.id, person.id, expectedRevision!, input)
    : await props.sourcing.createPerson(props.supplier.id, input);
  const outcome = saveOutcome(result);
  if (outcome === 'saved') editing.value = null;
  return outcome;
}
async function reloadForm(): Promise<void> {
  if (await props.reload() && !missingEdit.value) formVersion.value += 1;
}
async function remove(person: SupplierPerson): Promise<void> {
  if (!await requestConfirmation('Delete this person? Their contact routes are kept and will no longer name anyone.', { confirmLabel: 'Delete' })) return;
  const result = await props.sourcing.deletePerson(props.supplier.id, person.id, person.revision);
  if (result.ok) return;
  if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  if (result.failure.status === 409 || result.failure.status === 404) await props.sourcing.refreshSupplier(props.supplier.id);
}
</script>

<template>
  <section class="workspace-card space-y-4 p-5 sm:p-6" aria-labelledby="sourcing-people-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h3 id="sourcing-people-title" class="text-lg font-semibold">{{ t("People") }}</h3>
      <Button v-if="sourcing.canEdit.value && editing === null" size="sm" variant="outline" :is-disabled="access.busy.value" @click="editing = 'new'">{{ t("Add person") }}</Button>
    </div>
    <SourcingPersonForm
      v-if="editing === 'new'" :key="`new-${formVersion}`" :sourcing="sourcing" dirty-key="person-new"
      :disabled="access.busy.value || refreshing" :save="input => save(null, input)" @cancel="editing = null" @reload="reloadForm"
    />
    <ul class="divide-y divide-border">
      <li v-for="person in people" :key="person.id" class="space-y-1 py-3 first:pt-0 last:pb-0">
        <SourcingPersonForm
          v-if="editing === person.id" :key="`${person.id}-${formVersion}`" :sourcing="sourcing" :dirty-key="`person-${person.id}`"
          :initial="person" :unavailable="missingEdit" :disabled="access.busy.value || refreshing" :save="(input, revision) => save(person, input, revision)" @cancel="editing = null" @reload="reloadForm"
        />
        <template v-else>
          <p class="break-words font-medium">{{ person.name }}<span v-if="person.role" class="font-normal text-muted-foreground"> · {{ person.role }}</span></p>
          <p class="text-xs text-muted-foreground">{{ t("{count} linked routes", { count: routeCount(person) }) }}</p>
          <p v-if="person.notes" class="whitespace-pre-wrap break-words text-sm">{{ person.notes }}</p>
          <div v-if="sourcing.canEdit.value && editing === null" class="flex flex-wrap gap-2">
            <Button size="sm" variant="ghost" :is-disabled="access.busy.value" @click="edit(person)">{{ t("Edit") }}</Button>
            <Button size="sm" variant="ghost" tone="danger" :is-disabled="access.busy.value" @click="remove(person)">{{ t("Delete") }}</Button>
          </div>
        </template>
      </li>
    </ul>
    <p v-if="!people.length && editing !== 'new'" class="text-sm text-muted-foreground">{{ t("No people recorded yet.") }}</p>
  </section>
</template>
