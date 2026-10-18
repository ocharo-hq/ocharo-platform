<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import AccountSettings from './AccountSettings.vue';
import { notifyError } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import CatalogueWorkspacePage from './CatalogueWorkspacePage.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import DeskFrame from './frame/DeskFrame.vue';

import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { useWorkspace } from '@/application/workspace/UseWorkspace';
import { readWorkspaceLocation } from '@/domain/workspace/Models';
import { readDesignTarget } from '@/domain/workspace/EditorNavigation';

import BrandsPage from './BrandsPage.vue';
import CommercePage from './CommercePage.vue';
import IntegrationsPage from './IntegrationsPage.vue';
import DesignEditorPage from './DesignEditorPage.vue';
import OverviewPage from './OverviewPage.vue';
import DraftEditorPage from './DraftEditorPage.vue';
import DraftListPage from './DraftListPage.vue';
import MediaPage from './MediaPage.vue';
import OrganizationPage from './OrganizationPage.vue';
import EventsPage from './EventsPage.vue';
import SourcingPage from './SourcingPage.vue';

const { t } = useUiLocale();

/**
 * Composes real organization context with the preparation pages inside the Ocharo frame. The route picks the section;
 * Sourcing and Events move between their own views in place, and every other page opens fresh on each navigation, as
 * it did when each move loaded a new document.
 */
defineOptions({ name: 'WorkspacePage' });
const props = defineProps<{ access: AccessControls }>();
defineSlots<{ diagnostics(): unknown }>();
const route = useRoute();
const accountOpen = ref(false);
const location = computed(() => readWorkspaceLocation(route.path));
const designTarget = computed(() => readDesignTarget(new URL(route.fullPath, window.location.origin).search));
const workspace = useWorkspace(props.access, location);
/** Sourcing and Events keep their page across their own views; the rest remount per address. @internal */
const pageKey = computed(() =>
  location.value.section === 'sourcing' || location.value.section === 'events' ? location.value.section : route.fullPath,
);
watch(workspace.error, failure => { if (failure) notifyError(failureMessage(failure), 'workspace-load'); });
watch(workspace.refreshError, failure => { if (failure) notifyError('Some workspace information could not refresh. Your design remains open.', 'workspace-refresh'); });
</script>

<template>
  <DeskFrame :access="access" :workspace="workspace" :location="location" @settings="accountOpen = true">
    <AccountSettings v-if="accountOpen" :access="access" @close="accountOpen = false" />
    <ContentSkeleton v-if="workspace.initialLoading.value" :variant="location.section === 'products' || location.section === 'sourcing' ? 'form' : location.section === 'media' || location.section === 'catalogue' ? 'gallery' : 'rows'" :label="t('Loading your workspace…')" />
    <div v-else :key="pageKey" :aria-busy="workspace.loading.value || workspace.refreshing.value">
      <p v-if="workspace.error.value" role="status" class="query-label">{{ t("Workspace unavailable") }}</p>
      <slot v-else-if="location.section === 'system'" name="diagnostics" />
      <OrganizationPage v-else-if="!location.organizationId" :access="access" :workspace="workspace" :catalogue-entry="location.section === 'catalogue'" />
      <section v-else-if="!workspace.organization.value" role="status" class="query-label">
        <h1 class="text-xl font-semibold">{{ t("Organization unavailable") }}</h1>
        <p class="mt-3 text-sm text-muted-foreground">{{ t("Choose one of your current memberships.") }}</p>
      </section>
      <OverviewPage v-else-if="location.section === 'overview'" :access="access" :workspace="workspace" />
      <CatalogueWorkspacePage v-else-if="location.section === 'catalogue'" :access="access" :workspace="workspace" :initial-document-id="designTarget.documentId" :initial-page-id="designTarget.pageId" :navigation-error="designTarget.error" :initial-mode="designTarget.mode" />
      <DesignEditorPage v-else-if="location.section === 'business-cards'" :access="access" :workspace="workspace" kind="business-card" :initial-document-id="designTarget.documentId" :initial-page-id="designTarget.pageId" :navigation-error="designTarget.error" />
      <CommercePage v-else-if="location.section === 'products'" :access="access" :workspace="workspace" />
      <SourcingPage v-else-if="location.section === 'sourcing'" :access="access" :workspace="workspace" :initial-location="location.sourcing" />
      <EventsPage v-else-if="location.section === 'events'" :access="access" :workspace="workspace" :initial-location="location.events" />
      <IntegrationsPage v-else-if="location.section === 'integrations'" :access="access" :workspace="workspace" />
      <BrandsPage v-else-if="location.section === 'brands'" :access="access" :workspace="workspace" />
      <MediaPage v-else-if="location.section === 'media'" :access="access" :workspace="workspace" />
      <DraftEditorPage
        v-else-if="location.draftId === 'new' || workspace.draft.value"
        :access="access"
        :workspace="workspace"
      />
      <DraftListPage v-else :access="access" :workspace="workspace" />
    </div>
  </DeskFrame>
</template>
