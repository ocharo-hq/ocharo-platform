<script setup lang="ts">
import { onScopeDispose } from 'vue';
import { TriangleAlert } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { FeedbackToastHost } from '@wow-two-beta/fe/presentation/feedback';
import { AlertModal, AlertModalContent, ModalTitle, ModalDescription } from '@wow-two-beta/fe/presentation/overlays';
import { confirmation, feedbackBus, resolveConfirmation } from '../../application/common/UseFeedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
onScopeDispose(() => resolveConfirmation(false));
</script>
<template>
  <FeedbackToastHost :bus="feedbackBus" />
  <AlertModal
    :open="!!confirmation"
    @update:open="
      (value) => {
        if (!value) resolveConfirmation(false);
      }
    "
  >
    <AlertModalContent v-if="confirmation" :is-blurred="true" class="w-[min(32rem,calc(100vw-2rem))]">
      <Icon :icon="TriangleAlert" :size="32" class="mb-4 text-warning" />
      <ModalTitle class="text-xl font-semibold">{{ t(confirmation.title) }}</ModalTitle>
      <ModalDescription class="mt-3 leading-7 text-muted-foreground">{{ t(confirmation.message) }}</ModalDescription>
      <div class="mt-6 flex justify-end gap-3">
        <button type="button" class="btn-outline" autofocus @click="resolveConfirmation(false)">
          {{ t('Cancel') }}
        </button>
        <button type="button" class="btn-primary" @click="resolveConfirmation(true)">
          {{ t(confirmation.confirmLabel) }}
        </button>
      </div>
    </AlertModalContent>
  </AlertModal>
</template>
