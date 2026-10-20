<script setup lang="ts">
import { computed, onScopeDispose, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import { provideLocale } from '@wow-two-beta/fe/foundation/i18n';
import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { RouteAnnouncer } from '@wow-two-beta/fe/router';

import { feedbackBus } from '../application/common/UseFeedback';
import { useUiLocale } from '../application/shared/UseUiLocale';
import FeedbackHost from '../presentation/common/FeedbackHost.vue';
import { DocumentTitleSuffix } from './CatalogueRouter';
import { ThemeStorageKey } from './theme';

/**
 * Composes the colour mode, the SDK copy in the interface language, app feedback and the routed layouts: the operator
 * frame around every authoring place, and the published reader's own header.
 */
defineOptions({ name: 'AppRoot' });
const route = useRoute();
const { t, uiLocale } = useUiLocale();
provideLocale(
  uiLocale,
  computed(() => ({
    'AppShell.skipToContent': t('Skip to content'),
    'AccountMenu.menuLabel': t('Account'),
    'AccountMenu.triggerLabel': t('Account menu for {name}'),
    'AccountMenu.anonymousTriggerLabel': t('Account menu'),
    'AccountMenu.appearanceLabel': t('Appearance'),
    'AccountMenu.systemModeLabel': t('System'),
    'AccountMenu.lightModeLabel': t('Light'),
    'AccountMenu.darkModeLabel': t('Dark'),
    'AccountMenu.languageLabel': t('Interface language'),
    'AccountMenu.organizationLabel': t('Organization'),
    'AccountMenu.versionsLabel': t('Versions'),
    'AccountMenu.signOutLabel': t('Sign out'),
    'OverlayCloseButton.close': t('Close'),
    'SelectPickerContent.searchPlaceholder': t('Search…'),
    'SelectPickerContent.noResultsLabel': t('No results'),
    'Toast.closeLabel': t('Close'),
    'Toast.deleteLabel': t('Delete notification'),
    'NotificationItem.deleteNotification': t('Delete notification'),
    'NotificationCenterGroup.notifications': t('Notifications'),
    'NotificationCenterGroup.closeNotifications': t('Close notifications'),
    'Alert.closeLabel': t('Close'),
    'Banner.closeLabel': t('Close'),
    'Tag.closeLabel': t('Remove'),
    'Spinner.label': t('Loading…'),
  })),
);
// The router names the document on every navigation; a language change renames the open place.
watch(uiLocale, () => {
  const label = route.meta.label;
  document.title = typeof label === 'string' ? `${t(label)} · ${DocumentTitleSuffix}` : DocumentTitleSuffix;
});
onScopeDispose(() => feedbackBus.reset());
</script>

<template>
  <ColorModeProvider :storage-key="ThemeStorageKey">
    <FeedbackHost />
    <RouterView />
    <RouteAnnouncer />
  </ColorModeProvider>
</template>
