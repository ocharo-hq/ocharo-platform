<script setup lang="ts">
import { computed } from 'vue';
import { Copy } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Modal, ModalContent, ModalTitle, Tooltip } from '@wow-two-beta/fe/presentation/overlays';
import type { AccessControls } from '@/application/common/hooks/UseAccess';
import { feedbackBus } from '@/application/common/hooks/UseFeedback';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
defineOptions({ name: 'AccountSettings' });
const props = defineProps<{ access: AccessControls }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useUiLocale();
const user = computed(() => props.access.session.value?.user ?? null);
const role = computed(() => props.access.session.value?.role ?? null);
/** Copies the Identity `sub`; the toast says so, and a blocked clipboard says it did not. */
async function copyUserId(): Promise<void> {
  const id = user.value?.id;
  if (!id) return;
  try {
    await navigator.clipboard.writeText(id);
    feedbackBus.notify({ tone: 'success', title: t('Copied'), id: 'user-id-copied', durationMs: 3000 });
  } catch {
    feedbackBus.notify({ tone: 'danger', title: t('Could not copy'), id: 'user-id-copied', durationMs: 5000 });
  }
}
</script>
<template>
  <Modal :open="true" @update:open="value => { if (!value) emit('close'); }">
    <ModalContent class="w-full max-w-lg rounded-2xl border border-border bg-card p-6 text-foreground">
      <ModalTitle class="text-xl font-semibold">{{ t('Account settings') }}</ModalTitle>
      <section class="mt-5 space-y-2">
        <img v-if="user?.picture" :src="user.picture" alt="" class="size-16 rounded-full object-cover" referrerpolicy="no-referrer" />
        <p v-if="user?.displayName" class="font-medium">{{ user.displayName }}</p>
        <p v-if="user?.phone" class="text-sm">{{ user.phone }}</p>
        <p v-if="user?.email" class="text-sm">{{ user.email }}</p>
        <p v-if="role" class="text-sm text-muted-foreground">{{ t(role) }}</p>
        <div v-if="user?.id" class="pt-2" data-user-id>
          <p class="text-xs font-medium text-muted-foreground">{{ t('User ID') }}</p>
          <div class="mt-1 flex items-center gap-2">
            <code data-user-id-value class="min-w-0 flex-1 select-all break-all rounded-md border border-border bg-muted px-2 py-1 font-mono text-xs">{{ user.id }}</code>
            <Tooltip :content="t('Copy user ID')">
              <Button variant="ghost" size="sm" data-user-id-copy :aria-label="t('Copy user ID')" @click="copyUserId"><Copy class="size-4" aria-hidden="true" /></Button>
            </Tooltip>
          </div>
          <p class="mt-1 text-xs text-muted-foreground">{{ t('Share it when an app asks for your user ID.') }}</p>
        </div>
        <p class="pt-2 text-sm text-muted-foreground">{{ t('Your sign-in, phone and organization roles are managed in Ocharo Identity.') }}</p>
      </section>
      <footer class="mt-6 flex justify-end gap-2">
        <Button variant="ghost" @click="emit('close')">{{ t('Close') }}</Button>
      </footer>
    </ModalContent>
  </Modal>
</template>
