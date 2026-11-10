<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
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

import { useCatalogueSettings, useCatalogueStore } from '../application/catalogue';
import { useBuildIdentity } from '../application/common/UseBuildIdentity';
import { useIdentitySession, useSessionActions } from '../application/common/UseIdentitySession';
import { useOcharoApps } from '../application/shared/UseOcharoApps';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { CatalogueGroupLabels, catalogueSection, readCatalogueSection } from '../domain/shared/CatalogueSections';
import { OcharoAppKind } from '../domain/shared/OcharoApps';
import NotificationsMenu from '../presentation/common/NotificationsMenu.vue';
import SessionConnection from '../presentation/common/SessionConnection.vue';
import CatalogueNav from '../presentation/frame/CatalogueNav.vue';
import PhoneNav from '../presentation/frame/PhoneNav.vue';
import { AppIcons } from '../presentation/frame/ShellIcons';

/** Where this browser remembers the desktop sidebar's rail choice; the key predates the shared frame. @internal */
const SidebarCollapsedKey = 'ocharo.catalogue.sidebar-collapsed';
/** The interface languages, each in its own language. @internal */
const InterfaceLanguages: readonly AccountMenuLocale[] = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
];

/**
 * Catalogue inside the frame every Ocharo app shares (the fe sdk's `AppFrame`): the bar, the sidebar and the page column
 * are the suite's, at one size in every app. Catalogue passes what is its own: the app list, its sections, where you
 * are, notifications, the account with the interface languages, and its notices above the page.
 */
defineOptions({ name: 'AppLayout' });
const { t, uiLocale, setUiLocale } = useUiLocale();
const route = useRoute();
const store = useCatalogueStore();
// The frame reads the catalogue settings once for every place, from the session-bound query cache.
useCatalogueSettings();
const { isAuthenticated, person } = useIdentitySession();
const { signingOut, signOut } = useSessionActions();
const { backendVersion, refresh: refreshBackendVersion, readRunningVersion } = useBuildIdentity();
const apps = useOcharoApps();
const appVersion = __APP_VERSION__;
// The badge beside the app button follows the release the API serves; a local build has no release to compare.
const { version: runningVersion } = useRunningVersion(() => (appVersion === 'dev' ? null : readRunningVersion()));

const section = computed(() => readCatalogueSection(route.meta));
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
  const entry = catalogueSection(section.value);
  const group = entry ? CatalogueGroupLabels[entry.group] : null;
  const place = typeof route.meta.label === 'string' ? route.meta.label : null;
  const labels = [group, entry?.label ?? null, place].filter((label): label is string => Boolean(label));
  return labels.filter((label, index) => labels.indexOf(label) === index).map((label) => t(label));
});

/** Who is signed in, and sign-out, exist only with an Ocharo session. */
const accountOpen = ref(false);
/** The person Identity names and their contact; a session that names neither still says it is signed in. */
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
    { label: t('Catalogue'), version: appVersion },
    { label: 'API', version: t(api), ...(commit ? { commit } : {}) },
  ];
});
/** The account menu: who is signed in, the versions, the interface languages, and sign-out while it runs. */
const account = computed<AppFrameAccount>(() => ({
  ...accountIdentity.value,
  locales: InterfaceLanguages,
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
    current-app="catalogue"
    :app-label="t('Catalogue')"
    app-subtitle="Ocharo"
    :app-switcher="switcher"
    :crumbs="crumbs"
    :trail-label="t('You are here')"
    :account="account"
    :collapse-storage-key="SidebarCollapsedKey"
    :content-max-width="1800"
    @update:locale="setUiLocale"
  >
    <template #brand><span class="oc-mark">O</span></template>
    <template #nav="{ isRail }"><CatalogueNav :current="section" :collapsed="isRail" /></template>
    <template #bar-end><NotificationsMenu /></template>
    <template #phone-nav><PhoneNav :current="section" /></template>
    <template #notice>
      <div
        v-if="store.notice.value"
        role="status"
        class="no-print mb-5 flex justify-between gap-4 rounded-xl border border-success/20 bg-success-soft px-5 py-3 text-sm text-success-soft-foreground"
      >
        <p>{{ store.notice.value }}</p>
        <Button
          variant="ghost"
          tone="success"
          shape="square"
          size="sm"
          class="shrink-0"
          :aria-label="t('Close')"
          @click="store.notice.value = null"
        >
          <Icon :icon="X" :size="16" />
        </Button>
      </div>
      <SessionConnection />
    </template>
    <AppErrorBoundary>
      <RouterView v-slot="{ Component }"><component :is="Component" :key="route.fullPath" /></RouterView>
      <template #fallback="{ reset }">
        <Card
          v-bind="{ role: 'region', 'aria-labelledby': 'page-error-heading' }"
          radius="2xl"
          padding="2xl"
          class="text-center"
        >
          <h1 id="page-error-heading" class="font-editorial text-2xl">{{ t('Could not open this page') }}</h1>
          <Button class="mt-5" @click="reset">{{ t('Retry') }}</Button>
        </Card>
      </template>
    </AppErrorBoundary>
  </AppFrame>
</template>
