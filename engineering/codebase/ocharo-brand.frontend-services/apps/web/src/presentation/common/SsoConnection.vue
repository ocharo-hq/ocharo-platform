<script setup lang="ts">
import { computed, ref } from 'vue';
import { useSsoSession } from '../../application/common/UseSsoSession';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** Offers to reconnect an expired Ocharo session in another tab, so unsaved edits here stay. */
defineOptions({ name: 'SsoConnection' });
const { t } = useUiLocale();
const { session, reconnect } = useSsoSession();
const busy = ref(false);
const loginUrl = computed(() => '/api/sso/login?returnUrl=' + encodeURIComponent(window.location.pathname));
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
    v-if="session.enabled && !session.authenticated"
    role="status"
    class="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4 text-sm"
  >
    <span>{{ t('Reconnect to Ocharo. Your unsaved changes stay in this tab.') }}</span>
    <a :href="loginUrl" target="_blank" rel="noopener" class="btn-outline">{{ t('Sign in to Ocharo') }}</a>
    <button class="btn-outline" :disabled="busy" :aria-busy="busy" @click="check">{{ t('Check connection') }}</button>
  </div>
  <p v-else-if="session.enabled && !session.canEdit" class="mb-4 text-sm text-muted-foreground">
    {{ t('You have read-only access.') }}
  </p>
</template>
