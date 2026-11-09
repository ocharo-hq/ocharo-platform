<script setup lang="ts">
import { computed, onBeforeUnmount, onScopeDispose, ref, watch } from 'vue';
import { RouterView, useRoute, useRouter } from 'vue-router';
import { AuthProvider, AuthStatus } from '@wow-two-beta/fe/auth';
import { provideLocale } from '@wow-two-beta/fe/foundation/i18n';
import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { SplashScreen } from '@wow-two-beta/fe/presentation/feedback';
import { RouteAnnouncer } from '@wow-two-beta/fe/router';

import { feedbackBus } from '../application/common/UseFeedback';
import { useUiLocale } from '../application/shared/UseUiLocale';
import { identityBridge, identityStrategy } from '../integration/common/IdentitySession';
import FeedbackHost from '../presentation/common/FeedbackHost.vue';
import { BootStageCount, SplashLogo } from './BootSplash';
import { DocumentTitleSuffix } from './CatalogueRouter';
import { ThemeStorageKey } from './theme';

/**
 * Composes the colour mode, the SDK copy in the interface language, the Ocharo session, app feedback and the routed
 * layouts — the operator frame around every authoring place, and the published reader's own header — under the
 * first-load splash that takes over from the static one in `index.html`.
 */
defineOptions({ name: 'AppRoot' });
const props = withDefaults(
  defineProps<{
    /** Whether the visit opened on the published reader, which reads no session until the operator frame is entered. */
    opensOnReader?: boolean;
  }>(),
  { opensOnReader: false },
);
const route = useRoute();
const router = useRouter();
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
    'MenuItem.newTabLabel': t('(opens in a new tab)'),
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
    'SplashScreen.label': t('Loading…'),
    'NumberInput.incrementLabel': t('Increase'),
    'NumberInput.decrementLabel': t('Decrease'),
    'SearchInput.clearSearch': t('Clear search'),
    'ColorPicker.colorPicker': t('Color picker'),
    'ColorPicker.hexColor': t('Hex color'),
    'ColorPicker.hue': t('Hue'),
    'ColorArea.saturation': t('Saturation'),
    'ColorArea.value': t('Brightness'),
  })),
);
// The router names the document on every navigation; a language change renames the open place.
watch(uiLocale, () => {
  const label = route.meta.label;
  document.title = typeof label === 'string' ? `${t(label)} · ${DocumentTitleSuffix}` : DocumentTitleSuffix;
});
onScopeDispose(() => feedbackBus.reset());

// Boot stages: the shell is mounting (the static splash's third), the session settled, the first place resolved. The
// splash lifts once both later stages land and never returns; while sign-in redirects, the place never resolves. The
// published reader reads no session, so its visit waits for the place alone.
const sessionSettled = ref(props.opensOnReader);
const stopSession = identityBridge.subscribeSession(({ status }) => {
  if (status === AuthStatus.Authenticated || status === AuthStatus.Anonymous) sessionSettled.value = true;
});
onBeforeUnmount(stopSession);
const routeReady = ref(false);
void router.isReady().then(() => {
  routeReady.value = true;
});
const bootStage = computed(() => 1 + Number(sessionSettled.value) + Number(routeReady.value));
const booting = computed(() => bootStage.value < BootStageCount);
</script>

<template>
  <ColorModeProvider :storage-key="ThemeStorageKey">
    <AuthProvider :strategy="identityStrategy" :bridge="identityBridge" :can-resolve-on-mount="!props.opensOnReader">
      <FeedbackHost />
      <RouterView />
      <RouteAnnouncer />
      <SplashScreen is-overlay :is-open="booting" :value="bootStage" :max="BootStageCount">
        <template #logo>
          <img
            :src="SplashLogo.ink"
            alt="Ocharo"
            :width="SplashLogo.width"
            :height="SplashLogo.height"
            class="dark:hidden"
          />
          <img
            :src="SplashLogo.reverse"
            alt="Ocharo"
            :width="SplashLogo.width"
            :height="SplashLogo.height"
            class="hidden dark:block"
          />
        </template>
      </SplashScreen>
    </AuthProvider>
  </ColorModeProvider>
</template>
