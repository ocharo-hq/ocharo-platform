<script setup lang="ts">
import { onMounted, onScopeDispose, ref, shallowRef } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Field, PasswordInput } from '@wow-two-beta/fe/presentation/forms';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { notifyError, requestConfirmation, resolveConfirmation } from '@/application/shared/UseFeedback';
import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import type { TelegramAvailability, TelegramLink } from '@/domain/workspace/TelegramAccess';
defineOptions({ name: 'AccountSettings' });
const props = defineProps<{ access: AccessControls }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useUiLocale();
const status = shallowRef<TelegramAvailability | null>(null);
const link = shallowRef<TelegramLink | null>(null);
const password = ref('');
const loading = ref(false);
const saving = ref(false);
const confirming = ref(false);
const controller = new AbortController();
onScopeDispose(() => {
  controller.abort();
  if (confirming.value) resolveConfirmation(false);
});
function credentialFailure(failure: ApiFailure): string {
  return failure.problem?.code === 'invalid_credentials'
    ? 'Your current password was not accepted. Check it and try again.'
    : failureMessage(failure);
}
async function refresh(): Promise<void> {
  if (loading.value || saving.value || confirming.value) return;
  loading.value = true;
  try {
    const result = await props.access.api.getTelegram(controller.signal);
    if (controller.signal.aborted) return;
    if (result.ok) { status.value = result.value; if (result.value.linked) link.value = null; }
    else if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure), 'telegram-settings');
  } finally { loading.value = false; }
}
async function connect(): Promise<void> {
  if (props.access.busy.value || loading.value || saving.value || confirming.value || !password.value || !status.value?.enabled) return;
  saving.value = true;
  try {
    const result = await props.access.write(signal => props.access.api.linkTelegram(password.value, signal), controller.signal);
    password.value = '';
    if (controller.signal.aborted) return;
    if (result.ok) link.value = result.value;
    else if (result.failure.code !== 'cancelled') notifyError(credentialFailure(result.failure), 'telegram-link');
  } finally { saving.value = false; }
}
async function disconnect(): Promise<void> {
  if (props.access.busy.value || loading.value || saving.value || confirming.value || !password.value || !status.value?.linked) return;
  confirming.value = true;
  try {
    if (props.access.dirty.value && !await requestConfirmation(t('Discard unsaved changes and sign out?'), { confirmLabel: t('Discard changes') })) return;
    confirming.value = false;
    if (controller.signal.aborted || props.access.busy.value || !password.value || !status.value?.linked) return;
    saving.value = true;
    const result = await props.access.write(signal => props.access.api.unlinkTelegram(password.value, signal), controller.signal);
    password.value = '';
    if (controller.signal.aborted) return;
    if (result.ok) props.access.authenticated();
    else if (result.failure.code !== 'cancelled') notifyError(credentialFailure(result.failure), 'telegram-unlink');
  } finally { saving.value = false; confirming.value = false; }
}
function close(): void { if (!saving.value && !confirming.value) emit('close'); }
onMounted(refresh);
</script>
<template>
  <Modal :open="true" @update:open="value => { if (!value) close(); }">
    <ModalContent class="w-full max-w-lg rounded-2xl border border-border bg-card p-6 text-foreground">
      <ModalTitle class="text-xl font-semibold">{{ t('Account settings') }}</ModalTitle>
      <section class="mt-5 space-y-4" :aria-busy="loading || saving">
        <h3 class="font-semibold">{{ t('Telegram sign-in') }}</h3>
        <p v-if="loading && !status" role="status">{{ t('Loading…') }}</p>
        <p v-else-if="status && !status.enabled" class="text-sm text-muted-foreground">{{ t('Telegram sign-in has not been configured by your administrator.') }}</p>
        <template v-else-if="status?.enabled">
          <p class="text-sm text-muted-foreground">{{ t(status.linked ? 'Telegram is connected to your account.' : 'Connect Telegram once to receive sign-in codes without an email password.') }}</p>
          <p v-if="status.linked && status.phone" class="text-sm">{{ status.phone }}</p>
          <form class="space-y-4" @submit.prevent="status?.linked ? disconnect() : connect()">
            <Field :label="t('Current password')"><PasswordInput v-model="password" autocomplete="current-password" is-required :is-disabled="loading || saving || confirming || access.busy.value" /></Field>
            <p v-if="status.linked" class="text-sm text-muted-foreground">{{ t('Disconnecting Telegram signs you out of your existing sessions.') }}</p>
            <Button type="submit" :is-loading="saving" :is-disabled="loading || confirming || access.busy.value">{{ t(status.linked ? 'Disconnect Telegram' : 'Connect Telegram') }}</Button>
          </form>
          <div v-if="link" class="space-y-3">
            <p class="text-sm">{{ t('Open the bot, press Start, and share your own contact using its button.') }}</p>
            <a :href="link.botUrl" target="_blank" rel="noopener noreferrer" class="inline-flex rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground">{{ t('Open Telegram') }}</a>
            <p class="text-xs text-muted-foreground">{{ t('Return here and refresh to confirm the connection.') }}</p>
          </div>
        </template>
      </section>
      <footer class="mt-6 flex justify-end gap-2">
        <Button variant="outline" :is-loading="loading" :is-disabled="saving || confirming" @click="refresh">{{ t('Refresh') }}</Button>
        <Button variant="ghost" :is-disabled="saving || confirming" @click="close">{{ t('Close') }}</Button>
      </footer>
    </ModalContent>
  </Modal>
</template>
