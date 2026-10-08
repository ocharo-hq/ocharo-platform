<script setup lang="ts">
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { SaveOutcome, SourcingControls } from '@/application/workspace/UseSourcing';
import {
  optionalText, safeHttpUrl, sourcingLimits, stageLabels, supplierStages,
  type Supplier, type SupplierInput, type SupplierStage,
} from '@/domain/workspace/Sourcing';

/** Creates or edits a supplier's own fields; contacts and people have their own cards. */
defineOptions({ name: 'SourcingSupplierForm' });
const props = defineProps<{
  sourcing: SourcingControls;
  dirtyKey: string;
  initial?: Supplier | null | undefined;
  disabled: boolean;
  submitLabel: string;
  save: (input: SupplierInput) => Promise<SaveOutcome>;
}>();
const emit = defineEmits<{ cancel: []; reload: [] }>();
const { t } = useUiLocale();
const baseline = () => ({
  name: props.initial?.name ?? '', stage: (props.initial?.stage ?? 'lead') as SupplierStage,
  country: props.initial?.country ?? '', city: props.initial?.city ?? '', website: props.initial?.website ?? '', notes: props.initial?.notes ?? '',
});
const draft = reactive(baseline());
const original = JSON.stringify(baseline());
const errors = ref<string[]>([]);
const saving = ref(false);
const conflict = ref(false);
const dirty = computed(() => JSON.stringify(draft) !== original);
watch(dirty, value => props.sourcing.setFormDirty(props.dirtyKey, value), { immediate: true });
onScopeDispose(() => props.sourcing.setFormDirty(props.dirtyKey, false));

function validate(): SupplierInput | null {
  const problems: string[] = [];
  const name = draft.name.trim();
  const country = draft.country.trim().toUpperCase();
  const website = draft.website.trim();
  if (!name || name.length > sourcingLimits.name) problems.push('Enter a name of up to 160 characters.');
  if (country && !/^[A-Z]{2}$/.test(country)) problems.push('Use a two-letter country code, for example IN or UZ.');
  if (draft.city.trim().length > sourcingLimits.city) problems.push('City is limited to 120 characters.');
  if (website && (!safeHttpUrl(website) || website.length > sourcingLimits.url)) problems.push('Enter the website as a full http:// or https:// address.');
  if (draft.notes.length > sourcingLimits.supplierNotes) problems.push('Notes are limited to 8000 characters.');
  errors.value = problems;
  return problems.length ? null : { name, stage: draft.stage, country: country || null, city: optionalText(draft.city), website: website || null, notes: draft.notes.trim() };
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
      <label class="sourcing-field sm:col-span-2">{{ t("Supplier name") }}
        <input v-model="draft.name" class="sourcing-input" required :maxlength="sourcingLimits.name" autocomplete="off" />
      </label>
      <label class="sourcing-field">{{ t("Stage") }}
        <select v-model="draft.stage" class="sourcing-input">
          <option v-for="stage in supplierStages" :key="stage" :value="stage">{{ t(stageLabels[stage]) }}</option>
        </select>
      </label>
      <label class="sourcing-field">{{ t("Country code") }}
        <input v-model="draft.country" class="sourcing-input uppercase placeholder:normal-case" maxlength="2" autocomplete="off" :placeholder="t('e.g. IN')" />
      </label>
      <label class="sourcing-field">{{ t("City") }}
        <input v-model="draft.city" class="sourcing-input" :maxlength="sourcingLimits.city" autocomplete="off" />
      </label>
      <label class="sourcing-field">{{ t("Website") }}
        <input v-model="draft.website" type="url" class="sourcing-input" :maxlength="sourcingLimits.url" placeholder="https://" autocomplete="off" />
      </label>
      <label class="sourcing-field sm:col-span-2">{{ t("Notes") }}
        <textarea v-model="draft.notes" class="sourcing-input" rows="4" :maxlength="sourcingLimits.supplierNotes" />
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
      <Button type="submit" size="sm" :is-loading="saving" :is-disabled="disabled">{{ submitLabel }}</Button>
      <Button size="sm" variant="ghost" :is-disabled="saving" @click="emit('cancel')">{{ t("Cancel") }}</Button>
    </div>
  </form>
</template>
