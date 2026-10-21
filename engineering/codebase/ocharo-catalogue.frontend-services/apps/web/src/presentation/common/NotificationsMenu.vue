<script setup lang="ts">
import { onScopeDispose, ref, shallowRef, watch } from 'vue';
import { useRoute } from 'vue-router';
import { Bell } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { NotificationCenterGroup } from '@wow-two-beta/fe/presentation/display';
import { Popover, PopoverContent, PopoverTrigger } from '@wow-two-beta/fe/presentation/overlays';
import { feedbackBus } from '../../application/common/UseFeedback';
import { useUiLocale } from '../../application/shared/UseUiLocale';

defineOptions({ name: 'NotificationsMenu' });
const { t } = useUiLocale();
const route = useRoute();
const open = ref(false);
const count = shallowRef(feedbackBus.retained().length);
onScopeDispose(feedbackBus.subscribeRetained((notices) => (count.value = notices.length)));
watch(
  () => route.fullPath,
  () => (open.value = false),
);
</script>

<template>
  <Popover v-model:open="open" placement="bottom-end">
    <PopoverTrigger as-child>
      <button
        type="button"
        :aria-label="t('Notifications')"
        :title="t('Notifications')"
        class="relative flex min-h-11 min-w-11 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Icon :icon="Bell" :size="20" />
        <span
          v-if="count"
          class="absolute right-0.5 top-0.5 min-w-4 rounded-full bg-primary px-1 text-center text-[10px] font-semibold leading-4 text-primary-foreground"
          :aria-label="t('{count} notifications', { count })"
          >{{ count }}</span
        >
      </button>
    </PopoverTrigger>
    <PopoverContent variant="glass-menu" class="no-print w-[min(24rem,calc(100vw-2rem))] rounded-2xl p-0">
      <NotificationCenterGroup
        :bus="feedbackBus"
        :title="t('Notifications')"
        class="w-full border-0 bg-transparent shadow-none"
        @close="open = false"
      >
        <template #emptyState
          ><p class="px-4 py-10 text-center text-sm text-muted-foreground">
            {{ t('No notifications yet.') }}
          </p></template
        >
      </NotificationCenterGroup>
    </PopoverContent>
  </Popover>
</template>
