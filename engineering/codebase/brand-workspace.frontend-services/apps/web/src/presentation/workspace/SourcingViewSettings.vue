<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, ref } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { requestConfirmation } from '@/application/shared/UseFeedback';
import { defaultSourcingPreferences, sourcingSections, sourcingSectionLabels, type SourcingPreferences } from '@/domain/workspace/SourcingPreferences';

/** Edits a private view preference; cancel never changes the supplier page. */
defineOptions({ name: 'SourcingViewSettings' });
const props = defineProps<{ initial: SourcingPreferences; save: (value: SourcingPreferences) => boolean }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useUiLocale();
const draft = ref<SourcingPreferences>({ ...props.initial, visible: [...props.initial.visible] });
const dirty = computed(() => JSON.stringify(draft.value) !== JSON.stringify(props.initial));
async function close(): Promise<void> {
  if (dirty.value && !await requestConfirmation(t('Discard unsaved view settings?'), { confirmLabel: t('Discard changes') })) return;
  emit('close');
}
function submit(): void { if (props.save(draft.value)) emit('close'); }
</script>

<template>
  <Modal :open="true" @update:open="value => { if (!value) void close(); }">
    <ModalContent class="w-full max-w-lg max-h-[90dvh] overflow-y-auto rounded-2xl border border-border bg-card p-6 text-foreground">
      <form class="space-y-5" @submit.prevent="submit">
        <ModalTitle class="text-xl font-semibold">{{ t('Sourcing view settings') }}</ModalTitle>
        <p class="text-sm text-muted-foreground">{{ t('Saved for your account in this browser. Other people’s views and supplier records stay unchanged.') }}</p>
        <label class="sourcing-field">{{ t('Default starting view') }}
          <DeskSelect v-model="draft.start" :label="t('Default starting view')" :options="[({ value: 'suppliers' as const, label: (t('Suppliers')) }), ({ value: 'materials' as const, label: (t('Materials')) })]" />
        </label>
        <label class="sourcing-field">{{ t('Supplier section order') }}
          <DeskSelect v-model="draft.order" :label="t('Supplier section order')" :options="[({ value: 'materials-first' as const, label: (t('Materials first')) }), ({ value: 'contacts-first' as const, label: (t('Contacts first')) })]" />
        </label>
        <fieldset class="space-y-3">
          <legend class="mb-2 text-sm font-medium">{{ t('Show on supplier pages') }}</legend>
          <label v-for="section in sourcingSections" :key="section" class="flex items-center gap-3 text-sm">
            <input v-model="draft.visible" type="checkbox" :value="section" class="h-4 w-4 accent-primary" />{{ t(sourcingSectionLabels[section]) }}
          </label>
          <p class="text-xs text-muted-foreground">{{ t('Supplier name, location and stage always stay visible.') }}</p>
        </fieldset>
        <footer class="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
          <Button type="button" variant="ghost" @click="draft = defaultSourcingPreferences()">{{ t('Restore defaults') }}</Button>
          <Button type="button" variant="outline" @click="close">{{ t('Cancel') }}</Button>
          <Button type="submit">{{ t('Save view settings') }}</Button>
        </footer>
      </form>
    </ModalContent>
  </Modal>
</template>
