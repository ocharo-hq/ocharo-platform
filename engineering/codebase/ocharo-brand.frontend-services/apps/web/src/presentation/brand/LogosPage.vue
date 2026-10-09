<script setup lang="ts">
import { useSsoSession } from '../../application/common/UseSsoSession';
const { canEdit: ssoCanEdit } = useSsoSession();
import { ref } from 'vue';
import { Check, Upload } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useBrand } from '../../application/brand';
import { slotLabels, slots } from '../../domain/brand/BrandModels';
import LogoSwatch from './LogoSwatch.vue';

/** Every logo slot: its current (draft) file, all uploaded versions, and uploads with a note. */
defineOptions({ name: 'LogosPage' });
const brand = useBrand();
const notes = ref<Record<string, string>>({});
const inputs = ref<Record<string, HTMLInputElement | null>>({});
async function onFile(slot: string, event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  const version = await brand.upload(slot, file, notes.value[slot]?.trim() || null);
  if (version) notes.value[slot] = '';
  const input = inputs.value[slot];
  if (input) input.value = '';
}
function size(bytes: number) {
  return bytes > 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
}
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="font-editorial text-4xl tracking-tight">Logotiplar</h1>
      <p class="mt-2 max-w-3xl text-sm text-muted-foreground">
        Har bir o‘ringa yangi faylni yuklang va “Joriy qilish”ni bosing. O‘zgarish qoralamada turadi; Umumiy sahifada
        “Nashr qilish” bosilganda barcha ilovalar yangi logotipni oladi. Faqat PNG, JPEG, WebP.
      </p>
    </div>
    <section v-for="slot in slots" :key="slot" class="panel" :aria-labelledby="`slot-${slot}`">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 :id="`slot-${slot}`" class="font-editorial text-2xl">{{ slotLabels[slot]!.label }}</h2>
          <p class="text-sm text-muted-foreground">
            {{ slotLabels[slot]!.hint }} · <code class="text-xs">{{ brand.urls.logo(slot) }}</code>
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <input
            v-model="notes[slot]"
            class="field w-56"
            placeholder="Izoh (ixtiyoriy)"
            :aria-label="`${slotLabels[slot]!.label} izohi`"
          />
          <button class="btn-outline" :disabled="!ssoCanEdit || brand.busy.value" @click="inputs[slot]?.click()">
            <Icon :icon="Upload" :size="14" />Yuklash
          </button>
          <input
            :ref="(el) => (inputs[slot] = el as HTMLInputElement | null)"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            class="hidden"
            @change="onFile(slot, $event)"
          />
        </div>
      </div>
      <div v-if="brand.currentVersion(slot)" class="mt-4">
        <LogoSwatch
          :src="brand.urls.media(brand.currentVersion(slot)!.sha256, brand.currentVersion(slot)!.hasPreview)"
          :alt="slotLabels[slot]!.label"
        />
      </div>
      <p v-else class="mt-4 rounded-xl border border-dashed border-border p-4 text-sm text-muted-foreground">
        Bu o‘ringa fayl tanlanmagan{{
          slot === 'on-light' || slot === 'social'
            ? ' — asosiy logotip ishlatiladi'
            : slot === 'favicon'
              ? ' — profil rasmi yoki och fon logotipi ishlatiladi'
              : ''
        }}.
      </p>
      <ul v-if="brand.versionsFor(slot).length" class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <li
          v-for="v in brand.versionsFor(slot)"
          :key="v.id"
          :class="[
            'flex gap-3 rounded-xl border p-3',
            brand.draft.value?.draft.currentLogos[slot] === v.id
              ? 'border-primary bg-primary-soft/30'
              : 'border-border',
          ]"
        >
          <img
            :src="brand.urls.media(v.sha256, v.hasPreview)"
            alt=""
            class="h-16 w-20 shrink-0 rounded bg-[#f8f7f3] object-contain p-1"
            loading="lazy"
          />
          <div class="min-w-0 flex-1 text-xs">
            <p class="truncate font-semibold">{{ v.fileName }}</p>
            <p class="text-muted-foreground">{{ v.width }}×{{ v.height }} · {{ size(v.byteLength) }}</p>
            <p v-if="v.note" class="mt-1 line-clamp-2 text-muted-foreground">{{ v.note }}</p>
            <div class="mt-2 flex flex-wrap gap-1">
              <span
                :class="[
                  'chip',
                  v.status === 'approved'
                    ? 'bg-success-soft text-success-soft-foreground'
                    : 'bg-muted text-muted-foreground',
                ]"
              >
                {{ v.status === 'approved' ? 'tasdiqlangan' : 'qoralama' }}
              </span>
              <button
                v-if="brand.draft.value?.draft.currentLogos[slot] !== v.id"
                class="chip bg-primary text-primary-foreground"
                :disabled="!ssoCanEdit || brand.busy.value"
                @click="brand.setCurrent(slot, v.id)"
              >
                Joriy qilish
              </button>
              <span v-else class="chip bg-primary-soft text-primary"><Icon :icon="Check" :size="11" />joriy</span>
              <button
                class="chip bg-secondary text-secondary-foreground"
                :disabled="!ssoCanEdit || brand.busy.value"
                @click="brand.setStatus(v.id, v.status === 'approved' ? 'draft' : 'approved')"
              >
                {{ v.status === 'approved' ? 'Tasdiqni olish' : 'Tasdiqlash' }}
              </button>
            </div>
          </div>
        </li>
      </ul>
    </section>
  </section>
</template>
