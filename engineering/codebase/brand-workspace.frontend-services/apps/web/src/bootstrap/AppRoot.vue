<script setup lang="ts">
import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { provideUiLocale } from '@/application/shared/UseUiLocale';
import { AuthProvider } from '@wow-two-beta/fe/auth';

import { useAccess } from '@/application/workspace/UseAccess';
import FoundationPage from '@/presentation/system/pages/FoundationPage.vue';
import FeedbackHost from '@/presentation/shared/FeedbackHost.vue';
import SessionGate from '@/presentation/workspace/SessionGate.vue';

import { ThemeStorageKey } from './theme';

/** Composes the colour mode, the cookie session boundary and the authenticated preparation workspace. */
defineOptions({ name: 'AppRoot' });
provideUiLocale();
const access = useAccess();
</script>

<template>
  <ColorModeProvider :storage-key="ThemeStorageKey">
    <FeedbackHost />
    <AuthProvider :strategy="access.strategy" :bridge="access.bridge"
      ><SessionGate :access="access"
        ><template #diagnostics><FoundationPage /></template></SessionGate
    ></AuthProvider>
  </ColorModeProvider>
</template>
