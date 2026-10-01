<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { onScopeDispose, watch } from 'vue';

import { useAppForm } from '@wow-two-beta/ui-vue/forms-engine/house';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Field, TextInput } from '@wow-two-beta/ui-vue/presentation/forms';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

const { t } = useUiLocale();

/** Creates brand configuration without implying any scheduling capability. */
defineOptions({ name: 'BrandsPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const form = useAppForm(() => ({
  defaultValues: { name: '', timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    const result = await props.workspace.createBrand(values, signal);
    if (result.ok) form.reset();
    return result;
  },
}));
watch(
  () => form.state.isDirty,
  (dirty) => {
    props.access.setDirty(dirty);
  },
);
onScopeDispose(() => {
  props.access.setDirty(false);
});
</script>

<template>
  <p class="eyebrow">{{ t("Organization configuration") }}</p>
  <h1 class="page-title">{{ t("Brands") }}</h1>
  <div class="grid gap-8 lg:grid-cols-2">
    <section :aria-label="t('Existing brands')" class="space-y-3">
      <div v-for="brand in workspace.brands.value" :key="brand.id" class="workspace-card p-5">
        <h2 class="font-semibold">{{ brand.name }}</h2>
        <p class="mt-2 text-sm text-muted-foreground">{{ brand.timeZone }}</p>
      </div>
      <p v-if="!workspace.brands.value?.length" class="empty-state">
        {{ t("No brands yet. An owner can create the first brand.") }}
      </p>
    </section>
    <form
      v-if="workspace.canApprove.value"
      class="workspace-card h-fit space-y-5 p-6"
      @submit.prevent="form.handleSubmit($event)"
    >
      <h2 class="text-xl font-semibold">{{ t("Create a brand") }}</h2>
      <form.Field v-slot="f" name="name" is-required
        ><Field :label="t('Brand name')"><TextInput v-model="f.value" required :maxlength="120" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="timeZone" is-required
        ><Field :label="t('Time zone')" :helper="t('For example Asia/Tashkent or UTC.')"
          ><TextInput v-model="f.value" required :maxlength="100" @blur="f.onBlur" /></Field
      ></form.Field>
      <p v-if="form.state.submitError" role="alert" class="form-error">{{ t(form.state.submitError.message ?? '') }}</p>
      <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value">{{ t("Create brand") }}</Button>
    </form>
  </div>
</template>
