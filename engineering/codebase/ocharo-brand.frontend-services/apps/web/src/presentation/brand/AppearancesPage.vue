<script setup lang="ts">
import { computed, ref } from 'vue';
import { CalendarPlus, Upload, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useBrand } from '../../application/brand';
import { slotLabels, slots } from '../../domain/brand/BrandModels';
import { formatWhen } from '../../domain/brand/UzDates';

/** Temporary event looks: during the window, chosen logos (and an accent colour) replace the released ones. */
defineOptions({ name: 'AppearancesPage' });
const brand = useBrand();
const form = ref<{
  id: string | null;
  name: string;
  startsAt: string;
  endsAt: string;
  overrides: Record<string, string>;
  accentHex: string;
  note: string;
  linkLabel: string;
  linkUrl: string;
} | null>(null);
const uploadSlot = ref('on-light');
const fileInput = ref<HTMLInputElement | null>(null);
const now = Date.now();
const list = computed(() => brand.appearances.value);
function local(value: Date) {
  const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60000);
  return shifted.toISOString().slice(0, 16);
}
function startNew() {
  const start = new Date(Date.now() + 24 * 3600 * 1000);
  start.setHours(0, 0, 0, 0);
  form.value = {
    id: null,
    name: '',
    startsAt: local(start),
    endsAt: local(new Date(start.getTime() + 3 * 24 * 3600 * 1000)),
    overrides: {},
    accentHex: '',
    note: '',
    linkLabel: '',
    linkUrl: '',
  };
}
function edit(id: string) {
  const a = brand.appearances.value.find((x) => x.id === id);
  if (!a) return;
  form.value = {
    id: a.id,
    name: a.name,
    startsAt: local(new Date(a.startsAt)),
    endsAt: local(new Date(a.endsAt)),
    overrides: { ...a.overrides },
    accentHex: a.accentHex ?? '',
    note: a.note ?? '',
    linkLabel: a.linkLabel ?? '',
    linkUrl: a.linkUrl ?? '',
  };
}
async function save() {
  if (!form.value) return;
  const saved = await brand.saveAppearance({
    id: form.value.id,
    name: form.value.name,
    startsAt: new Date(form.value.startsAt).toISOString(),
    endsAt: new Date(form.value.endsAt).toISOString(),
    overrides: Object.fromEntries(Object.entries(form.value.overrides).filter(([, v]) => v)),
    accentHex: form.value.accentHex || null,
    note: form.value.note || null,
    linkLabel: form.value.linkLabel.trim() || null,
    linkUrl: form.value.linkUrl.trim() || null,
  });
  if (saved) form.value = null;
}
async function onFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file || !form.value) return;
  const version = await brand.upload(uploadSlot.value, file, `Tadbir: ${form.value.name || 'nomsiz'}`);
  if (version && form.value) form.value.overrides[uploadSlot.value] = version.id;
  if (fileInput.value) fileInput.value.value = '';
}
function state(a: { status: string; startsAt: string; endsAt: string }) {
  if (a.status === 'cancelled') return { label: 'bekor qilingan', tone: 'bg-muted text-muted-foreground' };
  if (Date.parse(a.endsAt) <= now) return { label: 'tugagan', tone: 'bg-muted text-muted-foreground' };
  if (Date.parse(a.startsAt) <= now) return { label: 'faol', tone: 'bg-success-soft text-success-soft-foreground' };
  return { label: 'rejalashtirilgan', tone: 'bg-primary-soft text-primary-soft-foreground' };
}
</script>

<template>
  <section class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">Tadbir ko‘rinishlari</h1>
        <p class="mt-2 max-w-3xl text-sm text-muted-foreground">
          Navro‘z, Ramazon yoki kolleksiya haftasi uchun vaqtinchalik logotip. Belgilangan vaqtda barcha ilovalardagi
          barqaror logotip manzillari shu faylni ko‘rsatadi, keyin avtomatik qaytadi. Asosiy yozuv (wordmark) qayta
          chizilmaydi.
        </p>
      </div>
      <button class="btn-primary" @click="startNew"><Icon :icon="CalendarPlus" :size="15" />Yangi tadbir</button>
    </div>

    <section v-if="form" class="panel">
      <div class="flex items-start justify-between">
        <h2 class="font-editorial text-2xl">{{ form.id ? form.name : 'Yangi tadbir' }}</h2>
        <button class="btn-ghost" aria-label="Yopish" @click="form = null"><Icon :icon="X" :size="16" /></button>
      </div>
      <div class="mt-4 grid gap-4 md:grid-cols-4">
        <div class="md:col-span-2">
          <label class="label" for="a-name">Nomi</label><input id="a-name" v-model="form.name" class="field" />
        </div>
        <div>
          <label class="label" for="a-start">Boshlanishi</label
          ><input id="a-start" v-model="form.startsAt" type="datetime-local" class="field" />
        </div>
        <div>
          <label class="label" for="a-end">Tugashi</label
          ><input id="a-end" v-model="form.endsAt" type="datetime-local" class="field" />
        </div>
      </div>
      <h3 class="mt-5 text-sm font-semibold">Logotip almashtirishlari</h3>
      <div class="mt-2 grid gap-3 md:grid-cols-3">
        <div v-for="slot in slots" :key="slot">
          <label class="label" :for="`o-${slot}`">{{ slotLabels[slot]!.label }}</label>
          <select :id="`o-${slot}`" v-model="form.overrides[slot]" class="field">
            <option value="">— asosiy nashrdagisi</option>
            <option v-for="v in brand.versionsFor(slot)" :key="v.id" :value="v.id">{{ v.fileName }}</option>
          </select>
        </div>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-2 text-sm">
        <span class="text-muted-foreground">Tadbir uchun fayl yuklash:</span>
        <select v-model="uploadSlot" class="field w-44" aria-label="O‘rin">
          <option v-for="slot in slots" :key="slot" :value="slot">{{ slotLabels[slot]!.label }}</option>
        </select>
        <button class="btn-outline" @click="fileInput?.click()"><Icon :icon="Upload" :size="14" />Fayl</button>
        <input ref="fileInput" type="file" accept="image/png,image/jpeg,image/webp" class="hidden" @change="onFile" />
      </div>
      <div class="mt-4 grid gap-4 md:grid-cols-[200px_1fr]">
        <div>
          <label class="label" for="a-accent">Urg‘u rangi (ixtiyoriy)</label>
          <div class="flex gap-2">
            <input
              v-model="form.accentHex"
              type="color"
              class="h-10 w-12 rounded border border-border"
              aria-label="Rang tanlash"
            />
            <input id="a-accent" v-model="form.accentHex" class="field font-mono text-xs" placeholder="#1f7a4d" />
          </div>
        </div>
        <div><label class="label" for="a-note">Izoh</label><input id="a-note" v-model="form.note" class="field" /></div>
      </div>
      <p class="mt-4 text-xs text-muted-foreground">
        Tadbir havolasi (ixtiyoriy): saytda logotip yonida belgi bo‘lib chiqadi; logotipning o‘zi bosh sahifaga olib
        boradi.
      </p>
      <div class="mt-2 grid gap-4 md:grid-cols-[240px_1fr]">
        <div>
          <label class="label" for="a-link-label">Belgi matni</label
          ><input id="a-link-label" v-model="form.linkLabel" class="field" maxlength="60" placeholder="Yangi to‘plam" />
        </div>
        <div>
          <label class="label" for="a-link-url">Havola (https://)</label
          ><input id="a-link-url" v-model="form.linkUrl" class="field font-mono text-xs" placeholder="https://" />
        </div>
      </div>
      <div class="mt-5 flex justify-end">
        <button class="btn-primary" :disabled="brand.busy.value || !form.name.trim()" @click="save">Saqlash</button>
      </div>
    </section>

    <ul class="space-y-3">
      <li v-for="a in list" :key="a.id" class="panel flex flex-wrap items-center justify-between gap-3">
        <div>
          <p class="font-editorial text-xl">
            {{ a.name }} <span :class="['chip ml-2 align-middle', state(a).tone]">{{ state(a).label }}</span>
          </p>
          <p class="text-sm text-muted-foreground">
            {{ formatWhen(a.startsAt) }} — {{ formatWhen(a.endsAt) }} ·
            {{
              Object.keys(a.overrides)
                .map((s) => slotLabels[s]?.label ?? s)
                .join(', ') || (a.accentHex ? 'faqat rang' : 'faqat havola')
            }}<template v-if="a.linkUrl"> · belgi: {{ a.linkLabel }}</template>
          </p>
        </div>
        <div v-if="a.status === 'scheduled'" class="flex gap-2">
          <button class="btn-outline" @click="edit(a.id)">Tahrirlash</button>
          <button class="btn-ghost text-destructive" :disabled="brand.busy.value" @click="brand.cancelAppearance(a.id)">
            Bekor qilish
          </button>
        </div>
      </li>
      <li v-if="!list.length" class="panel p-8 text-center text-sm text-muted-foreground">Hali tadbir yo‘q.</li>
    </ul>
  </section>
</template>
