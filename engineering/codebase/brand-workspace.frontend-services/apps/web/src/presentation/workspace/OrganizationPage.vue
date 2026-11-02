<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';

import { Badge } from '@wow-two-beta/fe/presentation/display';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

const { t } = useUiLocale();

/** Lists the signed-in person's organization; organizations and members are managed in Ocharo Identity. */
defineOptions({ name: 'OrganizationPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; catalogueEntry?: boolean }>();
const organizationPath = (id: string) => `/organizations/${id}${props.catalogueEntry ? '/catalogue' : ''}`;
</script>

<template>
  <div class="grid gap-8">
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
          {{ t("Your account has no organization yet. Ask an Ocharo owner or admin for an invite.") }}
        </p>
      </div>
    </section>
  </div>
</template>
