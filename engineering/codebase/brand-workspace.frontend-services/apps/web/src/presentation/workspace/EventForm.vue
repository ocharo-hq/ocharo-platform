<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventSaveOutcome, EventsControls } from '@/application/workspace/UseEvents';
import {
  datePrecisions, eventDraft, eventFormats, eventLimits, eventPriorities, eventScopes, eventStatuses,
  formatLabels, precisionLabels, priorityLabels, scopeLabels, statusLabels, validateEventDraft,
  type DatePrecision, type EventInput, type ShowcaseEvent,
} from '@/domain/workspace/Events';

/** Creates or edits every field of an event, mirroring the contract's validation before sending. */
defineOptions({ name: 'EventForm' });
const props = defineProps<{
  events: EventsControls;
  dirtyKey: string;
  initial?: ShowcaseEvent | null | undefined;
  disabled: boolean;
  submitLabel: string;
  save: (input: EventInput) => Promise<EventSaveOutcome>;
}>();
const emit = defineEmits<{ cancel: []; reload: [] }>();
const { t } = useUiLocale();
const draft = reactive(eventDraft(props.initial));
const original = JSON.stringify(eventDraft(props.initial));
const errors = ref<string[]>([]);
const saving = ref(false);
const conflict = ref(false);
const dirty = computed(() => JSON.stringify(draft) !== original);
watch(dirty, value => props.events.setFormDirty(props.dirtyKey, value), { immediate: true });
onScopeDispose(() => props.events.setFormDirty(props.dirtyKey, false));
const monthly = computed(() => draft.datePrecision === 'month' || draft.datePrecision === 'estimated');
const minMonth = eventLimits.minDate.slice(0, 7);
const maxMonth = eventLimits.maxDate.slice(0, 7);

// Keep the visible date authoritative when changing precision; hidden draft fields may be older.
let previousDatedPrecision: DatePrecision = draft.datePrecision;
const dayInMonth = (day: string, month: string): string => !month ? '' : day.startsWith(`${month}-`) ? day : `${month}-01`;
watch(() => draft.datePrecision, (next: DatePrecision) => {
  if (next === 'unknown') return;
  const wasMonthly = previousDatedPrecision === 'month' || previousDatedPrecision === 'estimated';
  const isMonthly = next === 'month' || next === 'estimated';
  if (isMonthly && !wasMonthly) {
    draft.startMonth = draft.startDay.slice(0, 7);
    draft.endMonth = draft.endDay.slice(0, 7) === draft.startMonth ? '' : draft.endDay.slice(0, 7);
  } else if (next === 'day' && wasMonthly) {
    draft.startDay = dayInMonth(draft.startDay, draft.startMonth);
    draft.endDay = draft.endMonth ? dayInMonth(draft.endDay, draft.endMonth)
      : draft.endDay.startsWith(`${draft.startMonth}-`) ? draft.endDay : '';
  }
  previousDatedPrecision = next;
});

async function submit(): Promise<void> {
  if (props.disabled || saving.value || !props.events.canEdit.value) return;
  const { input, problems } = validateEventDraft(draft, props.initial);
  errors.value = problems;
  if (!input) return;
  saving.value = true;
  try {
    const outcome = await props.save(input);
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') props.events.setFormDirty(props.dirtyKey, false);
  } finally { saving.value = false; }
}
</script>

<template>
  <form class="space-y-4" novalidate @submit.prevent="submit">
    <fieldset :disabled="disabled || saving" class="grid min-w-0 gap-4 sm:grid-cols-2">
      <label class="events-field sm:col-span-2">{{ t("Event title") }}
        <input v-model="draft.title" class="events-input" required :maxlength="eventLimits.title" autocomplete="off" />
      </label>
      <label class="events-field sm:col-span-2">{{ t("Organizer") }}
        <input v-model="draft.organizer" class="events-input" :maxlength="eventLimits.organizer" autocomplete="off" />
      </label>
      <label class="events-field">{{ t("Scope") }}
        <DeskSelect v-model="draft.scope" :label="t('Scope')" :disabled="disabled || saving" :options="[...(eventScopes).map(item => ({ value: item, label: (t(scopeLabels[item])) }))]" />
      </label>
      <label class="events-field">{{ t("Format") }}
        <DeskSelect v-model="draft.format" :label="t('Format')" :disabled="disabled || saving" :options="[...(eventFormats).map(item => ({ value: item, label: (t(formatLabels[item])) }))]" />
      </label>
      <label class="events-field">{{ t("Status") }}
        <DeskSelect v-model="draft.status" :label="t('Status')" :disabled="disabled || saving" :options="[...(eventStatuses).map(item => ({ value: item, label: (t(statusLabels[item])) }))]" />
      </label>
      <label class="events-field">{{ t("Priority") }}
        <DeskSelect v-model="draft.priority" :label="t('Priority')" :disabled="disabled || saving" :options="[...(eventPriorities).map(item => ({ value: item, label: (t(priorityLabels[item])) }))]" />
      </label>
      <label class="events-field">{{ t("Country code") }}
        <input v-model="draft.country" class="events-input uppercase placeholder:normal-case" maxlength="2" autocomplete="off" :placeholder="t('e.g. UZ')" />
      </label>
      <label class="events-field">{{ t("City") }}
        <input v-model="draft.city" class="events-input" :maxlength="eventLimits.city" autocomplete="off" />
      </label>
      <label class="events-field sm:col-span-2">{{ t("Venue") }}
        <input v-model="draft.venue" class="events-input" :maxlength="eventLimits.venue" autocomplete="off" />
      </label>
      <label class="events-field sm:col-span-2">{{ t("Date precision") }}
        <DeskSelect v-model="draft.datePrecision" :label="t('Date precision')" :disabled="disabled || saving" :options="[...(datePrecisions).map(item => ({ value: item, label: (t(precisionLabels[item])) }))]" />
      </label>
      <template v-if="draft.datePrecision === 'day'">
        <label class="events-field">{{ t("Start date") }}
          <input v-model="draft.startDay" type="date" class="events-input" :min="eventLimits.minDate" :max="eventLimits.maxDate" />
        </label>
        <label class="events-field">{{ t("End date") }}
          <input v-model="draft.endDay" type="date" class="events-input" :min="draft.startDay || eventLimits.minDate" :max="eventLimits.maxDate" />
        </label>
      </template>
      <template v-else-if="monthly">
        <label class="events-field">{{ t("Start month") }}
          <input v-model="draft.startMonth" type="month" class="events-input" :min="minMonth" :max="maxMonth" />
        </label>
        <label class="events-field">{{ t("End month (optional)") }}
          <input v-model="draft.endMonth" type="month" class="events-input" :min="draft.startMonth || minMonth" :max="maxMonth" />
        </label>
        <p class="text-xs leading-5 text-muted-foreground sm:col-span-2">
          {{ draft.datePrecision === 'estimated' ? t("Estimated from earlier editions. Only the month is shown, marked as expected.") : t("Only the month is announced, so only the month is shown.") }}
        </p>
      </template>
      <p v-else class="text-xs leading-5 text-muted-foreground sm:col-span-2">{{ t("Dates stay empty until they are announced.") }}</p>
      <label class="events-field">{{ t("Application deadline") }}
        <input v-model="draft.applicationDeadline" type="date" class="events-input" :min="eventLimits.minDate" :max="eventLimits.maxDate" />
      </label>
      <label class="events-field">{{ t("Cost") }}
        <input v-model="draft.cost" class="events-input" :maxlength="eventLimits.cost" autocomplete="off" :placeholder="t('As stated, e.g. stand from 500 USD')" />
        <span class="text-xs">{{ t("Leave empty when the cost is not stated.") }}</span>
      </label>
      <label class="events-field sm:col-span-2">{{ t("How to take part") }}
        <textarea v-model="draft.participation" class="events-input" rows="3" :maxlength="eventLimits.participation" />
      </label>
      <label class="events-field">{{ t("Website") }}
        <input v-model="draft.website" type="url" class="events-input" :maxlength="eventLimits.url" placeholder="https://" autocomplete="off" />
      </label>
      <label class="events-field">{{ t("Where it was found") }}
        <input v-model="draft.sourceUrl" type="url" class="events-input" :maxlength="eventLimits.url" placeholder="https://" autocomplete="off" />
      </label>
      <label class="events-field sm:col-span-2">{{ t("Notes") }}
        <textarea v-model="draft.notes" class="events-input" rows="4" :maxlength="eventLimits.notes" />
        <span class="text-xs">{{ t("Fit, what to show, evidence. Never private prices or personal assessments.") }}</span>
      </label>
    </fieldset>
    <div v-if="conflict" role="alert" class="events-alert space-y-2">
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
