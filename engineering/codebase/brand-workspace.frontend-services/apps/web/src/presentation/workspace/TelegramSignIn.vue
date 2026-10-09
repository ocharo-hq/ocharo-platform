<script setup lang="ts">
import { onScopeDispose, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Field, TextInput } from '@wow-two-beta/ui-vue/presentation/forms';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { notifyError } from '@/application/shared/UseFeedback';
defineOptions({ name: 'TelegramSignIn' });
const props = defineProps<{ access: AccessControls }>();
const { t } = useUiLocale();
const controller = new AbortController();
onScopeDispose(() => controller.abort());
const phone = ref('');
const code = ref('');
const sentTo = ref<string | null>(null);
const sending = ref(false);
const signingIn = ref(false);
watch(phone, () => { sentTo.value = null; code.value = ''; });
async function send(): Promise<void> {
  if (props.access.busy.value || sending.value || signingIn.value) return;
  sending.value = true;
  try {
    const value = phone.value.trim();
    const result = await props.access.write(signal => props.access.api.requestTelegramCode(value, signal), controller.signal);
    if (controller.signal.aborted) return;
    if (result.ok) { sentTo.value = value; code.value = ''; }
    else if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure), 'telegram-code');
  } finally { sending.value = false; }
}
async function login(): Promise<void> {
  if (!sentTo.value || props.access.busy.value || sending.value || signingIn.value) return;
  signingIn.value = true;
  try {
    const result = await props.access.write(signal => props.access.api.loginTelegram(sentTo.value!, code.value, signal), controller.signal);
    if (controller.signal.aborted) return;
    code.value = '';
    if (result.ok) props.access.authenticated(window.location.pathname + window.location.search);
    else if (result.failure.code !== 'cancelled') notifyError(
      result.failure.status === 401 ? 'The code is invalid or expired. Request a new code and try again.' : failureMessage(result.failure), 'telegram-sign-in');
  } finally { signingIn.value = false; }
}
</script>
<template>
  <div class="space-y-5">
    <p class="text-sm text-muted-foreground">{{ t('Use the phone number linked to Telegram in your Ocharo account settings.') }}</p>
    <form class="space-y-4" @submit.prevent="send">
      <Field :label="t('Phone number')"><TextInput v-model="phone" type="tel" autocomplete="tel" placeholder="+998…" required :disabled="access.busy.value" /></Field>
      <Button type="submit" variant="outline" :is-loading="sending" :is-disabled="access.busy.value" is-full-width>{{ t(sentTo ? 'Send a new code' : 'Send Telegram code') }}</Button>
    </form>
    <form v-if="sentTo" class="space-y-4" @submit.prevent="login">
      <p role="status" class="text-sm text-muted-foreground">{{ t('If this number is connected to an active Ocharo account, a code will arrive in Telegram.') }}</p>
      <Field :label="t('Sign-in code')"><TextInput v-model="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" required :disabled="access.busy.value" /></Field>
      <Button type="submit" :is-loading="signingIn" :is-disabled="access.busy.value" is-full-width>{{ t('Sign in') }}</Button>
    </form>
  </div>
</template>
