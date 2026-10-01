<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { Download, Plus, Trash2, UploadCloud } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueAdmin, useCatalogueStore } from '../../application/catalogue';
import type { CatalogueSettings } from '../../domain/catalogue/CatalogueModels';
import TagInput from './components/TagInput.vue';

/** Brand settings, workspace import and exports. */
defineOptions({ name: 'SettingsPage' });
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
    <h1 class="font-editorial text-4xl tracking-tight">Sozlamalar</h1>

    <section v-if="form" class="panel" aria-labelledby="brand-heading">
      <h2 id="brand-heading" class="font-editorial text-2xl">Brend va turlar</h2>
      <div class="mt-4 grid gap-4 md:grid-cols-2">
        <div>
          <label class="label" for="brand">Brend nomi</label><input id="brand" v-model="form.brandName" class="field" />
        </div>
        <div>
          <label class="label" for="note">Narx izohi</label><input id="note" v-model="form.priceNote" class="field" />
        </div>
        <TagInput v-model="form.sizePreset" label="Standart o‘lchamlar" />
        <TagInput v-model="form.contactLines" label="Katalogdagi aloqa qatorlari" placeholder="Instagram: …" />
      </div>
      <h3 class="mt-6 text-sm font-semibold">Kiyim turlari</h3>
      <table class="mt-2 w-full text-sm">
        <thead class="text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
          <tr>
            <th class="py-2">Kalit</th>
            <th>O‘zbekcha</th>
            <th>Ruscha</th>
            <th>Inglizcha</th>
            <th />
          </tr>
        </thead>
        <tbody>
          <tr v-for="(g, index) in form.garmentTypes" :key="index" class="border-t border-border">
            <td class="py-2 pr-2"><input v-model="g.key" class="field" aria-label="Kalit" /></td>
            <td class="pr-2"><input v-model="g.uz" class="field" aria-label="O‘zbekcha" /></td>
            <td class="pr-2"><input v-model="g.ru" class="field" aria-label="Ruscha" /></td>
            <td class="pr-2"><input v-model="g.en" class="field" aria-label="Inglizcha" /></td>
            <td>
              <button class="btn-ghost" aria-label="O‘chirish" @click="form.garmentTypes.splice(index, 1)">
                <Icon :icon="Trash2" :size="14" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <div class="mt-4 flex gap-3">
        <button class="btn-outline" @click="form.garmentTypes.push({ key: '', uz: '', ru: '', en: '' })">
          <Icon :icon="Plus" :size="14" />Tur qo‘shish
        </button>
        <button class="btn-primary ml-auto" :disabled="admin.busy.value" @click="admin.saveSettings(form)">
          Saqlash
        </button>
      </div>
    </section>

    <section class="panel" aria-labelledby="import-heading">
      <h2 id="import-heading" class="font-editorial text-2xl">Maftunaning ish maydonidan import</h2>
      <p class="mt-2 text-sm text-muted-foreground">
        Manba:
        <code class="break-all rounded bg-muted px-1.5 py-0.5 text-xs">{{
          store.sourceRoot.value ?? 'sozlanmagan (Catalogue:SourceRoot)'
        }}</code
        >. Import faqat o‘qiydi: asl fayllar o‘zgarmaydi, har bir yozuvning SHA-256 izi saqlanadi.
      </p>
      <div class="mt-4 flex flex-wrap items-center gap-4 text-sm">
        <label class="flex items-center gap-2"
          ><input v-model="updateExisting" type="checkbox" />Mavjudlarini manba o‘zgargan bo‘lsa yangilash</label
        >
        <label class="flex items-center gap-2"
          ><input v-model="includeReferences" type="checkbox" />Oq fonli namuna rasmlarini ham olish</label
        >
        <button
          class="btn-primary ml-auto"
          :disabled="admin.busy.value"
          @click="admin.importWorkspace(updateExisting, includeReferences)"
        >
          <Icon :icon="UploadCloud" :size="15" />{{ admin.busy.value ? 'Import…' : 'Importni boshlash' }}
        </button>
      </div>
      <div v-if="admin.report.value" class="mt-4 rounded-xl bg-muted p-4 text-sm">
        <p>
          {{ admin.report.value.scanned }} ta yozuv ko‘rildi · {{ admin.report.value.created }} yangi ·
          {{ admin.report.value.updated }} yangilandi · {{ admin.report.value.imagesStored }} ta rasm
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
      <h2 id="export-heading" class="font-editorial text-2xl">Eksport</h2>
      <div class="mt-4 flex flex-wrap gap-3">
        <a class="btn-outline" :href="admin.exports.csv" download><Icon :icon="Download" :size="15" />Narxlar (CSV)</a>
        <a class="btn-outline" :href="admin.exports.json" download
          ><Icon :icon="Download" :size="15" />To‘liq zaxira (JSON)</a
        >
        <a class="btn-outline" :href="admin.exports.workspace" download
          ><Icon :icon="Download" :size="15" />Ish maydoni formati (ZIP)</a
        >
      </div>
      <p class="mt-3 text-xs text-muted-foreground">
        ZIP — har bir mahsulot uchun <code>och-{id}.json</code> (sxema 1.3). Ish maydoniga qo‘lda qaytarish uchun.
      </p>
    </section>
  </section>
</template>
