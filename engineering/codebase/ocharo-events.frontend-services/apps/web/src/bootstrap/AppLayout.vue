<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { useRunningVersion } from '@wow-two-beta/fe/foundation/net';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { AppFrame, type AppFrameAccount, type AppFrameSwitcher } from '@wow-two-beta/fe/presentation/layout';
import {
  AppSwitcherKind,
  type AccountMenuLocale,
  type AccountMenuVersion,
  type AppSwitcherItem,
} from '@wow-two-beta/fe/presentation/nav';
import { AppErrorBoundary } from '@wow-two-beta/fe/router';

import { useBuildIdentity } from '../application/common/UseBuildIdentity';
import { useIdentitySession } from '../application/common/UseIdentitySession';
import { useOcharoApps } from '../application/shared/UseOcharoApps';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { OcharoAppKind } from '../domain/shared/OcharoApps';
import IdentityConnection from '../presentation/common/IdentityConnection.vue';
import { AppIcons } from '../presentation/frame/ShellIcons';

/** Where this browser remembers the desktop sidebar's rail choice; Events has no sidebar yet, so it stays unread. @internal */
const SidebarCollapsedKey = 'ocharo.events.sidebar-collapsed';
/** The interface languages, each named in its own language. @internal */
const UiLanguages: readonly AccountMenuLocale[] = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
];

/**
 * Events inside the frame every Ocharo app shares (the fe sdk's `AppFrame`): the bar and the page column are the
 * suite's, at one size in every app. Events passes what is its own: the app list, where you are, the account with its
 * languages, and its notices above the page. It has one place, so it gives the frame no sidebar.
 */
defineOptions({ name: 'AppLayout' });
const { t, uiLocale, setUiLocale } = useUiLocale();
const route = useRoute();
const { isAuthenticated, person, organization, signingOut, signOut } = useIdentitySession();
const { backendVersion, refresh: refreshBackendVersion, readRunningVersion } = useBuildIdentity();
const apps = useOcharoApps();
const appVersion = __APP_VERSION__;
// The badge beside the app button follows the release the API serves; a local build has no release to compare.
const { version: runningVersion } = useRunningVersion(() => (appVersion === 'dev' ? null : readRunningVersion()));

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
/** The place itself: its label comes from its route metadata. */
const crumbs = computed(() => {
  const place = typeof route.meta.label === 'string' ? route.meta.label : null;
  return place ? [t(place)] : [];
});
/** The switcher's second line: the session organization's name, or the suite's while none is named. */
const appSubtitle = computed(() => organization.value?.name?.trim() || 'Ocharo');

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
    { label: t('Events'), version: appVersion },
    { label: 'API', version: t(api), ...(commit ? { commit } : {}) },
  ];
});
/** The account menu: who is signed in, the versions, and sign-out while it runs. */
const account = computed<AppFrameAccount>(() => ({
  ...accountIdentity.value,
  locales: UiLanguages,
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
    :locale="uiLocale"
    :version="appVersion"
    :running-version="runningVersion"
    :apps="items"
    current-app="events"
    :app-label="t('Events')"
    :app-subtitle="appSubtitle"
    :app-switcher="switcher"
    :crumbs="crumbs"
    :trail-label="t('You are here')"
    :account="account"
    :collapse-storage-key="SidebarCollapsedKey"
    @update:locale="setUiLocale"
  >
    <template #brand><span class="oc-mark">O</span></template>
    <template #notice><IdentityConnection /></template>
    <AppErrorBoundary>
      <RouterView />
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
