<script setup lang="ts">
import { useSsoSession } from '../../application/common/UseSsoSession';
const { canEdit: ssoCanEdit } = useSsoSession();
import { computed, ref, toRaw, watch } from 'vue';
import { SkeletonState } from '@wow-two-beta/ui-vue/presentation/feedback';
import IdentityFormSkeleton from '../common/IdentityFormSkeleton.vue';
import { Plus, Save, Trash2 } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useBrand } from '../../application/brand';
import type { BrandIdentity } from '../../domain/brand/BrandModels';

/** Name, spelling rule, descriptor/tagline/location in three languages, palette, type and official links. */
defineOptions({ name: 'IdentityPage' });
const brand = useBrand();
const form = ref<BrandIdentity | null>(null);
const baseline = ref('');
const baselineRevision = ref<number | null>(null);
const saving = ref(false);
watch(
  () => brand.draft.value,
  (view) => {
    if (!view || (form.value && JSON.stringify(form.value) !== baseline.value)) return;
    const identity = view.draft.identity;
    form.value = structuredClone(identity);
    baseline.value = JSON.stringify(identity);
    baselineRevision.value = view.revision;
  },
  { immediate: true },
);
const dirty = computed(() => form.value !== null && JSON.stringify(form.value) !== baseline.value);
async function save() {
  if (!form.value || baselineRevision.value === null || brand.busy.value || saving.value) return;
  const submitted = structuredClone(toRaw(form.value));
  const expectedRevision = baselineRevision.value;
  saving.value = true;
  try {
    const acknowledged = await brand.saveIdentity(submitted, expectedRevision);
    if (acknowledged) {
      baseline.value = JSON.stringify(submitted);
      baselineRevision.value = acknowledged.revision;
    }
  } finally {
    saving.value = false;
  }
}
const locales = [
  { code: 'uz', label: 'O‘zbekcha' },
  { code: 'ru', label: 'Ruscha' },
  { code: 'en', label: 'Inglizcha' },
] as const;
const texts = [
  { key: 'descriptor', label: 'Tavsif (kartochka old tomoni)' },
  { key: 'tagline', label: 'Shior' },
  { key: 'location', label: 'Joylashuv' },
] as const;
</script>

<template>
  <section class="space-y-6 pb-20">
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">Identitet</h1>
        <p class="mt-2 text-sm text-muted-foreground">Qoralamaga saqlanadi; ilovalar keyingi nashrdan keyin ko‘radi.</p>
      </div>
      <button class="btn-primary" :disabled="!ssoCanEdit || !dirty || brand.busy.value || saving" @click="save">
        <Icon :icon="Save" :size="15" />Saqlash
      </button>
    </div>

    <template v-if="form">
      <section class="panel">
        <div class="grid gap-4 md:grid-cols-[240px_1fr]">
          <div>
            <label class="label" for="name">Brend nomi</label
            ><input id="name" v-model="form.name" class="field text-lg" />
          </div>
          <div>
            <label class="label" for="rule">Yozilish qoidasi</label
            ><textarea id="rule" v-model="form.spellingRule" rows="2" class="field"></textarea>
          </div>
        </div>
      </section>

      <section class="panel">
        <h2 class="font-editorial text-2xl">Matnlar</h2>
        <div v-for="text in texts" :key="text.key" class="mt-4">
          <p class="label">{{ text.label }}</p>
          <div class="grid gap-3 md:grid-cols-3">
            <input
              v-for="l in locales"
              :key="l.code"
              v-model="form[text.key][l.code]"
              class="field"
              :placeholder="l.label"
              :aria-label="`${text.label} — ${l.label}`"
            />
          </div>
        </div>
        <div class="mt-4">
          <label class="label" for="voice">Ovoz va uslub eslatmalari</label
          ><textarea id="voice" v-model="form.voiceNotes" rows="3" class="field"></textarea>
        </div>
      </section>

      <section class="panel">
        <div class="flex items-center justify-between">
          <h2 class="font-editorial text-2xl">Ranglar</h2>
          <button
            class="btn-outline"
            @click="form.palette.push({ key: 'new', name: 'Yangi rang', hex: '#888888', role: 'accent' })"
          >
            <Icon :icon="Plus" :size="14" />Rang
          </button>
        </div>
        <p class="mt-1 text-xs text-muted-foreground">
          Har bir kalit ilovalarda <code>--ocharo-{kalit}</code> CSS tokeniga aylanadi.
        </p>
        <ul class="mt-4 grid gap-3 md:grid-cols-2">
          <li
            v-for="(color, index) in form.palette"
            :key="index"
            class="flex items-center gap-3 rounded-xl border border-border p-3"
          >
            <input
              v-model="color.hex"
              type="color"
              class="h-12 w-12 shrink-0 cursor-pointer rounded-lg border border-border"
              :aria-label="`${color.name} rangi`"
            />
            <div class="grid flex-1 gap-2 sm:grid-cols-3">
              <input v-model="color.key" class="field font-mono text-xs" aria-label="Kalit" />
              <input v-model="color.name" class="field" aria-label="Nomi" />
              <input v-model="color.hex" class="field font-mono text-xs" aria-label="Hex" />
            </div>
            <button
              class="btn-ghost text-destructive"
              aria-label="Rangni o‘chirish"
              @click="form.palette.splice(index, 1)"
            >
              <Icon :icon="Trash2" :size="14" />
            </button>
          </li>
        </ul>
      </section>

      <section class="panel">
        <h2 class="font-editorial text-2xl">Shriftlar</h2>
        <div class="mt-4 grid gap-4 md:grid-cols-3">
          <div>
            <label class="label" for="display">Sarlavha shrifti</label
            ><input id="display" v-model="form.typography.display" class="field" />
          </div>
          <div>
            <label class="label" for="body">Matn shrifti</label
            ><input id="body" v-model="form.typography.body" class="field" />
          </div>
          <div>
            <label class="label" for="tnotes">Izoh</label
            ><input id="tnotes" v-model="form.typography.notes" class="field" />
          </div>
        </div>
        <p class="mt-3 text-sm">
          <span :style="{ fontFamily: form.typography.display }" class="text-2xl">{{ form.name }}</span>
          <SkeletonState v-if="brand.refreshing.value && !dirty" class="ml-3 inline-block h-5 w-48 align-middle" />
          <span v-else :style="{ fontFamily: form.typography.body }" class="ml-3 text-muted-foreground">{{
            form.descriptor.uz
          }}</span>
        </p>
      </section>

      <section class="panel">
        <div class="flex items-center justify-between">
          <h2 class="font-editorial text-2xl">Rasmiy havolalar</h2>
          <button
            class="btn-outline"
            @click="form.links.push({ key: 'link', label: '', handle: null, url: 'https://' })"
          >
            <Icon :icon="Plus" :size="14" />Havola
          </button>
        </div>
        <p class="mt-1 text-xs text-muted-foreground">
          Hisob nomlari aniq yozilishini saqlaydi (masalan, ocharostile, Ocharo_Style). Telefon uchun <code>tel:</code>.
        </p>
        <ul class="mt-4 space-y-2">
          <li
            v-for="(link, index) in form.links"
            :key="index"
            class="grid gap-2 md:grid-cols-[120px_160px_180px_1fr_auto]"
          >
            <input v-model="link.key" class="field font-mono text-xs" aria-label="Kalit" />
            <input v-model="link.label" class="field" aria-label="Nomi" />
            <input v-model="link.handle" class="field" placeholder="hisob nomi" aria-label="Hisob nomi" />
            <input v-model="link.url" class="field font-mono text-xs" aria-label="Manzil" />
            <button
              class="btn-ghost text-destructive"
              aria-label="Havolani o‘chirish"
              @click="form.links.splice(index, 1)"
            >
              <Icon :icon="Trash2" :size="14" />
            </button>
          </li>
        </ul>
      </section>
    </template>
    <IdentityFormSkeleton v-else-if="brand.queryState.value === 'loading'" />
    <div v-else class="brand-query-region text-sm text-muted-foreground">
      <p>
        {{
          brand.queryState.value === 'unavailable'
            ? 'Identitet ma’lumotlari hozir mavjud emas.'
            : 'Hali identitet ma’lumotlari yo‘q.'
        }}
      </p>
    </div>
  </section>
</template>
