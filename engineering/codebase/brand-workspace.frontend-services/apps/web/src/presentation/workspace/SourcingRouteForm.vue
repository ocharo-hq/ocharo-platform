<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { SaveOutcome, SourcingControls } from '@/application/workspace/UseSourcing';
import {
  channelLabels, contactChannels, optionalText, safeHttpUrl, sourcingLimits,
  type ContactChannel, type ContactRoute, type RouteInput, type SupplierPerson,
} from '@/domain/workspace/Sourcing';

/** Records one way to reach a supplier. Saving never contacts anyone. */
defineOptions({ name: 'SourcingRouteForm' });
const props = defineProps<{
  sourcing: SourcingControls;
  dirtyKey: string;
  people: readonly SupplierPerson[];
  initial?: ContactRoute | null | undefined;
  disabled: boolean;
  unavailable?: boolean;
  save: (input: RouteInput, expectedRevision?: number) => Promise<SaveOutcome>;
}>();
const emit = defineEmits<{ cancel: []; reload: [] }>();
const { t } = useUiLocale();
const baseline = () => ({
  channel: (props.initial?.channel ?? 'whatsapp') as ContactChannel, destination: props.initial?.destination ?? '',
  label: props.initial?.label ?? '', sourceUrl: props.initial?.sourceUrl ?? '', verified: props.initial?.verified ?? false,
  isPrimary: props.initial?.isPrimary ?? false, personId: props.initial?.personId ?? '', notes: props.initial?.notes ?? '',
});
const draft = reactive(baseline());
const original = JSON.stringify(baseline());
// The draft belongs to this revision until an explicit successful reload remounts it.
const expectedRevision = props.initial?.revision;
const errors = ref<string[]>([]);
const saving = ref(false);
const conflict = ref(false);
watch(computed(() => JSON.stringify(draft) !== original), value => props.sourcing.setFormDirty(props.dirtyKey, value), { immediate: true });
onScopeDispose(() => props.sourcing.setFormDirty(props.dirtyKey, false));

function validate(): RouteInput | null {
  const problems: string[] = [];
  const destination = draft.destination.trim();
  const sourceUrl = draft.sourceUrl.trim();
  if (!destination || destination.length > sourcingLimits.destination) problems.push('Enter a destination of up to 512 characters.');
  if (draft.label.trim().length > sourcingLimits.label) problems.push('Label is limited to 120 characters.');
  if (sourceUrl && (!safeHttpUrl(sourceUrl) || sourceUrl.length > sourcingLimits.url)) problems.push('Enter the source as a full http:// or https:// address.');
  if (draft.notes.length > sourcingLimits.notes) problems.push('Notes are limited to 4000 characters.');
  errors.value = problems;
  if (problems.length) return null;
  return {
    channel: draft.channel, destination, label: optionalText(draft.label), sourceUrl: sourceUrl || null,
    verified: draft.verified, isPrimary: draft.isPrimary,
    personId: props.people.some(person => person.id === draft.personId) ? draft.personId : null, notes: draft.notes.trim(),
  };
}
async function submit(): Promise<void> {
  const input = validate();
  if (!input || saving.value || props.disabled) return;
  saving.value = true;
  try {
    const outcome = await props.save(input, expectedRevision);
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') props.sourcing.setFormDirty(props.dirtyKey, false);
  } finally { saving.value = false; }
}
</script>

<template>
  <form class="space-y-4" novalidate @submit.prevent="submit">
    <div v-if="unavailable" role="alert" class="sourcing-alert space-y-2"><p>{{ t("This item is no longer in this view. Your edits are still here; copy them before closing.") }}</p><Button size="sm" variant="outline" :is-disabled="disabled || saving" @click="emit('reload')">{{ t("Reload latest") }}</Button></div>
    <fieldset :disabled="disabled || saving" class="grid min-w-0 gap-4 sm:grid-cols-2">
      <label class="sourcing-field">{{ t("Channel") }}
        <DeskSelect v-model="draft.channel" :label="t('Channel')" :disabled="disabled || saving" :options="[...(contactChannels).map(channel => ({ value: channel, label: (t(channelLabels[channel])) }))]" />
      </label>
      <label class="sourcing-field">{{ t("Destination") }}
        <input v-model="draft.destination" class="sourcing-input" required :maxlength="sourcingLimits.destination" autocomplete="off" :placeholder="t('Number, address or link')" />
      </label>
      <label class="sourcing-field">{{ t("Label") }}
        <input v-model="draft.label" class="sourcing-input" :maxlength="sourcingLimits.label" autocomplete="off" :placeholder="t('e.g. Sales desk')" />
      </label>
      <label class="sourcing-field">{{ t("Person") }}
        <DeskSelect v-model="draft.personId" :label="t('Person')" :disabled="disabled || saving" :options="[({ value: '' as const, label: (t('No person')) }), ...(people).map(person => ({ value: person.id, label: (person.name) }))]" />
      </label>
      <label class="sourcing-field sm:col-span-2">{{ t("Where this contact was found") }}
        <input v-model="draft.sourceUrl" type="url" class="sourcing-input" :maxlength="sourcingLimits.url" placeholder="https://" autocomplete="off" />
      </label>
      <label class="sourcing-field sm:col-span-2">{{ t("Notes") }}
        <textarea v-model="draft.notes" class="sourcing-input" rows="3" :maxlength="sourcingLimits.notes" />
      </label>
      <div class="flex flex-wrap gap-5 sm:col-span-2">
        <label class="sourcing-check"><input v-model="draft.verified" type="checkbox" />{{ t("Verified") }}</label>
        <label class="sourcing-check"><input v-model="draft.isPrimary" type="checkbox" />{{ t("Primary route") }}</label>
      </div>
    </fieldset>
    <div v-if="conflict" role="alert" class="sourcing-alert space-y-2">
      <p>{{ t("This record changed. Your edits are still here. Reload the latest version before trying again.") }}</p>
      <Button size="sm" variant="outline" :is-disabled="disabled || saving" @click="emit('reload')">{{ t("Discard my edits and reload") }}</Button>
    </div>
    <ul v-if="errors.length" role="alert" class="space-y-1">
      <li v-for="error in errors" :key="error" class="form-error">{{ t(error) }}</li>
    </ul>
    <div class="flex flex-wrap gap-3">
      <Button type="submit" size="sm" :is-loading="saving" :is-disabled="disabled">{{ initial ? t("Save route") : t("Add route") }}</Button>
      <Button size="sm" variant="ghost" :is-disabled="disabled || saving" @click="emit('cancel')">{{ t("Cancel") }}</Button>
    </div>
  </form>
</template>
