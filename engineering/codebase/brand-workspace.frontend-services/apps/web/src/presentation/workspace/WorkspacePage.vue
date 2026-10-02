<script setup lang="ts">
import { watch } from 'vue';
import { notifyError } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import CatalogueWorkspacePage from './CatalogueWorkspacePage.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import UiLanguagePicker from '@/presentation/shared/UiLanguagePicker.vue';
import BuildIdentity from '../shared/BuildIdentity.vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';
import { AppShell, AppShellContent, AppShellHeader, AppShellMain } from '@wow-two-beta/ui-vue/presentation/layout';

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

const { t } = useUiLocale();

/** Composes real organization context with the preparation pages. */
defineOptions({ name: 'WorkspacePage' });
const props = defineProps<{ access: AccessControls }>();
defineSlots<{ diagnostics(): unknown }>();
const currentPath = window.location.pathname;
const location = readWorkspaceLocation(currentPath);
const designTarget = readDesignTarget(window.location.search);
const workspace = useWorkspace(props.access, location);
watch(workspace.error, failure => { if (failure) notifyError(failureMessage(failure), 'workspace-load'); });
watch(workspace.refreshError, failure => { if (failure) notifyError('Some workspace information could not refresh. Your design remains open.', 'workspace-refresh'); });
function changeOrganization(event: Event): void {
  const select = event.target as HTMLSelectElement;
  const id = select.value;
  select.value = location.organizationId ?? '';
  props.access.navigate(id ? `/organizations/${id}` : '/');
}
</script>

<template>
  <AppShell sidebar-width="0px" class="min-h-screen bg-background text-foreground">
    <AppShellHeader class="h-auto min-h-14 border-b border-border bg-card px-0 py-4">
      <div class="mx-auto flex w-full max-w-[1600px] flex-wrap items-center justify-between gap-4 px-5 sm:px-9">
        <div class="flex items-center gap-3">
          <span class="brand-mark" aria-hidden="true">O</span
          ><span
            ><b class="block text-sm">{{ t("Ocharo workspace") }}</b
            ><span class="text-xs text-muted-foreground">{{ t("Your creative business") }}</span></span
          >
        </div>
        <div class="flex flex-wrap items-center gap-3">
          <UiLanguagePicker />
          <span class="text-sm text-muted-foreground">{{ access.session.value?.user?.displayName }}</span
          ><Button variant="ghost" size="sm" :is-disabled="access.busy.value" @click="access.logout">{{ t("Sign out") }}</Button>
        </div>
      </div>
    </AppShellHeader>
    <AppShellMain class="w-full min-w-0">
      <AppShellContent class="mx-auto w-full max-w-[1600px] px-5 pb-12 sm:px-9">
        <div class="my-6 flex flex-wrap items-center justify-between gap-4 border-b border-border pb-5">
          <label class="flex min-w-0 flex-wrap items-center gap-3 text-sm"
            ><span class="text-muted-foreground">{{ t("Organization") }}</span
            ><select
              :aria-label="t('Organization')"
              class="native-select w-auto max-w-full"
              :value="location.organizationId ?? ''"
              :disabled="access.busy.value || workspace.loading.value"
              @change="changeOrganization"
            >
              <option value="">{{ t("All organizations") }}</option>
              <option v-for="org in workspace.organizations.value" :key="org.id" :value="org.id">
                {{ org.name }}
              </option></select
            ><Badge v-if="workspace.organization.value" variant="outline">{{ t(workspace.organization.value.role) }}</Badge></label
          >
          <Button size="sm" variant="outline" :is-disabled="access.busy.value || access.dirty.value || workspace.loading.value || workspace.refreshing.value" :is-loading="workspace.refreshing.value" @click="workspace.refresh()">{{ t("Refresh") }}</Button>
          <nav :aria-label="t('Workspace sections')" class="flex flex-wrap gap-1">
            <template v-if="location.organizationId"
              ><Button
                v-for="section in ['overview', 'products', 'catalogue', 'business-cards', 'content', 'media', 'brands', 'integrations'] as const"
                :key="section"
                size="sm"
                :variant="location.section === section ? 'soft' : 'ghost'"
                :aria-current="location.section === section ? 'page' : undefined"
                :is-disabled="access.busy.value"
                @click="
                  access.navigate(
                    `/organizations/${location.organizationId}${section === 'overview' ? '' : `/${section}`}`,
                  )
                "
                >{{ t(({overview:'Overview',products:'Products & releases',integrations:'Connected apps',catalogue:'Catalogues','business-cards':'Business cards',content:'Content',media:'Media',brands:'Brands'})[section]) }}</Button
              ></template
            >
            <Button size="sm" variant="ghost" :is-disabled="access.busy.value" @click="access.navigate('/system')"
              >{{ t("System") }}</Button
            >
          </nav>
        </div>
        <div>
          <ContentSkeleton v-if="workspace.initialLoading.value" :variant="location.section === 'products' ? 'form' : location.section === 'media' || location.section === 'catalogue' ? 'gallery' : 'rows'" :label="t('Loading your workspace…')" />
          <div v-else :aria-busy="workspace.loading.value || workspace.refreshing.value">
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
        </div>
        <footer class="mt-12 border-t border-border pt-5 text-xs leading-5 text-muted-foreground">
          {{ t("Ocharo · A place for your collection, identity, and stories. Social publishing is not connected.") }}
          <div class="mt-3"><span class="mr-2">{{ t("About this app") }}</span><BuildIdentity /></div>
        </footer>
      </AppShellContent>
    </AppShellMain>
  </AppShell>
</template>
