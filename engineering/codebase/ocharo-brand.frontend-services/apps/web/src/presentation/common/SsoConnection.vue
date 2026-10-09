<script setup lang="ts">
import { computed, ref } from 'vue';
import { useSsoSession } from '../../application/common/UseSsoSession';
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
    <span>Ocharo hisobiga qayta ulaning. Saqlanmagan o‘zgarishlar shu oynada qoladi.</span>
    <a :href="loginUrl" target="_blank" rel="noopener" class="btn-outline">Ocharo hisobiga kirish</a>
    <button class="btn-outline" :disabled="busy" :aria-busy="busy" @click="check">Ulanishni tekshirish</button>
  </div>
  <p v-else-if="session.enabled && !session.canEdit" class="mb-4 text-sm text-muted-foreground">
    Sizda faqat ko‘rish ruxsati bor.
  </p>
</template>
