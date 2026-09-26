<script setup lang="ts">
import { AuthProvider } from '@wow-two-beta/ui-vue/auth';

import { useAccess } from '@/application/workspace/UseAccess';
import FoundationPage from '@/presentation/system/pages/FoundationPage.vue';
import SessionGate from '@/presentation/workspace/SessionGate.vue';

/** Composes the cookie session boundary and the authenticated preparation workspace. */
defineOptions({ name: 'AppRoot' });
const access = useAccess();
</script>

<template>
  <AuthProvider :strategy="access.strategy"
    ><SessionGate :access="access"
      ><template #diagnostics><FoundationPage /></template></SessionGate
  ></AuthProvider>
</template>
