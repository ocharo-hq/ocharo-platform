<script setup lang="ts">
import { computed, ref } from 'vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { hasSignInError, signInUrl, useIdentitySession } from '../../application/common/UseIdentitySession';
const { t } = useUiLocale();
const { session, canEdit, reconnect } = useIdentitySession();
const busy = ref(false);
/** Sign-in opens in another tab, so unsaved edits here stay; it returns to this page there. */
const loginUrl = computed(() => signInUrl(window.location.pathname));
const signInFailed = hasSignInError(window.location.search);
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
    v-if="!session.isAuthenticated"
    role="status"
    class="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4 text-sm"
  >
    <span>{{
      signInFailed
        ? t('Sign-in did not complete. Try again.')
        : t('Reconnect to Ocharo. Your unsaved changes stay in this tab.')
    }}</span>
    <a :href="loginUrl" target="_blank" rel="noopener" class="btn-outline">{{ t('Sign in to Ocharo') }}</a>
    <button class="btn-outline" :disabled="busy" :aria-busy="busy" @click="check">{{ t('Check connection') }}</button>
  </div>
  <p v-else-if="!canEdit" class="mb-4 text-sm text-muted-foreground">
    {{ t('You have read-only access.') }}
  </p>
</template>
