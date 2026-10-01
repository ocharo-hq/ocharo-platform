<script setup lang="ts">
import { provideLocale } from '@wow-two-beta/ui-vue/foundation/i18n';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { AuthProvider } from '@wow-two-beta/ui-vue/auth';

import { useAccess } from '@/application/workspace/UseAccess';
import FoundationPage from '@/presentation/system/pages/FoundationPage.vue';
import FeedbackHost from '@/presentation/shared/FeedbackHost.vue';
import SessionGate from '@/presentation/workspace/SessionGate.vue';

/** Composes the cookie session boundary and the authenticated preparation workspace. */
defineOptions({ name: 'AppRoot' });
const { uiLocale } = useUiLocale();
provideLocale(uiLocale);
const access = useAccess();
</script>

<template>
  <FeedbackHost />
  <AuthProvider :strategy="access.strategy"
    ><SessionGate :access="access"
      ><template #diagnostics><FoundationPage /></template></SessionGate
  ></AuthProvider>
</template>
