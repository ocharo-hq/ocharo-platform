<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { RefreshCw, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { useRunningVersion } from '@wow-two-beta/fe/foundation/net';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { AppFrame, type AppFrameAccount, type AppFrameSwitcher } from '@wow-two-beta/fe/presentation/layout';
import { AppSwitcherKind, type AccountMenuVersion, type AppSwitcherItem } from '@wow-two-beta/fe/presentation/nav';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { useBrand } from '../application/brand';
import { useBuildIdentity } from '../application/common/UseBuildIdentity';
import { useIdentitySession } from '../application/common/UseIdentitySession';
import { useOcharoApps } from '../application/shared/UseOcharoApps';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { BrandGroupLabels, brandSection, readBrandSection } from '../domain/shared/BrandSections';
import { OcharoAppKind } from '../domain/shared/OcharoApps';
import BrandQueryState from '../presentation/common/BrandQueryState.vue';
import IdentityConnection from '../presentation/common/IdentityConnection.vue';
import BrandNav from '../presentation/frame/BrandNav.vue';
import PhoneNav from '../presentation/frame/PhoneNav.vue';
import { AppIcons } from '../presentation/frame/ShellIcons';

/** Where this browser remembers the desktop sidebar's rail choice; the key predates the shared frame. @internal */
const SidebarCollapsedKey = 'ocharo.brand.sidebar-collapsed';

/**
 * Brand inside the frame every Ocharo app shares (the fe sdk's `AppFrame`): the bar, the sidebar and the page column
 * are the suite's, at one size in every app. Brand passes what is its own: the app list, its sections, where you are,
 * the release this draft stands on with a refresh, the account, and its notices above the page.
 */
defineOptions({ name: 'AppLayout' });
const { t } = useUiLocale();
const route = useRoute();
const brand = useBrand();
const { isAuthenticated, person, signingOut, signOut } = useIdentitySession();
const { backendVersion, refresh: refreshBackendVersion, readRunningVersion } = useBuildIdentity();
const apps = useOcharoApps();
const appVersion = __APP_VERSION__;
// The badge beside the app button follows the release the API serves; a local build has no release to compare.
const { version: runningVersion } = useRunningVersion(() => (appVersion === 'dev' ? null : readRunningVersion()));

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
/** The app menu's own phrases. */
const switcher = computed<AppFrameSwitcher>(() => ({
  title: t('Ocharo apps'),
  sitesLabel: t('Public sites'),
  closeLabel: t('Close'),
  newTabLabel: t('(opens in a new tab)'),
  triggerLabel: t('Ocharo apps — {app}'),
}));
/** The group, the section and the place itself, each once: the place's label comes from its route metadata. */
const crumbs = computed(() => {
  const entry = brandSection(section.value);
  const group = entry ? BrandGroupLabels[entry.group] : null;
  const place = typeof route.meta.label === 'string' ? route.meta.label : null;
  const labels = [group, entry?.label ?? null, place].filter((label): label is string => Boolean(label));
  return labels.filter((label, index) => labels.indexOf(label) === index).map((label) => t(label));
});
/** Whether the open place waits for the draft: Identity keeps its own editor mounted, and a not-found place needs none. */
const needsDraft = computed(() => section.value !== null && section.value !== 'identity');
/** The first-read placeholder each place shows until the draft arrives. */
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

const accountOpen = ref(false);
/** Who is signed in, from Identity: the name, then the email or, for a phone sign-in, the number; sign-out with it. A
 *  person Identity names by neither still says where the session came from. */
const accountIdentity = computed(() => {
  const signedIn = person.value;
  if (!isAuthenticated.value || !signedIn) return {};
  const name = signedIn.name?.trim() || null;
  const avatarSrc = signedIn.picture;
  const contact = signedIn.email?.trim() || signedIn.phoneNumber?.trim() || null;
  if (!name && !contact) return { description: t('Signed in to Ocharo'), avatarSrc, onSignOut: signOut };
  return {
    ...(name ? { name } : {}),
    // A person without a display name is named by their contact; it is not repeated under itself.
    ...(contact && contact !== name ? { description: contact } : {}),
    avatarSrc,
    onSignOut: signOut,
  };
});
/** The app's own version, then the API's from its status, with the commit the build carries split off. */
const versions = computed<AccountMenuVersion[]>(() => {
  const [api = '', commit] = backendVersion.value.split('+');
  return [
    { label: t('Brand'), version: appVersion },
    { label: 'API', version: t(api), ...(commit ? { commit } : {}) },
  ];
});
/** The account menu: who is signed in, the versions, and sign-out while it runs. */
const account = computed<AppFrameAccount>(() => ({
  ...accountIdentity.value,
  versions: versions.value,
  isBusy: signingOut.value,
}));
watch(accountOpen, (open) => {
  if (open) void refreshBackendVersion();
});
</script>

<template>
  <AppFrame
    v-model:account-open="accountOpen"
    :version="appVersion"
    :running-version="runningVersion"
    :apps="items"
    current-app="brand"
    :app-label="t('Brand')"
    app-subtitle="Ocharo"
    :app-switcher="switcher"
    :crumbs="crumbs"
    :trail-label="t('You are here')"
    :account="account"
    :collapse-storage-key="SidebarCollapsedKey"
  >
    <template #brand><span class="oc-mark">O</span></template>
    <template #nav="{ isRail }"><BrandNav :current="section" :collapsed="isRail" /></template>
    <template #bar-end>
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
    </template>
    <template #phone-nav><PhoneNav :current="section" /></template>
    <template #notice>
      <div
        v-if="brand.notice.value"
        role="status"
        class="no-print mb-6 flex items-start justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-4 text-sm text-success-soft-foreground"
      >
        <p class="leading-6">{{ brand.notice.value }}</p>
        <Button
          variant="ghost"
          tone="success"
          shape="square"
          size="sm"
          class="shrink-0"
          :aria-label="t('Close')"
          @click="brand.notice.value = null"
        >
          <Icon :icon="X" :size="15" />
        </Button>
      </div>
      <IdentityConnection />
    </template>
    <AppErrorBoundary>
      <RouterView v-slot="{ Component }">
        <component :is="Component" v-if="brand.draft.value || !needsDraft" :key="route.fullPath" />
        <BrandQueryState v-else :state="brand.queryState.value" :kind="queryKind" />
      </RouterView>
      <template #fallback="{ reset }">
        <Card
          v-bind="{ role: 'region', 'aria-labelledby': 'page-error-heading' }"
          radius="2xl"
          padding="2xl"
          class="text-center"
        >
          <h1 id="page-error-heading" class="font-editorial text-2xl">{{ t('Could not open this page') }}</h1>
          <Button tone="primary" class="mt-5" @click="reset">{{ t('Retry') }}</Button>
        </Card>
      </template>
    </AppErrorBoundary>
  </AppFrame>
</template>
