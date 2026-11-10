<script setup lang="ts">
import { computed, onMounted, ref, useTemplateRef, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { RotateCw, UserCog } from 'lucide-vue-next';
import { useRunningVersion } from '@wow-two-beta/fe/foundation/net';
import { AppFrame, type AppFrameAccount, type AppFrameSwitcher } from '@wow-two-beta/fe/presentation/layout';
import {
  AppSwitcherKind,
  MenuItem,
  type AccountMenuLocale,
  type AccountMenuOrganization,
  type AccountMenuVersion,
  type AppSwitcherItem,
} from '@wow-two-beta/fe/presentation/nav';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { failureMessage, type AccessControls } from '@/application/common/hooks/UseAccess';
import { useBuildIdentity } from '@/application/common/hooks/UseBuildIdentity';
import { notifyError } from '@/application/common/hooks/UseFeedback';
import { useOcharoApps } from '@/application/common/hooks/UseOcharoApps';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import { useWorkspace } from '@/application/common/hooks/UseWorkspace';
import { DeskGroupLabels, DeskSections } from '@/domain/common/DeskSections';
import { OcharoAppKind } from '@/domain/common/models/OcharoApps';
import { readWorkspaceLocation } from '@/domain/common/models/Workspace';
import AccountSettings from '@/presentation/access/AccountSettings.vue';
import ContentSkeleton from '@/presentation/common/ContentSkeleton.vue';
import RouteFailure from '@/presentation/common/RouteFailure.vue';
import DeskNav from '@/presentation/frame/DeskNav.vue';
import PhoneNav from '@/presentation/frame/PhoneNav.vue';
import { AppIcons } from '@/presentation/frame/ShellIcons';

import { deskPageProps, readDeskPlace } from './router/DeskPlaces';

/** Where this browser remembers the desktop sidebar's rail choice; the key predates the shared frame. @internal */
const SidebarCollapsedKey = 'ocharo.desk.sidebar-collapsed';
/** The interface languages, each named in its own language. @internal */
const UiLanguages: readonly AccountMenuLocale[] = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
];

/**
 * Desk inside the frame every Ocharo app shares (the fe sdk's `AppFrame`): the bar, the sidebar and the page column are
 * the suite's. Desk passes what is its own: the app list with the open organization, its sections, where you are, the
 * refresh, the account with its organizations and languages, and its notices above the page.
 *
 * The frame owns the document's organization reads, so they stay loaded while places swap. Sourcing and Events keep
 * their page across their own views; every other place opens fresh on each navigation, as it did when each move loaded
 * a new document. A membership the session does not hold reads as an unavailable organization, and a place that fails
 * to load or render shows its failure inside the frame.
 */
defineOptions({ name: 'AppLayout' });
const props = defineProps<{ access: AccessControls }>();
const { t, uiLocale, setUiLocale } = useUiLocale();
const { backendVersion, refresh: refreshBackendVersion, readRunningVersion } = useBuildIdentity();
const apps = useOcharoApps();
const appVersion = __APP_VERSION__;
// The badge beside the app button follows the release the API serves; a local build has no release to compare.
const { version: runningVersion } = useRunningVersion(() => (appVersion === 'dev' ? null : readRunningVersion()));
const route = useRoute();
const frame = useTemplateRef<{ scrollToTop: () => void }>('frame');
const settingsOpen = ref(false);
const location = computed(() => readWorkspaceLocation(route.path));
const workspace = useWorkspace(props.access, location);
/** The open place's sidebar section; `null` for an address outside every section. */
const section = computed(() => readDeskPlace(route)?.section ?? null);
/** Sourcing and Events keep their page across their own views; the rest remount per address. @internal */
const pageKey = computed(() => (section.value === 'sourcing' || section.value === 'events' ? section.value : route.fullPath));

const organization = computed(() => workspace.organization.value ?? null);
const user = computed(() => props.access.session.value?.user ?? null);
/** The account menu names the person; the email stands in when no display name is set. */
const accountName = computed(() => user.value?.displayName || user.value?.email || '');
const accountEmail = computed(() => (user.value?.displayName ? user.value.email : ''));
const memberships = computed<AccountMenuOrganization[]>(() =>
  (workspace.organizations.value ?? []).map((org) => ({ id: org.id, label: org.name, description: t(org.role) })),
);
const versions = computed<AccountMenuVersion[]>(() => [
  { label: 'Desk', version: appVersion },
  { label: 'API', version: t(backendVersion.value) },
]);
/** Sections need an organization; the organization chooser shows the frame without them. */
const hasSections = computed(() => Boolean(organization.value));
/** Sections without a refresh control of their own reload the shared workspace reads from the top bar. */
const sharedRefresh = computed(() => hasSections.value && ['media', 'brands', 'content', 'integrations'].includes(section.value ?? ''));
const refreshDisabled = computed(
  () => props.access.busy.value || props.access.dirty.value || workspace.loading.value || workspace.refreshing.value,
);
const items = computed<AppSwitcherItem[]>(() =>
  apps.map((app) => ({
    key: app.key,
    label: t(app.label),
    description: t(app.summary),
    href: app.url,
    icon: AppIcons[app.key],
    kind: app.kind === OcharoAppKind.Workspace ? AppSwitcherKind.App : AppSwitcherKind.Site,
  })),
);
/** The app menu's own phrases. */
const switcher = computed<AppFrameSwitcher>(() => ({
  title: t('Ocharo apps'),
  sitesLabel: t('Public sites'),
  closeLabel: t('Close'),
  newTabLabel: t('(opens in a new tab)'),
  triggerLabel: t('Ocharo apps — {app}'),
}));
/** The account menu: who is signed in, the organizations, the versions, and sign-out while it runs. */
const account = computed<AppFrameAccount>(() => ({
  name: accountName.value,
  description: accountEmail.value,
  avatarSrc: user.value?.picture ?? null,
  organizations: memberships.value,
  locales: UiLanguages,
  versions: versions.value,
  isBusy: props.access.busy.value,
  onSignOut: props.access.logout,
}));
/** The section's group, then the place itself. */
const crumbs = computed(() => {
  const entry = DeskSections.find((item) => item.id === section.value);
  const group = entry ? DeskGroupLabels[entry.group] : null;
  const label = readDeskPlace(route)?.label;
  return [...(group ? [t(group)] : []), ...(label ? [t(label)] : [])];
});
const skeletonVariant = computed(() => {
  const current = location.value.section;
  return current === 'products' || current === 'sourcing' ? 'form' : current === 'media' || current === 'catalogue' ? 'gallery' : 'rows';
});

/** Switching organization replaces the document, so nothing private carries across. @internal */
function switchOrganization(id: string): void {
  void props.access.navigate(`/organizations/${id}`);
}

onMounted(() => void refreshBackendVersion());

watch(workspace.error, (failure) => { if (failure) notifyError(failureMessage(failure), 'workspace-load'); });
watch(workspace.refreshError, (failure) => {
  if (failure) notifyError('Some workspace information could not refresh. Your design remains open.', 'workspace-refresh');
});
/* A new section starts at the top of the scrolling region; a move inside a section keeps its place. */
watch(section, () => frame.value?.scrollToTop());
watch(
  () => route.path,
  () => {
    if (!location.value.organizationId && section.value !== 'system') frame.value?.scrollToTop();
  },
);
</script>

<template>
  <AppFrame
    ref="frame"
    :organization="organization?.id ?? ''"
    :locale="uiLocale"
    :version="appVersion"
    :running-version="runningVersion"
    :apps="items"
    current-app="desk"
    app-label="Desk"
    v-bind="organization ? { appSubtitle: organization.name } : {}"
    :app-switcher="switcher"
    :has-navigation="hasSections"
    :crumbs="hasSections || section === 'system' || section === null ? crumbs : []"
    :trail-label="t('You are here')"
    :account="account"
    :collapse-storage-key="SidebarCollapsedKey"
    @update:organization="switchOrganization"
    @update:locale="setUiLocale"
  >
    <template #brand><span class="oc-mark">O</span></template>
    <template #nav="{ isRail }"><DeskNav v-if="organization" :organization-id="organization.id" :current="section" :collapsed="isRail" /></template>
    <template #bar-end>
      <button
        v-if="sharedRefresh"
        type="button"
        class="grid size-11 shrink-0 place-items-center rounded-xl text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
        :aria-label="t('Refresh')"
        :title="t('Refresh')"
        :disabled="refreshDisabled"
        @click="workspace.refresh()"
      >
        <RotateCw :size="18" :class="{ 'animate-spin motion-reduce:animate-none': workspace.refreshing.value }" aria-hidden="true" />
      </button>
    </template>
    <template #account-items>
      <MenuItem @select="settingsOpen = true"><UserCog :size="15" aria-hidden="true" />{{ t('Account settings') }}</MenuItem>
    </template>
    <template #phone-nav><PhoneNav v-if="organization" :organization-id="organization.id" :current="section" /></template>
    <template #notice>
      <AccountSettings v-if="settingsOpen" :access="access" @close="settingsOpen = false" />
    </template>
    <ContentSkeleton v-if="workspace.initialLoading.value" :variant="skeletonVariant" :label="t('Loading your workspace…')" />
    <div v-else :key="pageKey" :aria-busy="workspace.loading.value || workspace.refreshing.value">
      <p v-if="workspace.error.value" role="status" class="query-label">{{ t("Workspace unavailable") }}</p>
      <section v-else-if="location.organizationId && !organization" role="status" class="query-label">
        <h1 class="text-xl font-semibold">{{ t("Organization unavailable") }}</h1>
        <p class="mt-3 text-sm text-muted-foreground">{{ t("Choose one of your current memberships.") }}</p>
      </section>
      <AppErrorBoundary v-else>
        <RouterView v-slot="{ Component, route: current }"
          ><component :is="Component" v-bind="deskPageProps(current, { access, workspace })"
        /></RouterView>
        <template #fallback="{ reset }"><RouteFailure @retry="reset" /></template>
      </AppErrorBoundary>
    </div>
  </AppFrame>
</template>
