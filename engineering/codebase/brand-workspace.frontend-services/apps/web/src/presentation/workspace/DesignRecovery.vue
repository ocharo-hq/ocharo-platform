<script setup lang="ts">
import type { DesignRecoveryControls } from '@/application/workspace/UseDesignRecovery';

defineOptions({ name: 'DesignRecovery' });
defineProps<{ recovery: DesignRecoveryControls; disabled: boolean }>();
</script>

<template>
  <section v-if="recovery.supported.value" class="design-recovery" aria-label="Tab design recovery">
    <div><b>Keep unsaved changes in this tab</b><p>Optional recovery through reload and re-sign-in, for this account in this tab. Save before closing the tab.</p></div>
    <p class="recovery-status" role="status">{{ recovery.status.value }}</p>
    <div v-if="recovery.available.value" class="recovery-candidate">
      <b>{{ recovery.available.value.title || 'Untitled design' }}</b>
      <span>{{ new Date(recovery.available.value.savedAt).toLocaleString() }} · {{ recovery.available.value.snapshot.pages.length }} pages<span v-if="recovery.available.value.source"> · source revision {{ recovery.available.value.source.revision }}</span></span>
      <button type="button" class="quiet-button" :disabled="disabled" @click="recovery.restore">Restore as new copy</button>
    </div>
    <div class="recovery-actions"><button v-if="!recovery.enabled.value" type="button" class="quiet-button" :disabled="disabled" @click="recovery.enable">Enable tab recovery</button><button v-else type="button" class="quiet-button" :disabled="disabled" @click="recovery.disable">Stop keeping new changes</button><button v-if="recovery.hasStored.value" type="button" class="quiet-button" :disabled="disabled" @click="recovery.discard">Discard tab recovery</button></div>
    <p v-if="recovery.error.value" role="alert">{{ recovery.error.value }}</p>
  </section>
</template>

<style scoped>
.design-recovery { margin-block:16px; padding:16px 20px; border:1px solid #dcd6c8; border-radius:10px; background:#fbf9f3; display:grid; gap:8px; font-size:13px; line-height:1.6; }.design-recovery p { margin:0; }.design-recovery>div>p { color:#6b655b; font-size:12px; }.recovery-actions { display:flex; flex-wrap:wrap; gap:12px; }.recovery-candidate { display:grid; gap:4px; border-top:1px solid #dcd6c8; padding-top:10px; }.recovery-candidate button { justify-self:start; }.recovery-candidate span { color:#6b655b; font-size:12px; }.design-recovery [role=alert] { color:#963e2d; }
</style>
