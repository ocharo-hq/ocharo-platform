<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { Download, Plus, Trash2, UploadCloud } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueAdmin, useCatalogueStore } from '../../application/catalogue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { CatalogueSettings } from '../../domain/catalogue/CatalogueModels';
import TagInput from './components/TagInput.vue';
import InterfacePreferences from '../common/InterfacePreferences.vue';

/** Edits catalogue defaults and imports or downloads product records. */
defineOptions({ name: 'SettingsPage' });
const { t } = useUiLocale();
const store = useCatalogueStore();
const admin = useCatalogueAdmin();
const form = ref<CatalogueSettings | null>(null);
const updateExisting = ref(false);
const includeReferences = ref(false);
watch(
  () => store.settings.value,
  (value) => {
    form.value = value ? structuredClone(value) : null;
  },
  { immediate: true },
);
onMounted(() => store.loadSettings(true));
</script>

<template>
  <section class="space-y-6">
    <h1 class="font-editorial text-4xl tracking-tight">{{ t('Settings') }}</h1>

    <section class="panel" aria-labelledby="interface-settings-heading">
      <h2 id="interface-settings-heading" class="mb-4 font-editorial text-2xl">{{ t('Interface settings') }}</h2>
      <InterfacePreferences />
    </section>

    <section v-if="form" class="panel" aria-labelledby="catalogue-defaults-heading">
      <h2 id="catalogue-defaults-heading" class="font-editorial text-2xl">{{ t('Catalogue defaults') }}</h2>
      <p class="mt-2 text-sm text-muted-foreground">
        {{ t('These defaults apply to this catalogue app. They do not change Ocharo’s global brand identity.') }}
      </p>
      <div class="mt-4 grid gap-4 md:grid-cols-2">
        <div>
          <label class="label" for="brand">{{ t('Brand name') }}</label>
          <input id="brand" v-model="form.brandName" class="field" />
        </div>
        <div>
          <label class="label" for="note">{{ t('Price note') }}</label>
          <input id="note" v-model="form.priceNote" class="field" />
        </div>
        <TagInput v-model="form.sizePreset" :label="t('Default sizes')" />
        <TagInput
          v-model="form.contactLines"
          :label="t('Catalogue contact lines')"
          :placeholder="t('Instagram: @yourname')"
        />
      </div>
      <h3 class="mt-6 text-sm font-semibold">{{ t('Garment types') }}</h3>
      <div class="mt-2 overflow-x-auto">
        <table class="w-full min-w-[40rem] text-sm">
          <thead class="text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
            <tr>
              <th class="py-2">{{ t('Key') }}</th>
              <th>{{ t('Uzbek') }}</th>
              <th>{{ t('Russian') }}</th>
              <th>{{ t('English') }}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr v-for="(g, index) in form.garmentTypes" :key="index" class="border-t border-border">
              <td class="py-2 pr-2"><input v-model="g.key" class="field" :aria-label="t('Garment type key')" /></td>
              <td class="pr-2"><input v-model="g.uz" class="field" :aria-label="t('Garment type in Uzbek')" /></td>
              <td class="pr-2"><input v-model="g.ru" class="field" :aria-label="t('Garment type in Russian')" /></td>
              <td class="pr-2"><input v-model="g.en" class="field" :aria-label="t('Garment type in English')" /></td>
              <td>
                <button
                  class="btn-ghost"
                  :aria-label="t('Remove garment type')"
                  @click="form.garmentTypes.splice(index, 1)"
                >
                  <Icon :icon="Trash2" :size="14" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-4 flex flex-wrap gap-3">
        <button class="btn-outline" @click="form.garmentTypes.push({ key: '', uz: '', ru: '', en: '' })">
          <Icon :icon="Plus" :size="14" />{{ t('Add garment type') }}
        </button>
        <button class="btn-primary ml-auto" :disabled="admin.busy.value" @click="admin.saveSettings(form)">
          {{ t(admin.busy.value ? 'Saving…' : 'Save') }}
        </button>
      </div>
    </section>

    <section class="space-y-4" aria-labelledby="catalogue-data-heading">
      <h2 id="catalogue-data-heading" class="font-editorial text-2xl">{{ t('Catalogue data') }}</h2>
      <section class="panel" aria-labelledby="import-heading">
        <h3 id="import-heading" class="font-editorial text-2xl">{{ t('Import product records') }}</h3>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t('Source:') }}
          <code class="break-all rounded bg-muted px-1.5 py-0.5 text-xs">{{
            store.sourceRoot.value || t('Not configured')
          }}</code>
        </p>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t('Import keeps the source files unchanged and records a SHA-256 fingerprint for each record.') }}
        </p>
        <div class="mt-4 flex flex-wrap items-center gap-4 text-sm">
          <label class="flex items-center gap-2">
            <input v-model="updateExisting" type="checkbox" />{{ t('Update existing records when the source changes') }}
          </label>
          <label class="flex items-center gap-2">
            <input v-model="includeReferences" type="checkbox" />{{ t('Include white-background reference images') }}
          </label>
          <button
            class="btn-primary ml-auto"
            :disabled="admin.busy.value"
            @click="admin.importWorkspace(updateExisting, includeReferences)"
          >
            <Icon :icon="UploadCloud" :size="15" />{{ t(admin.busy.value ? 'Importing…' : 'Start import') }}
          </button>
        </div>
        <div v-if="admin.report.value" class="mt-4 rounded-xl bg-muted p-4 text-sm">
          <p>
            {{
              t('Scanned: {scanned} · Created: {created} · Updated: {updated} · Images: {images}', {
                scanned: admin.report.value.scanned,
                created: admin.report.value.created,
                updated: admin.report.value.updated,
                images: admin.report.value.imagesStored,
              })
            }}
          </p>
          <ul
            v-if="admin.report.value.warnings.length"
            class="mt-2 max-h-40 list-disc overflow-y-auto pl-5 text-xs text-muted-foreground"
          >
            <li v-for="w in admin.report.value.warnings" :key="w">{{ w }}</li>
          </ul>
        </div>
      </section>

      <section class="panel" aria-labelledby="export-heading">
        <h3 id="export-heading" class="font-editorial text-2xl">{{ t('Download catalogue data') }}</h3>
        <div class="mt-4 flex flex-wrap gap-3">
          <a class="btn-outline" :href="admin.exports.csv" download>
            <Icon :icon="Download" :size="15" />{{ t('Prices (CSV)') }}
          </a>
          <a class="btn-outline" :href="admin.exports.json" download>
            <Icon :icon="Download" :size="15" />{{ t('Full backup (JSON)') }}
          </a>
          <a class="btn-outline" :href="admin.exports.workspace" download>
            <Icon :icon="Download" :size="15" />{{ t('Workspace format (ZIP)') }}
          </a>
        </div>
        <p class="mt-3 text-xs text-muted-foreground">
          {{ t('ZIP contains one file per product:') }} <code>och-{id}.json</code>.
          {{ t('Schema {schema}. Restore these files to the workspace manually.', { schema: '1.3' }) }}
        </p>
      </section>
    </section>
  </section>
</template>
