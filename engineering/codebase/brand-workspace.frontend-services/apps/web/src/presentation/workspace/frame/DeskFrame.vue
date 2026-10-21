<script setup lang="ts">
import { computed, onMounted, useTemplateRef, watch } from 'vue';
import { useRoute } from 'vue-router';
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

import { useBuildIdentity } from '@/application/shared/UseBuildIdentity';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { DeskGroupLabels, DeskSections, deskSectionLabel } from '@/domain/workspace/DeskSections';
import type { WorkspaceLocation } from '@/domain/workspace/Models';

import DeskNav from './DeskNav.vue';
import OcharoAppsMenu from './OcharoAppsMenu.vue';
import PhoneNav from './PhoneNav.vue';

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
 * The Ocharo frame, as Desk wears it. The glass top bar is the suite's: the app button, where you are, and the account
 * menu. The sidebar is Desk's own sections; it collapses to an icon rail on desktops and is always the rail on tablets.
 * Phones swap it for a bottom bar. Only the main region scrolls.
 */
defineOptions({ name: 'DeskFrame' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; location: WorkspaceLocation }>();
const emit = defineEmits<{ settings: [] }>();
defineSlots<{ default(): unknown }>();
const { t, uiLocale, setUiLocale } = useUiLocale();
const { backendVersion, refresh: refreshBackendVersion } = useBuildIdentity();
const route = useRoute();
// The stored choice seeds the first render too, so a collapsed rail never animates in from the full sidebar.
const collapsed = usePersistentState(SidebarCollapsedKey, localStorageStorageBroker.read<boolean>(SidebarCollapsedKey) === true);
const breakpoint = useBreakpoint(TailwindBreakpoints);
const main = useTemplateRef<{ el: HTMLElement | null }>('main');

const organization = computed(() => props.workspace.organization.value ?? null);
const user = computed(() => props.access.session.value?.user ?? null);
/** The account menu names the person; the email stands in when no display name is set. */
const accountName = computed(() => user.value?.displayName || user.value?.email || '');
const accountEmail = computed(() => (user.value?.displayName ? user.value.email : ''));
const memberships = computed<AccountMenuOrganization[]>(() =>
  (props.workspace.organizations.value ?? []).map((org) => ({ id: org.id, label: org.name, description: t(org.role) })),
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
const sharedRefresh = computed(() => hasSections.value && ['media', 'brands', 'content', 'integrations'].includes(props.location.section));
const refreshDisabled = computed(
  () => props.access.busy.value || props.access.dirty.value || props.workspace.loading.value || props.workspace.refreshing.value,
);
const crumbs = computed(() => {
  const entry = DeskSections.find((section) => section.id === props.location.section);
  const group = entry ? DeskGroupLabels[entry.group] : null;
  return [...(group ? [t(group)] : []), t(deskSectionLabel(props.location.section))];
});

/** Switching organization replaces the document, so nothing private carries across. @internal */
function switchOrganization(id: string): void {
  void props.access.navigate(`/organizations/${id}`);
}

onMounted(() => void refreshBackendVersion());

/* A new section starts at the top of the scrolling region; a move inside a section keeps its place. */
watch(
  () => props.location.section,
  () => main.value?.el?.scrollTo?.({ top: 0 }),
);
watch(
  () => route.path,
  () => {
    if (!props.location.organizationId && props.location.section !== 'system') main.value?.el?.scrollTo?.({ top: 0 });
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
          v-if="hasSections || location.section === 'system'"
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
            <MenuItem @select="emit('settings')"><UserCog :size="15" aria-hidden="true" />{{ t('Account settings') }}</MenuItem>
          </template>
        </AccountMenu>
      </div>
    </AppShellHeader>
    <AppShellSidebar v-if="hasSections" variant="glass">
      <DeskNav
        :organization-id="organization!.id"
        :current="location.section"
        :collapsed="isRail"
        :can-toggle="isDesktop"
        @toggle="collapsed.setValue((previous) => !previous)"
      />
    </AppShellSidebar>
    <AppShellMain ref="main" class="min-w-0">
      <AppShellContent gutter="lg" max-width="2xl" class="pb-12 pt-6 lg:px-8 xl:px-10">
        <slot />
      </AppShellContent>
    </AppShellMain>
    <PhoneNav v-if="hasSections" :organization-id="organization!.id" :current="location.section" />
  </AppShell>
</template>
