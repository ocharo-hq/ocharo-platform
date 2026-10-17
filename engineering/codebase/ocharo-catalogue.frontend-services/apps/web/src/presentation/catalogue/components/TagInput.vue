<script setup lang="ts">
import { computed, ref } from 'vue';
import { orderProductSizes } from '../../../domain/catalogue/ProductSizes';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
const { t } = useUiLocale();
import { X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';

/** Editable list of short codes (sizes, tags, fabric codes). Enter or comma adds; × removes. */
defineOptions({ name: 'TagInput' });
const props = defineProps<{ label: string; placeholder?: string; suggestions?: string[]; orderedSizes?: boolean }>();
const model = defineModel<string[]>({ required: true });
const entry = ref('');
/** The selected sizes in catalogue order, or tags in their authored order. */
const displayed = computed(() =>
  props.orderedSizes ? orderProductSizes(model.value, props.suggestions) : model.value,
);
function add(raw: string) {
  const values = raw
    .split(',')
    .map((v) => v.trim())
    .filter((v) => v.length > 0 && !model.value.includes(v));
  if (values.length) {
    const next = [...model.value, ...values];
    model.value = props.orderedSizes ? orderProductSizes(next, props.suggestions) : [...new Set(next)];
  }
  entry.value = '';
}
function remove(value: string) {
  model.value = displayed.value.filter((v) => v !== value);
}
</script>

<template>
  <div>
    <span class="label">{{ props.label }}</span>
    <div
      class="flex min-h-10 flex-wrap items-center gap-1.5 rounded-lg border border-border bg-card px-2 py-1.5 focus-within:border-primary"
    >
      <span v-for="value in displayed" :key="value" class="chip bg-secondary text-secondary-foreground">
        {{ value }}
        <button
          type="button"
          class="rounded-full p-0.5 hover:bg-card"
          :aria-label="`${t('Remove')} ${value}`"
          @click="remove(value)"
        >
          <Icon :icon="X" :size="11" />
        </button>
      </span>
      <input
        v-model="entry"
        class="min-w-24 flex-1 bg-transparent px-1 py-1 text-sm outline-none"
        :placeholder="props.placeholder ?? t('Add…')"
        :aria-label="props.label"
        @keydown.enter.prevent="add(entry)"
        @keydown.,.prevent="add(entry)"
        @blur="entry && add(entry)"
      />
    </div>
    <div v-if="props.suggestions?.some((s) => !model.includes(s))" class="mt-1.5 flex flex-wrap gap-1">
      <button
        v-for="s in props.suggestions.filter((s) => !model.includes(s))"
        :key="s"
        type="button"
        class="rounded-md border border-dashed border-border px-2 py-0.5 text-[11px] text-muted-foreground hover:border-primary hover:text-primary-soft-foreground"
        @click="add(s)"
      >
        + {{ s }}
      </button>
    </div>
  </div>
</template>
