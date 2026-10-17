<script setup lang="ts">
import { notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { onScopeDispose, ref, watch } from 'vue';

import { AppErrorFactory, ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { useAppForm } from '@wow-two-beta/fe/forms-engine/house';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Field, FileUploadPicker, TextAreaInput } from '@wow-two-beta/fe/presentation/forms';

import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

const { t } = useUiLocale();

/** Imports explicitly selected image bytes and their provenance into the current organization. */
defineOptions({ name: 'MediaPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const selectedFile = ref<File | null>(null);
const fileError = ref('');
const uploaded = ref(false);
const form = useAppForm(() => ({
  defaultValues: { provenance: '' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    if (!selectedFile.value) {
      fileError.value = 'Choose a PNG or JPEG image.';
      return ResultExtensions.fail(AppErrorFactory.validation());
    }
    const result = await props.workspace.uploadMedia(selectedFile.value, values.provenance, signal);
    if (result.ok) {
      form.reset();
      selectedFile.value = null;
      uploaded.value = true;
    }
    if (!result.ok && result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
    return result;
  },
}));
watch(
  () => form.state.isDirty || selectedFile.value !== null,
  (dirty) => {
    props.access.setDirty(dirty);
  },
);
onScopeDispose(() => {
  props.access.setDirty(false);
});
function selectFile(files: readonly File[], rejected: readonly unknown[]): void {
  selectedFile.value = files[0] ?? null;
  fileError.value = rejected.length ? 'Use one PNG or JPEG image up to 5 MiB.' : '';
  uploaded.value = false;
}
</script>

<template>
  <p class="eyebrow">{{ t("Originals & provenance") }}</p>
  <h1 class="page-title">{{ t("Media library") }}</h1>
  <p class="mb-7 max-w-2xl text-muted-foreground">{{ t("Keep your original images together with their source details.") }}</p>
  <div class="grid items-start gap-8 lg:grid-cols-[1fr_1.4fr]">
    <form
      v-if="workspace.canEdit.value"
      class="workspace-card space-y-5 p-6"
      @submit.prevent="form.handleSubmit($event)"
    >
      <h2 class="text-xl font-semibold">{{ t("Add an image") }}</h2>
      <FileUploadPicker
        accept="image/png,image/jpeg"
        :max-size="5 * 1024 * 1024"
        :max-files="1"
        :is-disabled="access.busy.value"
        :label="t('Choose an original image')"
        hint="PNG or JPEG · up to 5 MiB"
        @files-change="selectFile"
      />
      <p v-if="selectedFile" class="break-all text-sm">{{ t("Selected:") }} {{ selectedFile.name }}</p>
      <p v-if="fileError" role="alert" class="form-error">{{ t(fileError) }}</p>
      <form.Field v-slot="f" name="provenance" is-required
        ><Field
          :label="t('Source and provenance')"
          :helper="t('Record the creator and source, and whether this is a photograph or a visualization.')"
          ><TextAreaInput v-model="f.value" is-required :maxlength="2000" :rows="4" @blur="f.onBlur" /></Field
      ></form.Field>
      <p v-if="uploaded" role="status" class="text-sm text-success-soft-foreground">{{ t("Image added.") }}</p>
      <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value || !selectedFile"
        >{{ t("Upload image") }}</Button
      >
    </form>
    <section :aria-label="t('Uploaded media')" class="grid gap-4 sm:grid-cols-2">
      <article v-for="asset in workspace.media.value" :key="asset.id" class="workspace-card overflow-hidden">
        <img
          :src="`/api/organizations/${asset.organizationId}/media/${asset.id}/content`"
          :alt="asset.fileName"
          loading="lazy"
          class="aspect-[4/3] w-full bg-muted object-contain"
        />
        <div class="space-y-3 p-4">
          <h2 class="break-all text-sm font-semibold">{{ asset.fileName }}</h2>
          <p class="text-xs text-muted-foreground">
            {{ (asset.byteLength / 1024 / 1024).toFixed(2) }} {{ t("MiB ·") }} {{ asset.contentType }}
          </p>
          <p class="whitespace-pre-wrap break-words text-sm leading-6">{{ asset.provenance }}</p>
          <details class="text-xs text-muted-foreground">
            <summary>{{ t("Integrity fingerprint") }}</summary>
            <p class="mt-2 break-all">SHA-256 {{ asset.sha256 }}</p>
          </details>
        </div>
      </article>
      <p v-if="!workspace.media.value?.length" class="empty-state sm:col-span-2">
        {{ t("No media has been uploaded to this organization.") }}
      </p>
    </section>
  </div>
</template>
