<script setup lang="ts">
import ContentSkeleton from '@/presentation/common/ContentSkeleton.vue';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import { useAuth } from '@wow-two-beta/fe/auth';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import type { AccessControls } from '@/application/common/hooks/UseAccess';
import type { Session } from '@/domain/common/models/Workspace';

import LoginPage from './LoginPage.vue';

const { t } = useUiLocale();

/**
 * Keeps private views behind a resolved, revalidated server session: an unsettled session shows a skeleton, a signed-out
 * browser signs in here at the address it asked for, and a recheck keeps the private view mounted with its edits.
 */
defineOptions({ name: 'SessionGate' });
defineProps<{ access: AccessControls }>();
defineSlots<{ default(): unknown }>();
const auth = useAuth<Session>();
</script>

<template>
  <main v-if="auth.isPending || access.invalidated.value" class="mx-auto max-w-xl px-6 py-24" aria-busy="true">
    <ContentSkeleton :label="t('Checking your session…')" :cards="1" />
  </main>
  <main v-else-if="!auth.isAuthenticated && access.sessionError.value" class="mx-auto max-w-xl px-6 py-24">
    <h1 class="text-2xl font-semibold">{{ t("Session unavailable") }}</h1>
    <Button class="mt-6" @click="auth.refresh">{{ t("Retry session") }}</Button>
  </main>
  <LoginPage v-else-if="!auth.isAuthenticated" :access="access" />
  <template v-else>
    <section v-if="access.checking.value || access.sessionError.value" class="mx-auto max-w-xl px-6 py-24">
      <ContentSkeleton v-if="access.checking.value" :label="t('Rechecking your session…')" :cards="1" />
      <template v-else
        ><h1 class="text-2xl font-semibold">{{ t("Verify your session to continue") }}</h1>
        <p class="mt-3 text-sm text-muted-foreground">
          {{ t("This page keeps your unsaved edits while the session is checked.") }}
        </p>
        <Button class="mt-6" @click="access.recheck">{{ t("Retry session") }}</Button></template
      >
    </section>
    <div v-show="!access.checking.value && !access.sessionError.value">
      <slot />
    </div>
  </template>
</template>
