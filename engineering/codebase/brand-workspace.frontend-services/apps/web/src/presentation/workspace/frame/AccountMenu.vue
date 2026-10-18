<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { Check, ChevronDown, LogOut, UserCog } from 'lucide-vue-next';
import { useColorMode } from '@wow-two-beta/fe/foundation/primitives';
import { Avatar } from '@wow-two-beta/fe/presentation/display';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
  MenuItem,
  MenuLabel,
  MenuRadioGroup,
  MenuRadioItem,
  MenuSeparator,
} from '@wow-two-beta/fe/presentation/nav';

import { useBuildIdentity } from '@/application/shared/UseBuildIdentity';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

/**
 * The avatar at the end of the top bar. It holds everything about the person and the device rather than the work:
 * organization, account settings, appearance, interface language, the build, and sign-out.
 */
defineOptions({ name: 'AccountMenu' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const emit = defineEmits<{ settings: [] }>();
const { t, uiLocale, setUiLocale } = useUiLocale();
const colorMode = useColorMode();
const { backendVersion, refresh } = useBuildIdentity();
const appVersion = __APP_VERSION__;
const languages = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'ru', label: 'Русский' },
  { value: 'en', label: 'English' },
] as const;
const appearances = [
  { value: 'system', label: 'Match device' },
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
] as const;
const user = computed(() => props.access.session.value?.user ?? null);
const displayName = computed(() => user.value?.displayName || user.value?.email || t('Account'));
const organizations = computed(() => props.workspace.organizations.value ?? []);
const current = computed(() => props.workspace.organization.value ?? null);
const appearance = computed(() => (colorMode.followsSystem ? 'system' : colorMode.mode));

/** Applies an appearance row; the SDK provider remembers it for the next visit. @internal */
function chooseAppearance(value: string | null): void {
  if (value === 'system') colorMode.followSystem();
  else if (value === 'light' || value === 'dark') colorMode.setMode(value);
}
/** Switching organization replaces the document, so nothing private carries across. @internal */
function chooseOrganization(id: string | null): void {
  if (id && id !== current.value?.id) void props.access.navigate(`/organizations/${id}`);
}
onMounted(() => void refresh());
</script>

<template>
  <DropdownMenu placement="bottom-end">
    <DropdownMenuTrigger as-child>
      <button
        type="button"
        class="flex min-h-11 shrink-0 items-center gap-1.5 rounded-xl border border-border bg-card/60 py-1 ps-1 pe-2 transition-colors hover:border-input focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :aria-label="t('Account menu for {name}', { name: displayName })"
        :disabled="access.busy.value"
      >
        <Avatar :name="displayName" size="sm" />
        <ChevronDown :size="14" class="text-muted-foreground" aria-hidden="true" />
      </button>
    </DropdownMenuTrigger>
    <DropdownMenuContent :aria-label="t('Account')" class="oc-glass-menu max-h-[calc(100dvh-5rem)] w-72 overflow-y-auto border-(--oc-glass-edge) shadow-(--oc-menu-shadow)">
      <MenuLabel>{{ displayName }}</MenuLabel>
      <p v-if="user?.email" class="truncate px-2 pb-2 text-xs text-muted-foreground">{{ user.email }}</p>
      <template v-if="organizations.length > 1">
        <MenuSeparator />
        <!-- Many memberships scroll inside their own group, so appearance and sign-out stay in reach. -->
        <MenuRadioGroup :label="t('Organization')" class="max-h-44 overflow-y-auto" :model-value="current?.id ?? null" @update:model-value="chooseOrganization">
          <MenuRadioItem v-for="org in organizations" :key="org.id" :value="org.id">
            <template #indicator="{ checked }"><Check v-if="checked" :size="14" /></template>
            {{ org.name }}
          </MenuRadioItem>
        </MenuRadioGroup>
      </template>
      <p v-else-if="current" class="px-2 pb-2 text-xs text-muted-foreground">{{ current.name }} · {{ t(current.role) }}</p>
      <MenuSeparator />
      <MenuItem @select="emit('settings')"><UserCog :size="15" />{{ t('Account settings') }}</MenuItem>
      <MenuSeparator />
      <MenuRadioGroup :label="t('Appearance')" :model-value="appearance" @update:model-value="chooseAppearance">
        <MenuRadioItem v-for="item in appearances" :key="item.value" :value="item.value">
          <template #indicator="{ checked }"><Check v-if="checked" :size="14" /></template>
          {{ t(item.label) }}
        </MenuRadioItem>
      </MenuRadioGroup>
      <MenuSeparator />
      <MenuRadioGroup :label="t('Interface language')" :model-value="uiLocale" @update:model-value="(value: string | null) => value && setUiLocale(value)">
        <MenuRadioItem v-for="language in languages" :key="language.value" :value="language.value">
          <template #indicator="{ checked }"><Check v-if="checked" :size="14" /></template>
          {{ language.label }}
        </MenuRadioItem>
      </MenuRadioGroup>
      <MenuSeparator />
      <p class="px-2 py-1.5 font-mono text-[11px] text-muted-foreground">
        {{ t('Desk {app} · API {api}', { app: appVersion, api: t(backendVersion) }) }}
      </p>
      <MenuSeparator />
      <MenuItem state="destructive" @select="access.logout"><LogOut :size="15" />{{ t('Sign out') }}</MenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
