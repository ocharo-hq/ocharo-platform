<script setup lang="ts">
import { computed, nextTick, onMounted, onScopeDispose, ref } from 'vue';
import type { DesignPreflightReview } from '@/application/workspace/UseDesignPreflight';
import type { DesignPreflightIssue } from '@/domain/workspace/DesignPreflight';

defineOptions({ name: 'DesignPreflightReview' });
const props = defineProps<{ review: DesignPreflightReview }>();
const emit = defineEmits<{ resolve: [allowed: boolean]; inspect: [issue: DesignPreflightIssue] }>();
const dialog = ref<HTMLDialogElement>(), acknowledged = ref(false);
const hasErrors = computed(() => props.review.report.issues.some(issue => issue.severity === 'error'));
const needsApproval = computed(() => !!props.review.report.issues.length || props.review.unsaved);
const previousFocus = document.activeElement;
onMounted(async () => { await nextTick(); dialog.value?.showModal(); });
onScopeDispose(() => { dialog.value?.close(); if (previousFocus instanceof HTMLElement) previousFocus.focus(); });
function inspect(issue: DesignPreflightIssue): void { emit('resolve', false); emit('inspect', issue); }
</script>

<template>
  <dialog ref="dialog" class="preflight-dialog" aria-labelledby="preflight-heading" @cancel.prevent="emit('resolve', false)">
    <div class="preflight-heading"><div><p class="eyebrow">Export check</p><h2 id="preflight-heading">{{ hasErrors ? 'Fix the missing images.' : review.report.issues.length ? 'Review these page details.' : 'Pages checked.' }}</h2></div><button class="quiet-button" aria-label="Close export check" @click="emit('resolve', false)">Close</button></div>
    <p>{{ review.report.pageCount }} {{ review.report.pageCount === 1 ? 'page' : 'pages' }} checked against the PDF renderer.</p>
    <p v-if="review.unsaved" class="preflight-warning">This download includes unsaved edits. Save your design to keep this version.</p>
    <p v-if="!review.report.issues.length" role="status">Images are available. No page-edge or text-frame overflow was detected.</p>
    <ul v-if="review.report.issues.length" class="preflight-issues">
      <li v-for="(issue,index) in review.report.issues" :key="`${issue.pageId}-${issue.elementId}-${issue.code}`" :class="{'preflight-error':issue.severity === 'error'}">
        <b>{{ issue.severity === 'error' ? 'Fix required' : 'Review layout' }} · Page {{ issue.pageNumber }} · {{ issue.pageTitle }}</b>
        <span>{{ issue.elementLabel }} — {{ issue.message }}</span>
        <button class="quiet-button" :aria-label="`Inspect issue ${index + 1}: ${issue.elementLabel} on page ${issue.pageNumber}`" @click="inspect(issue)">Show element →</button>
      </li>
    </ul>
    <p v-if="!hasErrors && review.report.issues.length">Intentional crops and backgrounds may extend beyond the page. Confirm their appearance in PDF preview.</p>
    <label v-if="needsApproval && !hasErrors && !review.inspectionOnly" class="preflight-acknowledge"><input v-model="acknowledged" type="checkbox"> I reviewed these details and want to continue with this exact version.</label>
    <footer><button class="quiet-button" @click="emit('resolve', false)">{{ review.inspectionOnly ? 'Done' : 'Return to editing' }}</button><button v-if="!review.inspectionOnly && !hasErrors" class="preflight-continue" :disabled="needsApproval && !acknowledged" @click="emit('resolve', true)">{{ review.actionLabel }}</button></footer>
  </dialog>
</template>

<style scoped>
.preflight-dialog { margin:auto; color:#342b23; background:#fffdf8; border:1px solid #d7cbbb; border-radius:14px; padding:24px; width:min(640px,calc(100vw - 32px)); max-height:85dvh; overflow:auto; box-shadow:0 20px 80px #21180e44; }.preflight-dialog::backdrop { background:#21180e80; }.preflight-heading { display:flex; justify-content:space-between; gap:20px; align-items:start; }.preflight-heading h2 { font:26px Georgia,serif; margin:7px 0; }.preflight-dialog p,.preflight-dialog li,.preflight-acknowledge { font-size:13px; line-height:1.6; }.preflight-issues { list-style:none; margin:16px 0; padding:0; display:grid; gap:12px; }.preflight-issues li { display:grid; gap:5px; border:1px solid #ddd1be; border-radius:8px; padding:12px; }.preflight-issues button { justify-self:start; }.preflight-error { border-color:#b77769!important; }.preflight-warning { background:#f4ebd7; padding:12px; border-radius:6px; }.preflight-acknowledge { display:flex; gap:10px; align-items:start; }.preflight-acknowledge input { margin-top:5px; }.preflight-dialog footer { display:flex; justify-content:flex-end; flex-wrap:wrap; gap:12px; margin-top:20px; }.preflight-continue { background:#40573e; color:#fff; border:0; border-radius:6px; padding:10px 15px; cursor:pointer; }.preflight-continue:disabled { opacity:.5; cursor:default; }
</style>
