<script setup lang="ts">
import { computed } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Field, TagsInput } from '@wow-two-beta/fe/presentation/forms';
import { orderProductSizes } from '../../../domain/catalogue/ProductSizes';
import { useUiLocale } from '../../../application/shared/UseUiLocale';

/**
 * Editable list of short codes (sizes, tags, fabric codes) over the SDK `TagsInput`: Enter, Tab, a comma or leaving
 * the field adds; a tag's close button removes. Sizes keep the catalogue's size order, and the suggestions not chosen
 * yet are one click away.
 */
defineOptions({ name: 'TagInput' });
const props = defineProps<{ label: string; placeholder?: string; suggestions?: string[]; orderedSizes?: boolean }>();
const model = defineModel<string[]>({ required: true });
const { t } = useUiLocale();
/** The selected sizes in catalogue order, or tags in their authored order. */
const displayed = computed(() =>
  props.orderedSizes ? orderProductSizes(model.value, props.suggestions) : model.value,
);
const remaining = computed(() => props.suggestions?.filter((s) => !model.value.includes(s)) ?? []);
function commit(tags: readonly string[]) {
  const values = [...new Set(tags.map((tag) => tag.trim()).filter((tag) => tag.length > 0))];
  model.value = props.orderedSizes ? orderProductSizes(values, props.suggestions) : values;
}
</script>

<template>
  <Field :label="props.label">
    <TagsInput
      :model-value="displayed"
      :placeholder="props.placeholder ?? t('Add…')"
      v-bind="{ 'aria-label': props.label }"
      @update:model-value="commit"
    />
    <div v-if="remaining.length" class="mt-1.5 flex flex-wrap gap-1">
      <Button
        v-for="s in remaining"
        :key="s"
        variant="outline"
        tone="neutral"
        size="xs"
        class="border-dashed text-[11px] font-normal text-muted-foreground"
        @click="commit([...model, s])"
      >
        + {{ s }}
      </Button>
    </div>
  </Field>
</template>
