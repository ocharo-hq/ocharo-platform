<script setup lang="ts">
import { computed, ref } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { useIdentitySession, useSessionActions } from '../../application/common/UseIdentitySession';

/** Offers sign-in when the Ocharo session is missing or ended, in another tab so unsaved edits here stay. */
defineOptions({ name: 'SessionConnection' });
const { t } = useUiLocale();
const { isAuthenticated, canEdit, signInHref, signInFailed } = useIdentitySession();
const { reconnect } = useSessionActions();
const busy = ref(false);
/** Sign-in opens in another tab and returns to this page there. */
const signInUrl = computed(() => (isAuthenticated.value ? '' : signInHref(window.location.pathname)));
const failed = signInFailed();
async function check() {
  busy.value = true;
  try {
    await reconnect();
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <div
    v-if="!isAuthenticated"
    role="status"
    class="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4 text-sm"
  >
    <span>{{
      failed
        ? t('Sign-in did not complete. Try again.')
        : t('Reconnect to Ocharo. Your unsaved changes stay in this tab.')
    }}</span>
    <Button as-child variant="outline" tone="neutral">
      <a :href="signInUrl" target="_blank" rel="noopener">{{ t('Sign in to Ocharo') }}</a>
    </Button>
    <Button variant="outline" tone="neutral" :is-loading="busy" @click="check">{{ t('Check connection') }}</Button>
  </div>
  <p v-else-if="!canEdit" class="mb-4 text-sm text-muted-foreground">
    {{ t('You have read-only access.') }}
  </p>
</template>
