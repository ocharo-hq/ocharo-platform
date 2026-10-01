<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

const { t } = useUiLocale();

/** Lists real saved draft heads, without fabricated activity or publishing metrics. */
defineOptions({ name: 'DraftListPage' });
defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
</script>

<template>
  <div class="mb-8 flex flex-wrap items-end justify-between gap-4">
    <div>
      <p class="eyebrow">{{ t("Content & preparation") }}</p>
      <h1 class="page-title mb-2">{{ t("A clear path to ready.") }}</h1>
      <p class="text-muted-foreground">{{ t("Draft, review, and prepare an approved package for manual sharing.") }}</p>
    </div>
    <Button
      v-if="workspace.canEdit.value"
      :is-disabled="access.busy.value || !workspace.brands.value?.length"
      @click="access.navigate(`/organizations/${workspace.organization.value!.id}/drafts/new`)"
      >{{ t("New draft") }}</Button
    >
  </div>
  <div v-if="!workspace.brands.value?.length" class="workspace-card mb-6 p-6">
    <h2 class="font-semibold">{{ t("Start with a brand") }}</h2>
    <p class="mt-2 text-sm text-muted-foreground">
      {{ t("A draft belongs to a brand. An organization owner can create the first one.") }}
    </p>
    <Button
      variant="outline"
      class="mt-4"
      :is-disabled="access.busy.value"
      @click="access.navigate(`/organizations/${workspace.organization.value!.id}/brands`)"
      >{{ t("Open brands") }}</Button
    >
  </div>
  <section :aria-label="t('Saved drafts')" class="space-y-3">
    <button
      v-for="draft in workspace.drafts.value"
      :key="draft.id"
      class="workspace-card flex w-full flex-wrap items-center justify-between gap-4 p-5 text-left disabled:opacity-50 sm:p-6"
      :disabled="access.busy.value"
      @click="access.navigate(`/organizations/${workspace.organization.value!.id}/drafts/${draft.id}`)"
    >
      <span class="min-w-0"
        ><b class="block break-words">{{ draft.title }}</b
        ><span class="mt-2 block text-sm text-muted-foreground"
          >{{ workspace.brands.value?.find((brand) => brand.id === draft.brandId)?.name }} {{ t("· Revision") }}
          {{ draft.revision }}</span
        ></span
      >
      <span class="flex flex-wrap items-center gap-3"
        ><Badge
          :variant="draft.status === 'approved' ? 'success' : draft.status === 'in-review' ? 'warning' : 'neutral'"
          >{{ t(draft.status === 'in-review' ? 'In review' : draft.status === 'approved' ? 'Approved' : 'Draft') }}</Badge
        ><time class="text-xs text-muted-foreground" :datetime="draft.updatedAtUtc">{{
          new Date(draft.updatedAtUtc).toLocaleDateString()
        }}</time></span
      >
    </button>
    <p v-if="!workspace.drafts.value?.length" class="empty-state">
      {{ t("No drafts yet. Start with one real piece of content.") }}
    </p>
  </section>
</template>
