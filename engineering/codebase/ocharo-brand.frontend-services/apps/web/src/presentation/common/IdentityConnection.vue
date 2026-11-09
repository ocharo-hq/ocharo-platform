<script setup lang="ts">
import { computed, ref } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { useIdentitySession } from '../../application/common/UseIdentitySession';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** Offers sign-in when the Ocharo session is missing or ended, in another tab so unsaved edits here stay. */
defineOptions({ name: 'IdentityConnection' });
const { t } = useUiLocale();
const { isAuthenticated, organizationDenied, canEdit, reconnect, signInHref, signInFailed } = useIdentitySession();
const busy = ref(false);
const failed = signInFailed();
const signInUrl = computed(() => (isAuthenticated.value ? '' : signInHref()));
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
    <span v-if="failed" role="alert" class="text-destructive">{{ t('Sign-in was not completed. Try again.') }}</span>
    <span v-else>{{ t('Reconnect to Ocharo. Your unsaved changes stay in this tab.') }}</span>
    <Button as-child variant="outline">
      <a :href="signInUrl" target="_blank" rel="noopener">{{ t('Sign in to Ocharo') }}</a>
    </Button>
    <Button variant="outline" :is-loading="busy" @click="check">{{ t('Check connection') }}</Button>
  </div>
  <p v-else-if="organizationDenied" role="alert" class="mb-4 text-sm text-destructive">
    {{ t('This Ocharo account has no access to Brand. Sign out and sign in with an Ocharo account.') }}
  </p>
  <p v-else-if="!canEdit" class="mb-4 text-sm text-muted-foreground">
    {{ t('You have read-only access.') }}
  </p>
</template>
