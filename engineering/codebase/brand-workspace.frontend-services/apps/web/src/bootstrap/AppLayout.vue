<script setup lang="ts">
import { computed, onMounted, ref, useTemplateRef, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { ChevronRight, RotateCw, UserCog } from 'lucide-vue-next';
import { TailwindBreakpoints, useBreakpoint } from '@wow-two-beta/fe/foundation/device';
import { localStorageStorageBroker, usePersistentState } from '@wow-two-beta/fe/foundation/storage';
import {
  AppShell,
  AppShellContent,
  AppShellHeader,
  AppShellMain,
  AppShellSidebar,
} from '@wow-two-beta/fe/presentation/layout';
import {
  AccountMenu,
  MenuItem,
  type AccountMenuLocale,
  type AccountMenuOrganization,
  type AccountMenuVersion,
} from '@wow-two-beta/fe/presentation/nav';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { failureMessage, type AccessControls } from '@/application/common/hooks/UseAccess';
import { useBuildIdentity } from '@/application/common/hooks/UseBuildIdentity';
import { notifyError } from '@/application/common/hooks/UseFeedback';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import { useWorkspace } from '@/application/common/hooks/UseWorkspace';
import { DeskGroupLabels, DeskSections } from '@/domain/common/DeskSections';
import { readWorkspaceLocation } from '@/domain/common/models/Workspace';
import AccountSettings from '@/presentation/access/AccountSettings.vue';
import ContentSkeleton from '@/presentation/common/ContentSkeleton.vue';
import RouteFailure from '@/presentation/common/RouteFailure.vue';
import DeskNav from '@/presentation/frame/DeskNav.vue';
import OcharoAppsMenu from '@/presentation/frame/OcharoAppsMenu.vue';
import PhoneNav from '@/presentation/frame/PhoneNav.vue';

import { deskPageProps, readDeskPlace } from './router/DeskPlaces';

/** The sidebar's widths: full, and the icon rail. @internal */
const SidebarWidth = { Full: '244px', Rail: '68px' } as const;
/** Where this browser remembers the desktop sidebar's rail choice; existing choices keep reading from it. @internal */
const SidebarCollapsedKey = 'ocharo.desk.sidebar-collapsed';
/** The interface languages, each named in its own language. @internal */
const UiLanguages: readonly AccountMenuLocale[] = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
];

/**
 * The Ocharo frame, as Desk wears it, around every place. The glass top bar is the suite's: the app button, where you
 * are, and the account menu. The sidebar is Desk's own sections; it collapses to an icon rail on desktops and is always
 * the rail on tablets. Phones swap it for a bottom bar. Only the main region scrolls.
 *
 * The frame owns the document's organization reads, so they stay loaded while places swap. Sourcing and Events keep
 * their page across their own views; every other place opens fresh on each navigation, as it did when each move loaded
 * a new document. A membership the session does not hold reads as an unavailable organization, and a place that fails
 * to load or render shows its failure inside the frame.
 */
defineOptions({ name: 'AppLayout' });
const props = defineProps<{ access: AccessControls }>();
const { t, uiLocale, setUiLocale } = useUiLocale();
const { backendVersion, refresh: refreshBackendVersion } = useBuildIdentity();
const route = useRoute();
// The stored choice seeds the first render too, so a collapsed rail never animates in from the full sidebar.
const collapsed = usePersistentState(SidebarCollapsedKey, localStorageStorageBroker.read<boolean>(SidebarCollapsedKey) === true);
const breakpoint = useBreakpoint(TailwindBreakpoints);
const main = useTemplateRef<{ el: HTMLElement | null }>('main');
const accountOpen = ref(false);
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
  { label: 'Desk', version: __APP_VERSION__ },
  { label: 'API', version: t(backendVersion.value) },
]);
/** Sections need an organization; the organization chooser shows the frame without them. */
const hasSections = computed(() => Boolean(organization.value));
const isDesktop = computed(() => ['lg', 'xl', '2xl'].includes(breakpoint.value ?? ''));
const isRail = computed(() => !isDesktop.value || collapsed.value.value);
const sidebarWidth = computed(() => (hasSections.value ? (isRail.value ? SidebarWidth.Rail : SidebarWidth.Full) : '0px'));
/** Sections without a refresh control of their own reload the shared workspace reads from the top bar. */
const sharedRefresh = computed(() => hasSections.value && ['media', 'brands', 'content', 'integrations'].includes(section.value ?? ''));
const refreshDisabled = computed(
  () => props.access.busy.value || props.access.dirty.value || workspace.loading.value || workspace.refreshing.value,
);
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
watch(section, () => main.value?.el?.scrollTo?.({ top: 0 }));
watch(
  () => route.path,
  () => {
    if (!location.value.organizationId && section.value !== 'system') main.value?.el?.scrollTo?.({ top: 0 });
  },
);
</script>

<template>
  <AppShell
    class="desk-frame surface-ambient transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none"
    sidebar-breakpoint="md"
    :sidebar-width="sidebarWidth"
  >
    <AppShellHeader height="auto" variant="glass" class="p-0 pt-[env(safe-area-inset-top)]">
      <div class="flex min-h-15 w-full min-w-0 items-center gap-3 px-3 sm:px-4">
        <OcharoAppsMenu current="desk" label="Desk" :subtitle="organization?.name ?? null" />
        <nav
          v-if="hasSections || section === 'system' || section === null"
          :aria-label="t('You are here')"
          class="hidden min-w-0 items-center gap-1.5 border-s border-border ps-3 text-sm text-muted-foreground md:flex"
        >
          <template v-for="(crumb, index) in crumbs" :key="index">
            <ChevronRight v-if="index > 0" :size="14" class="shrink-0" aria-hidden="true" />
            <span
              :class="index === crumbs.length - 1 ? 'truncate font-semibold text-foreground' : 'shrink-0'"
              :aria-current="index === crumbs.length - 1 ? 'page' : undefined"
              >{{ crumb }}</span
            >
          </template>
        </nav>
        <span class="flex-1" />
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
        <AccountMenu
          :name="accountName"
          :description="accountEmail"
          :avatar-src="user?.picture ?? null"
          :organizations="memberships"
          :organization="organization?.id ?? ''"
          :locales="UiLanguages"
          :locale="uiLocale"
          :versions="versions"
          :is-busy="access.busy.value"
          variant="glass-menu"
          @update:organization="switchOrganization"
          @update:locale="setUiLocale"
          @sign-out="access.logout"
        >
          <template #items>
            <MenuItem @select="accountOpen = true"><UserCog :size="15" aria-hidden="true" />{{ t('Account settings') }}</MenuItem>
          </template>
        </AccountMenu>
      </div>
    </AppShellHeader>
    <AppShellSidebar v-if="hasSections" variant="glass">
      <DeskNav
        :organization-id="organization!.id"
        :current="section"
        :collapsed="isRail"
        :can-toggle="isDesktop"
        @toggle="collapsed.setValue((previous) => !previous)"
      />
    </AppShellSidebar>
    <AppShellMain ref="main" class="min-w-0">
      <AppShellContent gutter="lg" max-width="2xl" class="pb-12 pt-6 lg:px-8 xl:px-10">
        <AccountSettings v-if="accountOpen" :access="access" @close="accountOpen = false" />
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
      </AppShellContent>
    </AppShellMain>
    <PhoneNav v-if="hasSections" :organization-id="organization!.id" :current="section" />
  </AppShell>
</template>
