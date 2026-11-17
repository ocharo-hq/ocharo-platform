<script setup lang="ts">
import EventsSelect from '@/presentation/common/EventsSelect.vue';
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventSaveOutcome, EventsControls } from '@/application/events/UseEvents';
import {
  eventLimits,
  eventPriorities,
  eventStatuses,
  priorityLabels,
  statusLabels,
  trackingDraft,
  validateTrackingDraft,
  type ShowcaseEvent,
  type TrackingInput,
} from '@/domain/events/Events';

/**
 * Edits where Ocharo stands with an existing event: its stage, priority, how to take part, Ocharo's own cost and notes.
 * The event's facts (title, dates, place, organizer) come from its source and change only through review in WoW2 Events,
 * so no field here edits them.
 */
defineOptions({ name: 'EventTrackingForm' });
const props = defineProps<{
  events: EventsControls;
  dirtyKey: string;
  initial: ShowcaseEvent;
  disabled: boolean;
  submitLabel: string;
  save: (input: TrackingInput) => Promise<EventSaveOutcome>;
}>();
const emit = defineEmits<{ cancel: []; reload: [] }>();
const { t } = useUiLocale();
const draft = reactive(trackingDraft(props.initial));
const original = JSON.stringify(trackingDraft(props.initial));
const errors = ref<string[]>([]);
const saving = ref(false);
const conflict = ref(false);
const dirty = computed(() => JSON.stringify(draft) !== original);
watch(dirty, (value) => props.events.setFormDirty(props.dirtyKey, value), { immediate: true });
onScopeDispose(() => props.events.setFormDirty(props.dirtyKey, false));

async function submit(): Promise<void> {
  if (props.disabled || saving.value || !props.events.canEdit.value) return;
  const { input, problems } = validateTrackingDraft(draft);
  errors.value = problems;
  if (!input) return;
  saving.value = true;
  try {
    const outcome = await props.save(input);
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') props.events.setFormDirty(props.dirtyKey, false);
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <form class="space-y-4" novalidate @submit.prevent="submit">
    <p class="text-xs leading-5 text-muted-foreground">
      {{ t("Dates, place and organizer come from the event's source and cannot be edited here.") }}
    </p>
    <fieldset :disabled="disabled || saving" class="grid min-w-0 gap-4 sm:grid-cols-2">
      <label class="events-field"
        >{{ t('Status') }}
        <EventsSelect
          v-model="draft.status"
          :label="t('Status')"
          :disabled="disabled || saving"
          :options="[...eventStatuses.map((item) => ({ value: item, label: t(statusLabels[item]) }))]"
        />
      </label>
      <label class="events-field"
        >{{ t('Priority') }}
        <EventsSelect
          v-model="draft.priority"
          :label="t('Priority')"
          :disabled="disabled || saving"
          :options="[...eventPriorities.map((item) => ({ value: item, label: t(priorityLabels[item]) }))]"
        />
      </label>
      <label class="events-field sm:col-span-2"
        >{{ t("Ocharo's cost") }}
        <input
          v-model="draft.cost"
          class="events-input"
          data-test="tracking-cost"
          :maxlength="eventLimits.cost"
          autocomplete="off"
          :placeholder="t('As stated, e.g. stand from 500 USD')"
        />
        <span class="text-xs">{{ t('Leave empty when the cost is not stated.') }}</span>
      </label>
      <label class="events-field sm:col-span-2"
        >{{ t('How to take part') }}
        <textarea
          v-model="draft.participation"
          class="events-input"
          data-test="tracking-participation"
          rows="3"
          :maxlength="eventLimits.participation"
        />
      </label>
      <label class="events-field sm:col-span-2"
        >{{ t('Notes') }}
        <textarea
          v-model="draft.notes"
          class="events-input"
          data-test="tracking-notes"
          rows="4"
          :maxlength="eventLimits.notes"
        />
        <span class="text-xs">{{
          t('Fit, what to show, evidence. Never private prices or personal assessments.')
        }}</span>
      </label>
    </fieldset>
    <div v-if="conflict" role="alert" class="events-alert space-y-2">
      <p>{{ t('This record changed. Your edits are still here. Reload the latest version before trying again.') }}</p>
      <Button size="sm" variant="outline" :is-disabled="saving" @click="emit('reload')">{{
        t('Discard my edits and reload')
      }}</Button>
    </div>
    <ul v-if="errors.length" role="alert" class="space-y-1">
      <li v-for="error in errors" :key="error" class="form-error">{{ t(error) }}</li>
    </ul>
    <div class="flex flex-wrap gap-3">
      <Button type="submit" size="sm" :is-loading="saving" :is-disabled="disabled">{{ submitLabel }}</Button>
      <Button size="sm" variant="ghost" :is-disabled="saving" @click="emit('cancel')">{{ t('Cancel') }}</Button>
    </div>
  </form>
</template>
