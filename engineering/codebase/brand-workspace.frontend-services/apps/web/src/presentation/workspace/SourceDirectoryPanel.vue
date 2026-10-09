<script setup lang="ts">
import { computed, ref } from 'vue';
import { ExternalLink } from 'lucide-vue-next';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { sourceDirectory } from '@/domain/workspace/SourceDirectory';

/** Lists public discovery links without implying a provider connection or automatic synchronization. */
defineOptions({ name: 'SourceDirectoryPanel' });
const { t } = useUiLocale();
const area = ref('all');
/** The sources relevant to the selected workflow, including shared textile events. */
const sources = computed(() => sourceDirectory.filter(source =>
  area.value === 'all' || source.area === 'both' || source.area === area.value));
</script>

<template>
  <section class="space-y-4" aria-labelledby="source-directory-title">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 id="source-directory-title" class="text-xl font-semibold">{{ t('Discovery sources') }}</h2>
        <p class="mt-1 max-w-2xl text-sm text-muted-foreground">
          {{ t('Open a source, verify its current edition or quotation, and save the evidence in Events or Sourcing.') }}
        </p>
      </div>
      <label class="text-sm">{{ t('Source category') }}
        <select v-model="area" class="native-select mt-2">
          <option value="all">{{ t('All sources') }}</option>
          <option value="events">{{ t('Events') }}</option>
          <option value="sourcing">{{ t('Sourcing') }}</option>
        </select>
      </label>
    </div>
    <p class="text-sm text-muted-foreground">{{ t('Reviewed links · 9 October 2026 · No automatic synchronization') }}</p>
    <ul class="grid gap-x-6 sm:grid-cols-2 lg:grid-cols-3">
      <li v-for="source in sources" :key="source.url" class="border-b border-border py-4">
        <a :href="source.url" target="_blank" rel="noopener noreferrer"
          class="inline-flex items-center gap-2 font-medium text-primary underline-offset-4 hover:underline">
          {{ source.name }}<ExternalLink class="size-4 shrink-0" :aria-label="t('Opens in a new tab')" />
        </a>
        <p class="mt-1 text-sm text-muted-foreground">{{ t(source.kind) }}</p>
      </li>
    </ul>
  </section>
</template>
