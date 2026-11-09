<script setup lang="ts">
import { onScopeDispose } from 'vue';
import { RouterView, useRoute, useRouter } from 'vue-router';
import { AuthProvider } from '@wow-two-beta/fe/auth';
import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';

import { useAccess } from '@/application/common/hooks/UseAccess';
import { provideUiLocale } from '@/application/common/hooks/UseUiLocale';
import SessionGate from '@/presentation/access/SessionGate.vue';
import FeedbackHost from '@/presentation/common/FeedbackHost.vue';
import RouteFailure from '@/presentation/common/RouteFailure.vue';

import { installDeskGuards } from './router/DeskGuards';
import { ThemeStorageKey } from './theme';

/**
 * Composes the colour mode, the cookie session boundary and the routed operator frame. A first address whose page code
 * could not load leaves nothing matched; its failure offers a reload, which fetches the current build.
 */
defineOptions({ name: 'AppRoot' });
provideUiLocale();
const access = useAccess();
const route = useRoute();
onScopeDispose(installDeskGuards(useRouter(), access));
/** Reloads the document, fetching the current build's page code. @internal */
function reload(): void {
  window.location.reload();
}
</script>

<template>
  <ColorModeProvider :storage-key="ThemeStorageKey">
    <FeedbackHost />
    <AuthProvider :strategy="access.strategy" :bridge="access.bridge"
      ><SessionGate :access="access"
        ><RouterView v-if="route.matched.length" v-slot="{ Component }"><component :is="Component" :access="access" /></RouterView
        ><RouteFailure v-else @retry="reload" /></SessionGate
    ></AuthProvider>
  </ColorModeProvider>
</template>
