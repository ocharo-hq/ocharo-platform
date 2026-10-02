<script setup lang="ts">
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { RefreshCw } from 'lucide-vue-next';
import { useUiLocale } from '../../application/shared/UseUiLocale';
withDefaults(defineProps<{ busy?: boolean; disabled?: boolean }>(), { busy: false, disabled: false });
defineEmits<{ refresh: [] }>();
const { t } = useUiLocale();
</script>
<template>
  <Button
    class="catalogue-refresh"
    variant="outline"
    :is-loading="busy"
    :is-disabled="disabled"
    @click="$emit('refresh')"
  >
    <template #leading><Icon :icon="RefreshCw" :size="16" /></template>
    {{ t('Refresh') }}
  </Button>
</template>
