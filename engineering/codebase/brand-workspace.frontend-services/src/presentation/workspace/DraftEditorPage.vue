<script setup lang="ts">
import { computed, onScopeDispose, ref, shallowRef, watch } from 'vue';

import { useAppForm } from '@wow-two-beta/ui-vue/forms-engine/house';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';
import { Field, TextAreaInput, TextInput } from '@wow-two-beta/ui-vue/presentation/forms';

import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { Draft, DraftInput, ManualExport } from '@/domain/workspace/Models';

/** Edits a detached draft buffer and acts only on exact saved revisions. */
defineOptions({ name: 'DraftEditorPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const saved = shallowRef<Draft | null>(props.workspace.draft.value ?? null);
const actionError = ref('');
const actionName = ref('');
const conflicted = ref(false);
const revokeReason = ref('');
const revoking = ref(false);
const prepared = shallowRef<ManualExport | null>(null);
const exportKey = ref<string | null>(null);
const controller = new AbortController();
const orgId = props.workspace.organization.value!.id;

/** @internal Produces detached form values from the saved immutable revision. */
function inputOf(draft: Draft | null): DraftInput {
  return draft
    ? {
        brandId: draft.brandId,
        title: draft.title,
        caption: draft.caption,
        language: draft.language,
        destination: draft.destination,
        sourceNotes: draft.sourceNotes,
        mediaIds: draft.media.map((item) => item.id),
      }
    : {
        brandId: props.workspace.brands.value?.[0]?.id ?? '',
        title: '',
        caption: '',
        language: 'en',
        destination: '',
        sourceNotes: '',
        mediaIds: [],
      };
}
const form = useAppForm<DraftInput>(() => ({
  defaultValues: inputOf(saved.value),
  isDisabled: props.access.busy.value || !props.workspace.canEdit.value,
  onSubmit: async (values, { signal }) => {
    actionError.value = '';
    conflicted.value = false;
    const result = await props.workspace.saveDraft(values, saved.value?.revision ?? null, signal);
    if (result.ok) {
      saved.value = result.value;
      form.reset(inputOf(result.value));
      prepared.value = null;
      exportKey.value = null;
      if (!props.workspace.draft.value) {
        props.access.setDirty(false);
        props.access.navigate(`/organizations/${orgId}/drafts/${result.value.id}`);
      }
    } else {
      conflicted.value = result.failure.status === 409;
      actionError.value = failureMessage(result.failure);
    }
    return result;
  },
}));
const selectedBytes = computed(() =>
  (props.workspace.media.value ?? [])
    .filter((m) => form.values.mediaIds.includes(m.id))
    .reduce((sum, m) => sum + m.byteLength, 0),
);
const actionDisabled = computed(
  () => props.access.busy.value || form.state.isDirty || !saved.value || conflicted.value,
);
const downloadUrl = computed(() => {
  if (!prepared.value) return null;
  const expected = `/api/organizations/${orgId}/exports/${prepared.value.id}/download`;
  try {
    const url = new URL(prepared.value.downloadUrl, window.location.origin);
    return url.origin === window.location.origin && url.pathname === expected && !url.search && !url.hash
      ? expected
      : null;
  } catch {
    return null;
  }
});
watch(
  () => form.state.isDirty || revokeReason.value.length > 0,
  (dirty) => {
    props.access.setDirty(dirty);
  },
);
onScopeDispose(() => {
  controller.abort();
  props.access.setDirty(false);
});

function toggleMedia(id: string): void {
  if (props.access.busy.value || !props.workspace.canEdit.value) return;
  const selected = form.values.mediaIds;
  form.setValue('mediaIds', selected.includes(id) ? selected.filter((item) => item !== id) : [...selected, id]);
}
async function transition(action: 'request-review' | 'approve' | 'revoke-approval'): Promise<void> {
  if (actionDisabled.value || !saved.value) return;
  actionError.value = '';
  actionName.value = action;
  const result = await props.workspace.transition(action, saved.value.revision, revokeReason.value, controller.signal);
  actionName.value = '';
  if (result.ok) {
    saved.value = result.value;
    revoking.value = false;
    revokeReason.value = '';
    prepared.value = null;
    exportKey.value = null;
  } else {
    actionError.value = failureMessage(result.failure);
    conflicted.value = result.failure.status === 409;
  }
}
async function prepareExport(): Promise<void> {
  if (actionDisabled.value || !saved.value) return;
  exportKey.value ??= crypto.randomUUID();
  actionError.value = '';
  actionName.value = 'export';
  const result = await props.workspace.createExport(saved.value.revision, exportKey.value, controller.signal);
  actionName.value = '';
  if (result.ok) prepared.value = result.value;
  else {
    actionError.value = failureMessage(result.failure);
    conflicted.value = result.failure.status === 409;
  }
}
</script>

<template>
  <div class="mb-7 flex flex-wrap items-center justify-between gap-4">
    <div>
      <p class="eyebrow">
        {{ workspace.brands.value?.find((b) => b.id === form.values.brandId)?.name ?? 'Content preparation' }}
      </p>
      <h1 class="page-title mb-2">{{ saved ? 'Prepare your content' : 'A new draft' }}</h1>
      <p class="text-sm text-muted-foreground">
        {{ saved ? `Saved revision ${saved.revision}` : 'Not saved yet'
        }}<span v-if="form.state.isDirty"> · Unsaved changes</span>
      </p>
    </div>
    <Button variant="outline" :is-disabled="access.busy.value" @click="access.navigate(`/organizations/${orgId}`)"
      >All drafts</Button
    >
  </div>
  <div class="grid items-start gap-7 xl:grid-cols-[1.65fr_1fr]">
    <form class="workspace-card space-y-6 p-6 sm:p-8" @submit.prevent="form.handleSubmit($event)">
      <div class="grid gap-5 sm:grid-cols-2">
        <form.Field v-slot="f" name="brandId" is-required
          ><Field label="Brand"
            ><select
              v-model="f.value"
              class="native-select"
              required
              :disabled="access.busy.value || !workspace.canEdit.value"
              @blur="f.onBlur"
            >
              <option value="" disabled>Choose a brand</option>
              <option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">
                {{ brand.name }}
              </option>
            </select></Field
          ></form.Field
        >
        <form.Field v-slot="f" name="language" is-required
          ><Field label="Language" helper="For example uz, uz-Latn, ru, or en."
            ><TextInput
              v-model="f.value"
              required
              :maxlength="35"
              pattern="[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*"
              @blur="f.onBlur" /></Field
        ></form.Field>
      </div>
      <form.Field v-slot="f" name="title" is-required
        ><Field label="Working title"><TextInput v-model="f.value" required :maxlength="200" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="caption" is-required
        ><Field label="Caption"
          ><TextAreaInput v-model="f.value" required :maxlength="10000" :rows="9" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="destination" is-required
        ><Field label="Intended destination" helper="For example Instagram feed or Telegram channel."
          ><TextInput v-model="f.value" required :maxlength="200" @blur="f.onBlur" /></Field
      ></form.Field>
      <form.Field v-slot="f" name="sourceNotes"
        ><Field
          label="Source notes"
          helper="Record the facts and source records behind this copy. A render is not evidence of a finished product."
          ><TextAreaInput v-model="f.value" :maxlength="4000" :rows="4" @blur="f.onBlur" /></Field
      ></form.Field>
      <fieldset class="space-y-4" :disabled="access.busy.value || !workspace.canEdit.value">
        <legend class="mb-2 font-medium">Images for this revision</legend>
        <p class="text-sm text-muted-foreground">
          Selection order becomes package order. Up to 10 images and 25 MiB combined.
        </p>
        <div class="grid max-h-96 gap-3 overflow-y-auto sm:grid-cols-2">
          <label
            v-for="asset in workspace.media.value"
            :key="asset.id"
            class="flex cursor-pointer items-center gap-3 rounded-xl border border-border p-3"
            ><input
              type="checkbox"
              :checked="form.values.mediaIds.includes(asset.id)"
              :disabled="
                !form.values.mediaIds.includes(asset.id) &&
                (form.values.mediaIds.length >= 10 || selectedBytes + asset.byteLength > 25 * 1024 * 1024)
              "
              @change="toggleMedia(asset.id)"
            /><img
              :src="`/api/organizations/${orgId}/media/${asset.id}/content`"
              alt=""
              class="h-12 w-12 rounded object-cover"
              loading="lazy"
            /><span class="min-w-0 break-all text-xs"
              >{{ asset.fileName
              }}<span v-if="form.values.mediaIds.includes(asset.id)" class="mt-1 block text-primary"
                >Order {{ form.values.mediaIds.indexOf(asset.id) + 1 }}</span
              ></span
            ></label
          >
        </div>
        <p v-if="!workspace.media.value?.length" class="text-sm text-muted-foreground">
          No images available. You can save a text-only draft, then add originals through Media.
        </p>
        <p class="text-xs text-muted-foreground">
          {{ form.values.mediaIds.length }} selected · {{ (selectedBytes / 1024 / 1024).toFixed(2) }} MiB
        </p>
      </fieldset>
      <p v-if="actionError" role="alert" class="form-error">{{ actionError }}</p>
      <p v-else-if="form.state.submitError" role="alert" class="form-error">{{ form.state.submitError.message }}</p>
      <div class="flex flex-wrap gap-3">
        <Button
          v-if="workspace.canEdit.value"
          type="submit"
          :is-loading="form.state.isSubmitting"
          :is-disabled="access.busy.value || conflicted"
          >Save revision</Button
        ><Button
          v-if="conflicted"
          variant="outline"
          :is-disabled="access.busy.value"
            @click="access.navigate(`/organizations/${orgId}/drafts/${saved?.id ?? 'new'}`)"
          >Reload latest version</Button
        >
      </div>
      <p class="text-xs leading-5 text-muted-foreground">
        Saving creates a new immutable revision and clears its current review and approval. Unsaved changes are never
        included in review or export.
      </p>
    </form>

    <aside class="space-y-5" aria-label="Review and manual export">
      <section class="workspace-card space-y-5 p-6">
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-lg font-semibold">Review the saved revision</h2>
          <Badge :variant="saved?.status === 'approved' ? 'success' : 'neutral'">{{
            saved?.status ?? 'Unsaved'
          }}</Badge>
        </div>
        <p v-if="!saved" class="text-sm leading-6 text-muted-foreground">Save this draft before requesting review.</p>
        <template v-else
          ><p class="text-sm leading-6">
            Actions apply to revision <b>{{ saved.revision }}</b
            >: its exact caption, language, destination, source notes, and selected image bytes.
          </p>
          <p v-if="form.state.isDirty" class="text-sm text-warning-soft-foreground">
            Save your changes before continuing with review or export.
          </p>
          <p v-if="saved.requestedReviewRevision" class="text-sm text-muted-foreground">
            Review requested for revision {{ saved.requestedReviewRevision }}.
          </p>
          <div v-if="saved.approval" class="rounded-xl bg-success-soft p-4 text-sm text-success-soft-foreground">
            <p>Revision {{ saved.approval.revision }} approved.</p>
            <p class="mt-2">
              Approved by
              {{
                saved.approval.approvedBy === access.session.value?.user?.id
                  ? `${access.session.value.user.displayName} (you)`
                  : 'an organization owner'
              }}.
            </p>
            <time class="mt-2 block" :datetime="saved.approval.approvedAtUtc">{{
              new Date(saved.approval.approvedAtUtc).toLocaleString()
            }}</time>
            <details class="mt-3 text-xs">
              <summary>Audit reference</summary>
              <p class="mt-2 break-all">Operator {{ saved.approval.approvedBy }}</p>
            </details>
          </div>
          <Button
            v-if="workspace.canEdit.value && saved.status === 'draft'"
            :is-disabled="actionDisabled"
            :is-loading="actionName === 'request-review'"
            @click="transition('request-review')"
            >Request review</Button
          >
          <template v-if="workspace.canApprove.value && saved.status === 'in-review'"
            ><p class="text-xs leading-5 text-muted-foreground">
              Owners may approve their own work in this initial workflow. Your operator identity is recorded.
            </p>
            <Button :is-disabled="actionDisabled" :is-loading="actionName === 'approve'" @click="transition('approve')"
              >Approve revision {{ saved.revision }}</Button
            ></template
          >
          <p v-if="!workspace.canApprove.value && saved.status === 'in-review'" class="text-sm text-muted-foreground">
            An organization owner can approve this revision.
          </p>
          <Button
            v-if="workspace.canApprove.value && saved.status === 'approved' && !revoking"
            variant="outline"
            :is-disabled="actionDisabled"
            @click="revoking = true"
            >Revoke approval</Button
          >
          <form v-if="revoking" class="space-y-3" @submit.prevent="transition('revoke-approval')">
            <Field label="Reason for revocation"
              ><TextAreaInput v-model="revokeReason" required :maxlength="1000" :disabled="access.busy.value" /></Field
            ><Button
              type="submit"
              variant="outline"
              :is-disabled="actionDisabled || !revokeReason.trim()"
              :is-loading="actionName === 'revoke-approval'"
              >Confirm revocation</Button
            >
          </form>
        </template>
      </section>
      <section class="workspace-card space-y-4 p-6">
        <p class="eyebrow">Manual handoff</p>
        <h2 class="text-lg font-semibold">Prepare an approved package</h2>
        <p class="text-sm leading-6 text-muted-foreground">
          The ZIP contains caption.txt, a source manifest, and the selected original media. Preparation does not confirm
          download or publication.
        </p>
        <Button
          v-if="workspace.canEdit.value"
          :is-disabled="actionDisabled || saved?.status !== 'approved'"
          :is-loading="actionName === 'export'"
          @click="prepareExport"
          >Prepare ZIP</Button
        >
        <div v-if="prepared" class="rounded-xl bg-muted p-4 text-sm">
          <p role="status">Package prepared from revision {{ prepared.revision }}.</p>
          <Button v-if="downloadUrl" as-child variant="outline" class="mt-3" :is-disabled="access.busy.value"
            ><a :href="downloadUrl">Download approved ZIP</a></Button
          >
          <p v-else role="alert" class="form-error mt-3">The service returned an unexpected download location.</p>
          <p class="mt-3 text-xs text-muted-foreground">
            Publication remains unverified. Prepared copies cannot be recalled.
          </p>
        </div>
      </section>
    </aside>
  </div>
</template>
