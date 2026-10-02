<script setup lang="ts">
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
withDefaults(defineProps<{ busy?: boolean; retained?: boolean }>(), { busy: false, retained: false });
defineEmits<{ retry: [] }>();
</script>
<template>
  <section
    class="request-unavailable panel flex flex-wrap items-center justify-between gap-4"
    :aria-label="t('Content unavailable')"
  >
    <p class="text-sm text-muted-foreground">
      {{ retained ? t('Your last saved content is retained. Try refreshing again.') : t('Content unavailable') }}
    </p>
    <button type="button" class="btn-outline" :disabled="busy" :aria-busy="busy" @click="$emit('retry')">
      {{ busy ? t('Working…') : t('Try again') }}
    </button>
  </section>
</template>
