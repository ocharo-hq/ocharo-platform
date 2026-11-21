<script setup lang="ts">
import { RouterView } from 'vue-router';
import { AuthProvider } from '@wow-two-beta/fe/auth';
import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { RouteAnnouncer } from '@wow-two-beta/fe/router';
import { identityBridge, identityStrategy } from '../integration/trends/TrendsApi';
</script>

<template>
  <ColorModeProvider storage-key="ocharo.trends.color-mode">
    <AuthProvider :strategy="identityStrategy" :bridge="identityBridge">
      <RouterView />
      <RouteAnnouncer />
    </AuthProvider>
  </ColorModeProvider>
</template>
