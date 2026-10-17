<script setup lang="ts">
import { notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { watch, onScopeDispose } from 'vue';

import { useAppForm } from '@wow-two-beta/fe/forms-engine/house';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge } from '@wow-two-beta/fe/presentation/display';
import { Field, TextInput } from '@wow-two-beta/fe/presentation/forms';

import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

const { t } = useUiLocale();

/** Lists actual memberships and creates an explicitly named organization. */
defineOptions({ name: 'OrganizationPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; catalogueEntry?: boolean }>();
const organizationPath = (id: string) => `/organizations/${id}${props.catalogueEntry ? '/catalogue' : ''}`;
const form = useAppForm(() => ({
  defaultValues: { name: '', slug: '' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    const result = await props.workspace.createOrganization(values, signal);
    if (result.ok) {
      form.reset();
      props.access.setDirty(false);
      props.access.navigate(organizationPath(result.value.id));
    }
    if (!result.ok && result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
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
  <div class="grid gap-8 lg:grid-cols-[1.2fr_1fr]">
    <section aria-labelledby="organizations-title">
      <p class="eyebrow">{{ t("Your memberships") }}</p>
      <h1 id="organizations-title" class="page-title">{{ t(catalogueEntry ? 'Choose your catalogue workspace' : 'Choose a workspace') }}</h1>
      <p class="mb-7 text-muted-foreground">{{ t(catalogueEntry ? 'Open the catalogue designer for one of your organizations.' : 'Each organization keeps its own brands, media, and content.') }}</p>
      <div class="space-y-3">
        <button
          v-for="org in workspace.organizations.value"
          :key="org.id"
          class="workspace-card flex w-full items-center justify-between gap-4 p-5 text-left disabled:opacity-50"
          :disabled="access.busy.value"
          @click="access.navigate(organizationPath(org.id))"
        >
          <span
            ><b class="block">{{ org.name }}</b
            ><span class="text-sm text-muted-foreground">{{ org.slug }}</span></span
          ><Badge variant="outline">{{ t(org.role) }}</Badge>
        </button>
        <p v-if="!workspace.organizations.value?.length" class="empty-state">
          {{ t("You do not belong to an organization yet. Create one to begin.") }}
        </p>
      </div>
    </section>
    <form class="workspace-card h-fit space-y-5 p-6 sm:p-8" @submit.prevent="form.handleSubmit($event)">
      <h2 class="text-xl font-semibold">{{ t("Create an organization") }}</h2>
      <p class="text-sm text-muted-foreground">{{ t("You will become its owner. Nothing is prefilled or created for you.") }}</p>
      <form.Field v-slot="f" name="name" is-required
        ><Field :label="t('Organization name')"
          ><TextInput v-model="f.value" is-required :maxlength="120" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="slug" is-required
        ><Field :label="t('Short name')" :helper="t('Lowercase letters, numbers, and hyphens.')"
          ><TextInput
            v-model="f.value"
            is-required
            pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
            :maxlength="64"
            @blur="f.onBlur" /></Field
      ></form.Field>
      <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value"
        >{{ t("Create organization") }}</Button
      >
    </form>
  </div>
</template>
