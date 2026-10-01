<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { useAuth } from '@wow-two-beta/ui-vue/auth';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import type { AccessControls } from '@/application/workspace/UseAccess';
import type { Session } from '@/domain/workspace/Models';

import LoginPage from './LoginPage.vue';
import WorkspacePage from './WorkspacePage.vue';

const { t } = useUiLocale();

/** Keeps private views behind a resolved, revalidated server session. */
defineOptions({ name: 'SessionGate' });
defineProps<{ access: AccessControls }>();
defineSlots<{ diagnostics(): unknown }>();
const auth = useAuth<Session>();
</script>

<template>
  <main v-if="auth.isPending || access.invalidated.value" class="mx-auto max-w-xl px-6 py-24" aria-busy="true">
    <p role="status">{{ t("Checking your session…") }}</p>
  </main>
  <main v-else-if="!auth.isAuthenticated && access.sessionError.value" class="mx-auto max-w-xl px-6 py-24">
    <h1 class="text-2xl font-semibold">{{ t("Session unavailable") }}</h1>
    <p role="alert" class="mt-5 leading-7">{{ t(access.sessionError.value ?? '') }}</p>
    <Button class="mt-6" @click="auth.refresh">{{ t("Retry session") }}</Button>
  </main>
  <LoginPage v-else-if="!auth.isAuthenticated" :access="access" />
  <template v-else>
    <section v-if="access.checking.value || access.sessionError.value" class="mx-auto max-w-xl px-6 py-24">
      <p v-if="access.checking.value" role="status">{{ t("Rechecking your session…") }}</p>
      <template v-else
        ><h1 class="text-2xl font-semibold">{{ t("Verify your session to continue") }}</h1>
        <p role="alert" class="mt-5">{{ t(access.sessionError.value ?? '') }}</p>
        <p class="mt-3 text-sm text-muted-foreground">
          {{ t("This page keeps your unsaved edits while the session is checked.") }}
        </p>
        <Button class="mt-6" @click="access.recheck">{{ t("Retry session") }}</Button></template
      >
    </section>
    <div v-show="!access.checking.value && !access.sessionError.value">
      <WorkspacePage :access="access"
        ><template #diagnostics><slot name="diagnostics" /></template
      ></WorkspacePage>
    </div>
  </template>
</template>
