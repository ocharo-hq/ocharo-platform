<script setup lang="ts">
import { onMounted, onScopeDispose, ref } from 'vue';
import TelegramSignIn from './TelegramSignIn.vue';
import { notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import UiLanguagePicker from '@/presentation/shared/UiLanguagePicker.vue';
import BuildIdentity from '../shared/BuildIdentity.vue';
import { useAppForm } from '@wow-two-beta/ui-vue/forms-engine/house';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Field, EmailInput, PasswordInput } from '@wow-two-beta/ui-vue/presentation/forms';

import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';

const { t } = useUiLocale();

/** Presents the explicitly provisioned operator sign-in. */
defineOptions({ name: 'LoginPage' });
const props = defineProps<{ access: AccessControls }>();
const telegramEnabled = ref(false);
const method = ref<'password' | 'telegram'>('password');
const passwordStarted = ref(false);
const availabilityController = new AbortController();
onScopeDispose(() => availabilityController.abort());
onMounted(async () => {
  const result = await props.access.api.getTelegram(availabilityController.signal);
  if (!availabilityController.signal.aborted && result.ok) {
    telegramEnabled.value = result.value.enabled;
    if (result.value.enabled && !passwordStarted.value && !form.state.isDirty && !form.state.isSubmitting) method.value = 'telegram';
  }
});
const form = useAppForm(() => ({
  defaultValues: { email: '', password: '' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    const result = await props.access.login(values, signal);
    form.setValue('password', '');
    if (result.ok) props.access.authenticated(window.location.pathname + window.location.search);
    if (!result.ok && result.failure.code !== 'cancelled') notifyError(
      result.failure.status === 401 || result.failure.status === 400
        ? 'Sign-in was not completed. Check your credentials and retry; repeated failures may temporarily lock the account.'
        : failureMessage(result.failure), 'operator-sign-in');
    return result;
  },
}));
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
      <template v-if="access.session.value?.setupRequired">
        <p class="mt-5 leading-7 text-muted-foreground">
          {{ t("An administrator needs to provision the first operator before sign-in is available.") }}
        </p>
        <p class="mt-4 text-sm text-muted-foreground">{{ t("This workspace does not offer public registration.") }}</p>
      </template>
      <template v-else>
      <div v-if="telegramEnabled" class="my-5 flex gap-2" :aria-label="t('Sign-in method')">
        <Button variant="outline" :aria-pressed="method === 'telegram'" :is-disabled="access.busy.value" @click="method = 'telegram'">Telegram</Button>
        <Button variant="outline" :aria-pressed="method === 'password'" :is-disabled="access.busy.value" @click="method = 'password'">{{ t('Email and password') }}</Button>
      </div>
      <TelegramSignIn v-if="telegramEnabled && method === 'telegram'" :access="access" />
      <form v-else class="mt-6 space-y-5" @focusin="passwordStarted = true" @input="passwordStarted = true" @submit.prevent="passwordStarted = true; form.handleSubmit($event)">
        <form.Field v-slot="f" name="email" is-required
          ><Field :label="t('Email')"
            ><EmailInput v-model="f.value" autocomplete="username" required @blur="f.onBlur" /></Field
        ></form.Field>
        <form.Field v-slot="f" name="password" is-required
          ><Field :label="t('Password')"
            ><PasswordInput v-model="f.value" autocomplete="current-password" required @blur="f.onBlur" /></Field
        ></form.Field>
        <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value" is-full-width
          >{{ t("Sign in") }}</Button
        >
        <p class="text-xs leading-5 text-muted-foreground">
          {{ t("Use the account provisioned by your administrator.") }}
        </p>
      </form>
      </template>
    </section>
  </main>
</template>
