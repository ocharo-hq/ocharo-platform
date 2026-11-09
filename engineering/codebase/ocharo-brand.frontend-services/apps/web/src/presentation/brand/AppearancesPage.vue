<script setup lang="ts">
import { computed, ref } from 'vue';
import { Temporal } from 'temporal-polyfill';
import { CalendarPlus, Upload, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card } from '@wow-two-beta/fe/presentation/display';
import {
  ColorInput,
  ColorPicker,
  DateTimeInput,
  Field,
  SelectPicker,
  SelectPickerContent,
  SelectPickerItem,
  SelectPickerTrigger,
  TextInput,
} from '@wow-two-beta/fe/presentation/forms';
import { useBrand } from '../../application/brand';
import { useIdentitySession } from '../../application/common/UseIdentitySession';
import { slotLabels, slots } from '../../domain/brand/BrandModels';
import { formatWhen } from '../../domain/brand/UzDates';

/** Temporary event looks: during the window, chosen logos (and an accent colour) replace the released ones. */
defineOptions({ name: 'AppearancesPage' });
const brand = useBrand();
const { canEdit } = useIdentitySession();
const form = ref<{
  id: string | null;
  name: string;
  /** Local wall-clock times, `YYYY-MM-DDTHH:mm`; empty while cleared. */
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
/** The date-time field's wall-clock value for a stored local time. */
function wallClock(value: string): Temporal.PlainDateTime | null {
  return value ? Temporal.PlainDateTime.from(value) : null;
}
/** The stored local time for the date-time field's value. */
function fromWallClock(value: Temporal.PlainDateTime | null): string {
  return value ? value.toString({ smallestUnit: 'minute' }) : '';
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
const canSave = computed(
  () =>
    canEdit.value && !brand.busy.value && Boolean(form.value?.name.trim() && form.value.startsAt && form.value.endsAt),
);
async function save() {
  if (!form.value || !canSave.value) return;
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
  if (a.status === 'cancelled') return { label: 'bekor qilingan', variant: 'neutral' } as const;
  if (Date.parse(a.endsAt) <= now) return { label: 'tugagan', variant: 'neutral' } as const;
  if (Date.parse(a.startsAt) <= now) return { label: 'faol', variant: 'success' } as const;
  return { label: 'rejalashtirilgan', variant: 'brand' } as const;
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
      <Button tone="primary" :is-disabled="!canEdit" @click="startNew">
        <template #leading><Icon :icon="CalendarPlus" :size="15" /></template>
        Yangi tadbir
      </Button>
    </div>

    <Card
      v-if="form"
      v-bind="{ role: 'region', 'aria-labelledby': 'appearance-form-heading' }"
      radius="2xl"
      padding="xl"
    >
      <div class="flex items-start justify-between">
        <h2 id="appearance-form-heading" class="font-editorial text-2xl">{{ form.id ? form.name : 'Yangi tadbir' }}</h2>
        <Button variant="ghost" tone="neutral" shape="square" aria-label="Yopish" @click="form = null">
          <Icon :icon="X" :size="16" />
        </Button>
      </div>
      <div class="mt-4 grid gap-4 md:grid-cols-4">
        <Field label="Nomi" class="md:col-span-2"><TextInput v-model="form.name" name="name" /></Field>
        <Field label="Boshlanishi">
          <DateTimeInput
            :model-value="wallClock(form.startsAt)"
            @update:model-value="form.startsAt = fromWallClock($event)"
          />
        </Field>
        <Field label="Tugashi">
          <DateTimeInput
            :model-value="wallClock(form.endsAt)"
            @update:model-value="form.endsAt = fromWallClock($event)"
          />
        </Field>
      </div>
      <h3 class="mt-5 text-sm font-semibold">Logotip almashtirishlari</h3>
      <div class="mt-2 grid gap-3 md:grid-cols-3">
        <Field v-for="slot in slots" :key="slot" :label="slotLabels[slot]!.label">
          <SelectPicker
            :model-value="form.overrides[slot] ?? ''"
            :options="[
              { itemKey: '', value: '', label: '— asosiy nashrdagisi' },
              ...brand
                .versionsFor(slot)
                .map((version) => ({ itemKey: version.id, value: version.id, label: version.fileName })),
            ]"
            @update:model-value="form.overrides[slot] = $event ?? ''"
          >
            <SelectPickerTrigger class="w-full" />
            <SelectPickerContent variant="glass-outline" class="z-[100] max-h-80 overflow-auto">
              <SelectPickerItem item-key="" label="— asosiy nashrdagisi" />
              <SelectPickerItem
                v-for="version in brand.versionsFor(slot)"
                :key="version.id"
                :item-key="version.id"
                :label="version.fileName"
              />
            </SelectPickerContent>
          </SelectPicker>
        </Field>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-2 text-sm">
        <span class="text-muted-foreground">Tadbir uchun fayl yuklash:</span>
        <SelectPicker
          :model-value="uploadSlot"
          :options="slots.map((slot) => ({ itemKey: slot, value: slot, label: slotLabels[slot]!.label }))"
          @update:model-value="uploadSlot = $event ?? 'on-light'"
        >
          <SelectPickerTrigger class="w-44" v-bind="{ 'aria-label': 'O‘rin' }" />
          <SelectPickerContent variant="glass-outline" class="z-[100] max-h-80 overflow-auto">
            <SelectPickerItem v-for="slot in slots" :key="slot" :item-key="slot" :label="slotLabels[slot]!.label" />
          </SelectPickerContent>
        </SelectPicker>
        <Button variant="outline" tone="neutral" @click="fileInput?.click()">
          <template #leading><Icon :icon="Upload" :size="14" /></template>
          Fayl
        </Button>
        <input ref="fileInput" type="file" accept="image/png,image/jpeg,image/webp" class="hidden" @change="onFile" />
      </div>
      <div class="mt-4 grid gap-4 md:grid-cols-[200px_1fr]">
        <Field label="Urg‘u rangi (ixtiyoriy)">
          <div class="flex gap-2">
            <ColorPicker
              :model-value="form.accentHex || null"
              trigger-variant="swatch"
              v-bind="{ 'aria-label': 'Rang tanlash' }"
              @update:model-value="form.accentHex = $event ?? ''"
            />
            <ColorInput
              :model-value="form.accentHex || null"
              class="font-mono text-xs"
              placeholder="#1f7a4d"
              @update:model-value="form.accentHex = $event ?? ''"
            />
          </div>
        </Field>
        <Field label="Izoh"><TextInput v-model="form.note" /></Field>
      </div>
      <p class="mt-4 text-xs text-muted-foreground">
        Tadbir havolasi (ixtiyoriy): saytda logotip yonida belgi bo‘lib chiqadi; logotipning o‘zi bosh sahifaga olib
        boradi.
      </p>
      <div class="mt-2 grid gap-4 md:grid-cols-[240px_1fr]">
        <Field label="Belgi matni">
          <TextInput v-model="form.linkLabel" maxlength="60" placeholder="Yangi to‘plam" />
        </Field>
        <Field label="Havola (https://)">
          <TextInput v-model="form.linkUrl" class="font-mono text-xs" placeholder="https://" />
        </Field>
      </div>
      <div class="mt-5 flex justify-end">
        <Button tone="primary" :is-disabled="!canSave" @click="save">Saqlash</Button>
      </div>
    </Card>

    <ul class="space-y-3">
      <li v-for="a in list" :key="a.id">
        <Card radius="2xl" padding="xl" class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p class="font-editorial text-xl">
              {{ a.name }}
              <Badge :variant="state(a).variant" size="sm" class="ml-2 align-middle">{{ state(a).label }}</Badge>
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
            <Button variant="outline" tone="neutral" @click="edit(a.id)">Tahrirlash</Button>
            <Button
              variant="ghost"
              tone="danger"
              :is-disabled="!canEdit || brand.busy.value"
              @click="brand.cancelAppearance(a.id)"
            >
              Bekor qilish
            </Button>
          </div>
        </Card>
      </li>
      <li v-if="!list.length">
        <Card radius="2xl" padding="2xl" class="text-center text-sm text-muted-foreground">Hali tadbir yo‘q.</Card>
      </li>
    </ul>
  </section>
</template>
