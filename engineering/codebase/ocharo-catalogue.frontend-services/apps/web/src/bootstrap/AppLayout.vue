<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { ChevronRight, X } from 'lucide-vue-next';
import { TailwindBreakpoints, useBreakpoint } from '@wow-two-beta/fe/foundation/device';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { localStorageStorageBroker, usePersistentState } from '@wow-two-beta/fe/foundation/storage';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
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

/** The sidebar's widths: full, and the icon rail. @internal */
const SidebarWidth = { Full: '244px', Rail: '68px' } as const;
/** Where this browser remembers the desktop sidebar's rail choice. @internal */
const SidebarCollapsedKey = 'ocharo.catalogue.sidebar-collapsed';
/** The interface languages, each in its own language. @internal */
const InterfaceLanguages: readonly AccountMenuLocale[] = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
];

/**
 * The Ocharo frame, as Catalogue wears it, around every authoring place. The glass top bar is the suite's: the app
 * button with the app version, where you are, notifications and the account menu. The sidebar is Catalogue's sections; it
 * collapses to an icon rail on desktops and is always the rail on tablets. Phones swap it for a bottom bar. Only the
 * main region scrolls.
 */
defineOptions({ name: 'AppLayout' });
const { t, uiLocale, setUiLocale } = useUiLocale();
const route = useRoute();
const store = useCatalogueStore();
// The frame reads the catalogue settings once for every place, from the session-bound query cache.
useCatalogueSettings();
const { isAuthenticated, person } = useIdentitySession();
const { signingOut, signOut } = useSessionActions();
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
watch(accountOpen, (open) => {
  if (open) void refreshBackendVersion();
});
</script>

<template>
  <AppShell
    class="catalogue-frame surface-ambient transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none"
    sidebar-breakpoint="md"
    :sidebar-width="sidebarWidth"
  >
    <AppShellHeader height="auto" variant="glass" class="no-print p-0 pt-[env(safe-area-inset-top)]">
      <div class="flex min-h-15 w-full min-w-0 items-center gap-3 px-3 sm:px-4">
        <div class="flex shrink-0 items-center gap-2">
          <AppSwitcherMenu
            :items="items"
            current="catalogue"
            :label="t('Catalogue')"
            subtitle="Ocharo"
            :title="t('Ocharo apps')"
            :sites-label="t('Public sites')"
            :close-label="t('Close')"
            :new-tab-label="t('(opens in a new tab)')"
            :trigger-label="t('Ocharo apps — {app}')"
            variant="glass-menu"
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
        <NotificationsMenu />
        <AccountMenu
          v-model:open="accountOpen"
          v-bind="accountIdentity"
          :locales="InterfaceLanguages"
          :locale="uiLocale"
          :versions="versions"
          :is-busy="signingOut"
          variant="glass-menu"
          @update:locale="setUiLocale"
        />
      </div>
    </AppShellHeader>
    <AppShellSidebar variant="glass" class="no-print">
      <CatalogueNav
        :current="section"
        :collapsed="isRail"
        :can-toggle="isDesktop"
        @toggle="collapsed.setValue((previous) => !previous)"
      />
    </AppShellSidebar>
    <AppShellMain class="min-w-0">
      <AppShellContent gutter="lg" :max-width="1800" class="pb-12 pt-6 xl:px-10 print:max-w-none print:p-0">
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
      </AppShellContent>
    </AppShellMain>
    <PhoneNav :current="section" />
  </AppShell>
</template>
