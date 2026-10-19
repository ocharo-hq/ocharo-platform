<script setup lang="ts">
import { computed } from 'vue';
import { AppSwitcherKind, AppSwitcherMenu, type AppSwitcherItem } from '@wow-two-beta/fe/presentation/nav';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { OcharoAppKind, ocharoApps, type OcharoApp } from '@/domain/shared/OcharoApps';

import { AppIcons } from './ShellIcons';

/**
 * The app button: the Ocharo mark, the current app and its organization, and a chevron, as one control. It opens the
 * Ocharo app menu — a panel under the button from tablets up, a full-screen dialog on phones.
 */
defineOptions({ name: 'OcharoAppsMenu' });
const props = defineProps<{ current: OcharoApp['key']; label: string; subtitle?: string | null }>();
const { t } = useUiLocale();
const apps = ocharoApps(import.meta.env as Record<string, string | undefined>);
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
</script>

<template>
  <AppSwitcherMenu
    :items="items"
    :current="props.current"
    :label="t(props.label)"
    v-bind="props.subtitle ? { subtitle: props.subtitle } : {}"
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
</template>
