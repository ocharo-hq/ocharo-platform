<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { ChevronRight, ExternalLink, RefreshCw, X } from 'lucide-vue-next';
import { TailwindBreakpoints, useBreakpoint } from '@wow-two-beta/fe/foundation/device';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { localStorageStorageBroker, usePersistentState } from '@wow-two-beta/fe/foundation/storage';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import {
  AppShell,
  AppShellContent,
  AppShellHeader,
  AppShellMain,
  AppShellSidebar,
} from '@wow-two-beta/fe/presentation/layout';
import {
  AccountMenu,
  AppSwitcherKind,
  AppSwitcherMenu,
  MenuItem,
  type AccountMenuVersion,
  type AppSwitcherItem,
} from '@wow-two-beta/fe/presentation/nav';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { useBrand } from '../application/brand';
import { useBuildIdentity } from '../application/common/UseBuildIdentity';
import { notifyError } from '../application/common/UseFeedback';
import { useSsoSession } from '../application/common/UseSsoSession';
import { useOcharoApps } from '../application/shared/UseOcharoApps';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { BrandGroupLabels, brandSection, readBrandSection } from '../domain/shared/BrandSections';
import { OcharoAppKind } from '../domain/shared/OcharoApps';
import BrandQueryState from '../presentation/common/BrandQueryState.vue';
import SsoConnection from '../presentation/common/SsoConnection.vue';
import BrandNav from '../presentation/frame/BrandNav.vue';
import PhoneNav from '../presentation/frame/PhoneNav.vue';
import { AppIcons } from '../presentation/frame/ShellIcons';

/** The sidebar's widths: full, and the icon rail. @internal */
const SidebarWidth = { Full: '244px', Rail: '68px' } as const;
/** Where this browser remembers the desktop sidebar's rail choice. @internal */
const SidebarCollapsedKey = 'ocharo.brand.sidebar-collapsed';

/**
 * The Ocharo frame, as Brand wears it, around every place. The glass top bar is the suite's: the app button with the
 * app version, where you are, the release this draft stands on with a refresh, and the account menu. The sidebar is
 * Brand's sections; it collapses to an icon rail on desktops and is always the rail on tablets. Phones swap it for a
 * bottom bar. Only the main region scrolls.
 */
defineOptions({ name: 'AppLayout' });
const { t } = useUiLocale();
const route = useRoute();
const brand = useBrand();
const { session: ssoSession, signingOut, signOut } = useSsoSession();
const { backendVersion, refresh: refreshBackendVersion } = useBuildIdentity();
const apps = useOcharoApps();
const appVersion = __APP_VERSION__;
// The stored choice seeds the first render too, so a collapsed rail never animates in from the full sidebar.
const collapsed = usePersistentState(
  SidebarCollapsedKey,
  localStorageStorageBroker.read<boolean>(SidebarCollapsedKey) === true,
);
const breakpoint = useBreakpoint(TailwindBreakpoints);

const isDesktop = computed(() => ['lg', 'xl', '2xl'].includes(breakpoint.value ?? ''));
const isRail = computed(() => !isDesktop.value || collapsed.value.value);
const sidebarWidth = computed(() => (isRail.value ? SidebarWidth.Rail : SidebarWidth.Full));
const section = computed(() => readBrandSection(route.meta));
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
/** The group, the section and the place itself, each once: the place's label comes from its route metadata. */
const crumbs = computed(() => {
  const entry = brandSection(section.value);
  const group = entry ? BrandGroupLabels[entry.group] : null;
  const place = typeof route.meta.label === 'string' ? route.meta.label : null;
  const labels = [group, entry?.label ?? null, place].filter((label): label is string => Boolean(label));
  return labels.filter((label, index) => labels.indexOf(label) === index).map((label) => t(label));
});
/** The first-read placeholder each place shows until the draft arrives; Identity keeps its own editor mounted. */
const queryKind = computed(() =>
  section.value === 'logos' || section.value === 'appearances' || section.value === 'connect'
    ? section.value
    : 'overview',
);
/** The release the draft stands on, and how much waits to be published. */
const releaseStatus = computed(() => {
  const latest = brand.draft.value?.latestRelease ?? null;
  return latest === null
    ? t('Not released yet')
    : t('Release {release} · {count} unpublished changes', { release: latest, count: brand.pending.value.length });
});

/** Who is signed in, and sign-out, exist only with an Ocharo session; the local pilot has neither. */
const signedIn = computed(() => ssoSession.value.enabled && ssoSession.value.authenticated);
const accountOpen = ref(false);
const accountIdentity = computed(() =>
  signedIn.value ? { description: t('Signed in through Ocharo Desk'), onSignOut: signOut } : {},
);
/** The app's own version, then the API's from its status, with the commit the build carries split off. */
const versions = computed<AccountMenuVersion[]>(() => {
  const [api = '', commit] = backendVersion.value.split('+');
  return [
    { label: t('Brand'), version: appVersion },
    { label: 'API', version: t(api), ...(commit ? { commit } : {}) },
  ];
});
/** Opens Desk's account page in another tab, so unsaved edits here stay. */
function openDeskAccount(): void {
  if (ssoSession.value.accountUrl) window.open(ssoSession.value.accountUrl, '_blank', 'noopener,noreferrer');
}
watch(accountOpen, (open) => {
  if (open) void refreshBackendVersion();
});
watch(
  brand.error,
  (message) => {
    if (message) notifyError(message);
  },
  { immediate: true },
);

onMounted(() => void brand.refresh());
</script>

<template>
  <AppShell
    class="brand-frame surface-ambient transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none"
    sidebar-breakpoint="md"
    :sidebar-width="sidebarWidth"
  >
    <AppShellHeader
      height="auto"
      class="no-print oc-glass border-b border-(--oc-glass-edge) p-0 pt-[env(safe-area-inset-top)] shadow-(--oc-glass-shadow)"
    >
      <div class="flex min-h-15 w-full min-w-0 items-center gap-3 px-3 sm:px-4">
        <div class="flex shrink-0 items-center gap-2">
          <AppSwitcherMenu
            :items="items"
            current="brand"
            :label="t('Brand')"
            subtitle="Ocharo"
            :title="t('Ocharo apps')"
            :sites-label="t('Public sites')"
            :close-label="t('Close')"
            :new-tab-label="t('(opens in a new tab)')"
            :trigger-label="t('Ocharo apps — {app}')"
            panel-class="oc-glass-menu border-(--oc-glass-edge) text-foreground shadow-(--oc-menu-shadow)"
            class="[&[data-state=open]_[data-app-switcher-chevron]]:text-primary-soft-foreground"
          >
            <template #mark><span class="oc-mark">O</span></template>
          </AppSwitcherMenu>
          <span class="font-mono text-[11px] text-muted-foreground" :title="t('App version')">{{ appVersion }}</span>
        </div>
        <nav
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
        <p
          v-if="brand.draft.value"
          class="hidden shrink-0 text-xs text-muted-foreground lg:block"
          :aria-busy="brand.refreshing.value || undefined"
        >
          <template v-if="brand.refreshing.value"
            ><SkeletonState class="inline-block h-4 w-44 align-middle" /><span class="sr-only">{{
              t('Checking the release…')
            }}</span></template
          >
          <template v-else>{{ releaseStatus }}</template>
        </p>
        <Button
          variant="outline"
          class="shrink-0"
          :is-loading="brand.refreshing.value"
          :is-disabled="brand.mutating.value"
          @click="brand.refresh"
        >
          <template #leading><Icon :icon="RefreshCw" :size="15" /></template>
          <span class="max-sm:sr-only">{{ t('Refresh') }}</span>
        </Button>
        <AccountMenu
          v-model:open="accountOpen"
          v-bind="accountIdentity"
          :versions="versions"
          :is-busy="signingOut"
          panel-class="oc-glass-menu border-(--oc-glass-edge) text-foreground shadow-(--oc-menu-shadow)"
        >
          <template v-if="ssoSession.enabled && ssoSession.accountUrl" #items>
            <MenuItem @select="openDeskAccount"
              ><ExternalLink :size="15" class="shrink-0" aria-hidden="true" />{{ t('Account') }}</MenuItem
            >
          </template>
        </AccountMenu>
      </div>
    </AppShellHeader>
    <AppShellSidebar class="no-print oc-glass-side border-e border-(--oc-glass-edge)">
      <BrandNav
        :current="section"
        :collapsed="isRail"
        :can-toggle="isDesktop"
        @toggle="collapsed.setValue((previous) => !previous)"
      />
    </AppShellSidebar>
    <AppShellMain class="min-w-0">
      <AppShellContent gutter="lg" :max-width="1400" class="pb-12 pt-6 xl:px-10 print:max-w-none print:p-0">
        <div
          v-if="brand.notice.value"
          role="status"
          class="no-print mb-6 flex items-start justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-4 text-sm text-success-soft-foreground"
        >
          <p class="leading-6">{{ brand.notice.value }}</p>
          <button class="shrink-0 rounded p-1" :aria-label="t('Close')" @click="brand.notice.value = null">
            <Icon :icon="X" :size="15" />
          </button>
        </div>
        <SsoConnection />
        <AppErrorBoundary>
          <RouterView v-slot="{ Component }">
            <component :is="Component" v-if="brand.draft.value || section === 'identity'" :key="route.fullPath" />
            <BrandQueryState v-else :state="brand.queryState.value" :kind="queryKind" />
          </RouterView>
          <template #fallback="{ reset }">
            <section class="panel p-10 text-center">
              <h1 class="font-editorial text-2xl">{{ t('Could not open this page') }}</h1>
              <button class="btn-primary mt-5" @click="reset">{{ t('Retry') }}</button>
            </section>
          </template>
        </AppErrorBoundary>
      </AppShellContent>
    </AppShellMain>
    <PhoneNav :current="section" />
  </AppShell>
</template>
