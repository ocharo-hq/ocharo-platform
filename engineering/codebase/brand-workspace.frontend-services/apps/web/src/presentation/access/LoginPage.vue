<script setup lang="ts">
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import UiLanguagePicker from '@/presentation/common/UiLanguagePicker.vue';
import BuildIdentity from '../common/BuildIdentity.vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import type { AccessControls } from '@/application/common/hooks/UseAccess';

const { t } = useUiLocale();

/** Sends operators to sign in at Ocharo's Identity instance; a failed return names itself. */
defineOptions({ name: 'LoginPage' });
defineProps<{ access: AccessControls }>();
const signInFailed = new URLSearchParams(window.location.search).has('signInError');
</script>

<template>
  <main class="mx-auto grid min-h-screen max-w-6xl items-center gap-12 px-6 py-12 md:grid-cols-2 md:px-12">
    <div>
      <div class="mb-10 flex items-center gap-3">
        <span class="brand-mark" aria-hidden="true">O</span><b>{{ t("Ocharo workspace") }}</b><BuildIdentity />
      </div>
      <p class="eyebrow">{{ t("Your creative business") }}</p>
      <h1 class="mt-4 text-4xl font-medium leading-tight tracking-tight sm:text-5xl">
        {{ t("Your collection.") }}<br />{{ t("Your next chapter.") }}
      </h1>
      <p class="mt-6 max-w-md text-lg leading-8 text-muted-foreground">
        {{ t("Design your catalogue, refine your business card, and bring your brand’s work together.") }}
      </p>
      <p class="mt-8 text-sm text-muted-foreground">{{ t("Prepare and export manually. Social publishing is not connected.") }}</p>
    </div>
    <section class="workspace-card p-7 sm:p-10" aria-labelledby="signin-title">
      <div class="mb-5"><UiLanguagePicker /></div>
      <h2 id="signin-title" class="text-2xl font-semibold">{{ t("Operator sign-in") }}</h2>
      <p class="mt-5 leading-7 text-muted-foreground">{{ t("Sign in with your phone; the code arrives in Telegram.") }}</p>
      <p v-if="signInFailed" role="alert" class="mt-4 text-sm text-destructive">{{ t("Sign-in was not completed. Try again.") }}</p>
      <Button class="mt-6" :is-disabled="access.busy.value" is-full-width @click="access.signIn()">{{ t("Sign in") }}</Button>
      <p class="mt-4 text-xs leading-5 text-muted-foreground">{{ t("New to Ocharo? Ask an owner or admin for an invite.") }}</p>
    </section>
  </main>
</template>
