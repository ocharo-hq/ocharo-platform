<script setup lang="ts">
import { watch, onScopeDispose } from 'vue';

import { useAppForm } from '@wow-two-beta/ui-vue/forms-engine/house';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';
import { Field, TextInput } from '@wow-two-beta/ui-vue/presentation/forms';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

/** Lists actual memberships and creates an explicitly named organization. */
defineOptions({ name: 'OrganizationPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const form = useAppForm(() => ({
  defaultValues: { name: '', slug: '' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    const result = await props.workspace.createOrganization(values, signal);
    if (result.ok) {
      form.reset();
      props.access.setDirty(false);
      props.access.navigate(`/organizations/${result.value.id}`);
    }
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
      <p class="eyebrow">Your memberships</p>
      <h1 id="organizations-title" class="page-title">Choose a workspace</h1>
      <p class="mb-7 text-muted-foreground">Each organization keeps its own brands, media, and content.</p>
      <div class="space-y-3">
        <button
          v-for="org in workspace.organizations.value"
          :key="org.id"
          class="workspace-card flex w-full items-center justify-between gap-4 p-5 text-left disabled:opacity-50"
          :disabled="access.busy.value"
          @click="access.navigate(`/organizations/${org.id}`)"
        >
          <span
            ><b class="block">{{ org.name }}</b
            ><span class="text-sm text-muted-foreground">{{ org.slug }}</span></span
          ><Badge variant="outline">{{ org.role }}</Badge>
        </button>
        <p v-if="!workspace.organizations.value?.length" class="empty-state">
          You do not belong to an organization yet. Create one to begin.
        </p>
      </div>
    </section>
    <form class="workspace-card h-fit space-y-5 p-6 sm:p-8" @submit.prevent="form.handleSubmit($event)">
      <h2 class="text-xl font-semibold">Create an organization</h2>
      <p class="text-sm text-muted-foreground">You will become its owner. Nothing is prefilled or created for you.</p>
      <form.Field v-slot="f" name="name" is-required
        ><Field label="Organization name"
          ><TextInput v-model="f.value" required :maxlength="120" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="slug" is-required
        ><Field label="Short name" helper="Lowercase letters, numbers, and hyphens."
          ><TextInput
            v-model="f.value"
            required
            pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
            :maxlength="64"
            @blur="f.onBlur" /></Field
      ></form.Field>
      <p v-if="form.state.submitError" role="alert" class="form-error">{{ form.state.submitError.message }}</p>
      <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value"
        >Create organization</Button
      >
    </form>
  </div>
</template>
