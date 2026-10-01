<script setup lang="ts">
import { useErrorToast } from '@/application/shared/UseFeedback';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { onScopeDispose, ref, watch } from 'vue';
import { renderDesignPage } from '@/application/workspace/ExportDocument';
import type { DesignPage } from '@/domain/workspace/EditorModels';

const { t } = useUiLocale();

defineOptions({ name: 'DesignPrintPreview' });
const props = defineProps<{ page: DesignPage }>();
const holder = ref<HTMLElement>();
const busy = ref(false), error = ref<string | null>(null);
useErrorToast(error);
let sequence = 0;
async function render(): Promise<void> {
  const page = props.page;
  const request = ++sequence; busy.value = true; error.value = '';
  try {
    const canvas = await renderDesignPage(page, 240);
    if (request !== sequence) { canvas.width = 0; canvas.height = 0; return; }
    canvas.style.width = '100%'; canvas.style.height = 'auto'; canvas.style.display = 'block';
    canvas.setAttribute('role', 'img'); canvas.setAttribute('aria-label', `${page.title} exact PDF preview`);
    const old = holder.value?.querySelector('canvas'); if (old) { old.width = 0; old.height = 0; }
    holder.value?.replaceChildren(canvas);
  } catch { if (request === sequence) error.value = 'Unable to render this page.'; }
  finally { if (request === sequence) busy.value = false; }
}
watch(() => props.page, render, { immediate: true });
onScopeDispose(() => { sequence++; const canvas = holder.value?.querySelector('canvas'); if (canvas) { canvas.width = 0; canvas.height = 0; } });
</script>

<template>
  <div class="print-preview" :style="{ aspectRatio: `${page.width} / ${page.height}`, background: page.background }" :aria-busy="busy">
    <div ref="holder" v-show="!busy && !error" class="print-preview-canvas" />
    <ContentSkeleton v-if="busy" class="preview-message" label="Rendering PDF preview…" :cards="1" />
    <div v-if="error" class="preview-message">
      <p>{{ t("Preview unavailable") }}</p>
      <button type="button" class="quiet-button" @click="render">{{ t("Retry preview") }}</button>
    </div>
  </div>
</template>

<style scoped>
.print-preview { position: relative; width: 100%; box-shadow: 0 12px 36px #21180e26; }
.print-preview-canvas { width: 100%; height: 100%; }.preview-message { position: absolute; inset: 0; margin: 0; padding: 24px; display: grid; place-items: center; background: #fbf9f3e8; font: 13px/1.6 Arial,sans-serif; text-align: center; }.preview-error { color: #743e29; }
</style>
