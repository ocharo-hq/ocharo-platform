<script setup lang="ts">
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { SaveOutcome, SourcingControls } from '@/application/workspace/UseSourcing';
import { optionalText, sourcingLimits, type PersonInput, type SupplierPerson } from '@/domain/workspace/Sourcing';

/** Records a named person behind a supplier's routes. */
defineOptions({ name: 'SourcingPersonForm' });
const props = defineProps<{
  sourcing: SourcingControls;
  dirtyKey: string;
  initial?: SupplierPerson | null | undefined;
  disabled: boolean;
  save: (input: PersonInput) => Promise<SaveOutcome>;
}>();
const emit = defineEmits<{ cancel: []; reload: [] }>();
const { t } = useUiLocale();
const baseline = () => ({ name: props.initial?.name ?? '', role: props.initial?.role ?? '', notes: props.initial?.notes ?? '' });
const draft = reactive(baseline());
const original = JSON.stringify(baseline());
const errors = ref<string[]>([]);
const saving = ref(false);
const conflict = ref(false);
watch(computed(() => JSON.stringify(draft) !== original), value => props.sourcing.setFormDirty(props.dirtyKey, value), { immediate: true });
onScopeDispose(() => props.sourcing.setFormDirty(props.dirtyKey, false));

function validate(): PersonInput | null {
  const problems: string[] = [];
  const name = draft.name.trim();
  if (!name || name.length > sourcingLimits.name) problems.push('Enter a name of up to 160 characters.');
  if (draft.role.trim().length > sourcingLimits.role) problems.push('Role is limited to 120 characters.');
  if (draft.notes.length > sourcingLimits.notes) problems.push('Notes are limited to 4000 characters.');
  errors.value = problems;
  return problems.length ? null : { name, role: optionalText(draft.role), notes: draft.notes.trim() };
}
async function submit(): Promise<void> {
  const input = validate();
  if (!input || saving.value) return;
  saving.value = true;
  try {
    const outcome = await props.save(input);
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') props.sourcing.setFormDirty(props.dirtyKey, false);
  } finally { saving.value = false; }
}
</script>

<template>
  <form class="space-y-4" novalidate @submit.prevent="submit">
    <fieldset :disabled="disabled || saving" class="grid min-w-0 gap-4 sm:grid-cols-2">
      <label class="sourcing-field">{{ t("Person name") }}
        <input v-model="draft.name" class="sourcing-input" required :maxlength="sourcingLimits.name" autocomplete="off" />
      </label>
      <label class="sourcing-field">{{ t("Role") }}
        <input v-model="draft.role" class="sourcing-input" :maxlength="sourcingLimits.role" autocomplete="off" :placeholder="t('e.g. Export manager')" />
      </label>
      <label class="sourcing-field sm:col-span-2">{{ t("Notes") }}
        <textarea v-model="draft.notes" class="sourcing-input" rows="3" :maxlength="sourcingLimits.notes" />
      </label>
    </fieldset>
    <div v-if="conflict" role="alert" class="sourcing-alert space-y-2">
      <p>{{ t("This record changed. Your edits are still here. Reload the latest version before trying again.") }}</p>
      <Button size="sm" variant="outline" :is-disabled="saving" @click="emit('reload')">{{ t("Discard my edits and reload") }}</Button>
    </div>
    <ul v-if="errors.length" role="alert" class="space-y-1">
      <li v-for="error in errors" :key="error" class="form-error">{{ t(error) }}</li>
    </ul>
    <div class="flex flex-wrap gap-3">
      <Button type="submit" size="sm" :is-loading="saving" :is-disabled="disabled">{{ initial ? t("Save person") : t("Add person") }}</Button>
      <Button size="sm" variant="ghost" :is-disabled="saving" @click="emit('cancel')">{{ t("Cancel") }}</Button>
    </div>
  </form>
</template>
