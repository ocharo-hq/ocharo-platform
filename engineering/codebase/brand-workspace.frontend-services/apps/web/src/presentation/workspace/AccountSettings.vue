<script setup lang="ts">
import { computed } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { useUiLocale } from '@/application/shared/UseUiLocale';
defineOptions({ name: 'AccountSettings' });
const props = defineProps<{ access: AccessControls }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useUiLocale();
const user = computed(() => props.access.session.value?.user ?? null);
const role = computed(() => props.access.session.value?.role ?? null);
</script>
<template>
  <Modal :open="true" @update:open="value => { if (!value) emit('close'); }">
    <ModalContent class="w-full max-w-lg rounded-2xl border border-border bg-card p-6 text-foreground">
      <ModalTitle class="text-xl font-semibold">{{ t('Account settings') }}</ModalTitle>
      <section class="mt-5 space-y-2">
        <p v-if="user?.displayName" class="font-medium">{{ user.displayName }}</p>
        <p v-if="user?.phone" class="text-sm">{{ user.phone }}</p>
        <p v-if="user?.email" class="text-sm">{{ user.email }}</p>
        <p v-if="role" class="text-sm text-muted-foreground">{{ t(role) }}</p>
        <p class="pt-2 text-sm text-muted-foreground">{{ t('Your sign-in, phone and organization roles are managed in Ocharo Identity.') }}</p>
      </section>
      <footer class="mt-6 flex justify-end gap-2">
        <Button variant="ghost" @click="emit('close')">{{ t('Close') }}</Button>
      </footer>
    </ModalContent>
  </Modal>
</template>
