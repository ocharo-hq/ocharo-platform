<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit } = useIdentitySession();
import { onMounted, ref, watch } from 'vue';
import { Download, Plus, Trash2, UploadCloud } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card, LabelText } from '@wow-two-beta/fe/presentation/display';
import { CheckboxField, TextInput } from '@wow-two-beta/fe/presentation/forms';
import { useCatalogueAdmin, useCatalogueSettings } from '../../application/catalogue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { CatalogueSettings } from '../../domain/catalogue/CatalogueModels';
import TagInput from './components/TagInput.vue';

/**
 * Edits catalogue defaults and imports or downloads product records. Interface language, appearance and versions
 * live in the account menu, so this page holds only catalogue data.
 */
defineOptions({ name: 'SettingsPage' });
const { t } = useUiLocale();
const settings = useCatalogueSettings();
const admin = useCatalogueAdmin();
const form = ref<CatalogueSettings | null>(null);
const updateExisting = ref(false);
const includeReferences = ref(false);
watch(
  () => settings.settings.value,
  (value) => {
    form.value = value ? structuredClone(value) : null;
  },
  { immediate: true },
);
onMounted(() => void settings.reload());
</script>

<template>
  <section class="space-y-6">
    <h1 class="font-editorial text-4xl tracking-tight">{{ t('Settings') }}</h1>

    <Card
      v-if="form"
      v-bind="{ role: 'region', 'aria-labelledby': 'catalogue-defaults-heading' }"
      radius="2xl"
      padding="xl"
    >
      <h2 id="catalogue-defaults-heading" class="font-editorial text-2xl">{{ t('Catalogue defaults') }}</h2>
      <p class="mt-2 text-sm text-muted-foreground">
        {{ t('These defaults apply to this catalogue app. They do not change Ocharo’s global brand identity.') }}
      </p>
      <div class="mt-4 grid gap-4 md:grid-cols-2">
        <div>
          <LabelText for="brand" class="mb-1.5 block">{{ t('Brand name') }}</LabelText>
          <TextInput id="brand" v-model="form.brandName" />
        </div>
        <div>
          <LabelText for="note" class="mb-1.5 block">{{ t('Price note') }}</LabelText>
          <TextInput id="note" :model-value="form.priceNote ?? ''" @update:model-value="form.priceNote = $event" />
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
              <td class="py-2 pr-2"><TextInput v-model="g.key" :aria-label="t('Garment type key')" /></td>
              <td class="pr-2"><TextInput v-model="g.uz" :aria-label="t('Garment type in Uzbek')" /></td>
              <td class="pr-2"><TextInput v-model="g.ru" :aria-label="t('Garment type in Russian')" /></td>
              <td class="pr-2"><TextInput v-model="g.en" :aria-label="t('Garment type in English')" /></td>
              <td>
                <Button
                  variant="ghost"
                  tone="neutral"
                  shape="square"
                  :aria-label="t('Remove garment type')"
                  @click="form.garmentTypes.splice(index, 1)"
                >
                  <Icon :icon="Trash2" :size="14" />
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-4 flex flex-wrap gap-3">
        <Button variant="outline" tone="neutral" @click="form.garmentTypes.push({ key: '', uz: '', ru: '', en: '' })">
          <template #leading><Icon :icon="Plus" :size="14" /></template>
          {{ t('Add garment type') }}
        </Button>
        <Button
          class="ml-auto"
          :is-disabled="!sessionCanEdit"
          :is-loading="admin.busy.value"
          @click="admin.saveSettings(form)"
        >
          {{ t(admin.busy.value ? 'Saving…' : 'Save') }}
        </Button>
      </div>
    </Card>

    <section class="space-y-4" aria-labelledby="catalogue-data-heading">
      <h2 id="catalogue-data-heading" class="font-editorial text-2xl">{{ t('Catalogue data') }}</h2>
      <Card v-bind="{ role: 'region', 'aria-labelledby': 'import-heading' }" radius="2xl" padding="xl">
        <h3 id="import-heading" class="font-editorial text-2xl">{{ t('Import product records') }}</h3>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t('Source:') }}
          <code class="break-all rounded bg-muted px-1.5 py-0.5 text-xs">{{
            settings.sourceRoot.value || t('Not configured')
          }}</code>
        </p>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t('Import keeps the source files unchanged and records a SHA-256 fingerprint for each record.') }}
        </p>
        <div class="mt-4 flex flex-wrap items-center gap-4 text-sm">
          <CheckboxField v-model="updateExisting" :label="t('Update existing records when the source changes')" />
          <CheckboxField v-model="includeReferences" :label="t('Include white-background reference images')" />
          <Button
            class="ml-auto"
            :is-disabled="!sessionCanEdit"
            :is-loading="admin.busy.value"
            @click="admin.importWorkspace(updateExisting, includeReferences)"
          >
            <template #leading><Icon :icon="UploadCloud" :size="15" /></template>
            {{ t(admin.busy.value ? 'Importing…' : 'Start import') }}
          </Button>
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
      </Card>

      <Card v-bind="{ role: 'region', 'aria-labelledby': 'export-heading' }" radius="2xl" padding="xl">
        <h3 id="export-heading" class="font-editorial text-2xl">{{ t('Download catalogue data') }}</h3>
        <div class="mt-4 flex flex-wrap gap-3">
          <Button as-child variant="outline" tone="neutral">
            <a :href="admin.exports.csv" download><Icon :icon="Download" :size="15" />{{ t('Prices (CSV)') }}</a>
          </Button>
          <Button as-child variant="outline" tone="neutral">
            <a :href="admin.exports.json" download><Icon :icon="Download" :size="15" />{{ t('Full backup (JSON)') }}</a>
          </Button>
          <Button as-child variant="outline" tone="neutral">
            <a :href="admin.exports.workspace" download
              ><Icon :icon="Download" :size="15" />{{ t('Workspace format (ZIP)') }}</a
            >
          </Button>
        </div>
        <p class="mt-3 text-xs text-muted-foreground">
          {{ t('ZIP contains one file per product:') }} <code>och-{id}.json</code>.
          {{ t('Schema {schema}. Restore these files to the workspace manually.', { schema: '1.3' }) }}
        </p>
      </Card>
    </section>
  </section>
</template>
