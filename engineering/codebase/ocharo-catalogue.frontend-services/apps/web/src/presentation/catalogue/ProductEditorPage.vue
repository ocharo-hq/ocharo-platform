<script setup lang="ts">
import { requestConfirmation } from '../../application/common/UseFeedback';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { onBeforeRouteLeave, RouterLink, useRoute, useRouter } from 'vue-router';
import { ArrowDown, ArrowLeft, ArrowUp, History, ImagePlus, RotateCcw, Save, Trash2, Upload } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueStore, useProductEditor } from '../../application/catalogue';
import type { LocaleCode, MediaRole, MediaView } from '../../domain/catalogue/CatalogueModels';
import { localeLabels, locales, roleLabels, statusLabels } from '../../domain/catalogue/CatalogueRules';
import PriceTiersEditor from './components/PriceTiersEditor.vue';
import StatusBadge from './components/StatusBadge.vue';
import TagInput from './components/TagInput.vue';
import { formatWhen } from '../../domain/catalogue/UzDates';

/** One product: basics, three-language copy, pricing, images, review notes and history. */
defineOptions({ name: 'ProductEditorPage' });
const route = useRoute();
const router = useRouter();
const store = useCatalogueStore();
const editor = useProductEditor();
const activeLocale = ref<LocaleCode>('uz');
const uploadRole = ref<MediaRole>('cutout');
const fileInput = ref<HTMLInputElement | null>(null);
const summary = ref('');
const loaded = ref(false);
const roles = Object.keys(roleLabels) as MediaRole[];
const productId = computed(() => (typeof route.params.id === 'string' ? route.params.id : null));
const mediaById = computed(
  () => new Map((editor.detail.value?.media ?? []).map((m) => [m.media.id, m] as [string, MediaView])),
);
const detached = computed(() =>
  (editor.detail.value?.media ?? []).filter((m) => !editor.draft.value?.media?.some((x) => x.id === m.media.id)),
);
const openIssues = computed(() => editor.draft.value?.issues.filter((i) => i.status === 'open').length ?? 0);

onMounted(async () => {
  await store.loadSettings();
  if (productId.value) await editor.load(productId.value);
  else
    editor.startNew(
      typeof route.query.type === 'string' ? route.query.type : (store.garmentTypes.value[0]?.key ?? 'nimcha'),
    );
  loaded.value = true;
});
function warnUnsaved(event: BeforeUnloadEvent) {
  if (editor.dirty.value) event.preventDefault();
}
window.addEventListener('beforeunload', warnUnsaved);
onBeforeUnmount(() => window.removeEventListener('beforeunload', warnUnsaved));
onBeforeRouteLeave(async () => !editor.dirty.value || await requestConfirmation('Saqlanmagan o‘zgarishlar bor. Chiqib ketilsinmi?', { confirmLabel: 'O‘zgarishlarni bekor qilish' }));

async function save() {
  const wasNew = editor.isNew.value;
  const id = await editor.save(summary.value || undefined);
  if (id) summary.value = '';
  if (id && wasNew) await router.replace(`/products/${id}`);
}
function move(index: number, delta: number) {
  const list = editor.draft.value?.media;
  if (!list) return;
  const target = index + delta;
  if (target < 0 || target >= list.length) return;
  const [item] = list.splice(index, 1);
  if (item) list.splice(target, 0, item);
}
function reattach(view: MediaView) {
  editor.draft.value?.media?.push({ id: view.media.id, role: view.media.role, alt: view.media.alt });
}
function addIssue() {
  editor.draft.value?.issues.push({ id: '', field: 'general', status: 'open', question: '' });
}
async function onFiles(event: Event) {
  const files = (event.target as HTMLInputElement).files;
  if (files?.length) await editor.upload(files, uploadRole.value);
  if (fileInput.value) fileInput.value.value = '';
}
</script>

<template>
  <section v-if="editor.draft.value" class="pb-24">
    <RouterLink to="/" class="btn-ghost -ml-3 mb-3"><Icon :icon="ArrowLeft" :size="15" />Mahsulotlar</RouterLink>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <p class="text-xs uppercase tracking-[0.2em] text-muted-foreground">
          {{ store.garmentName(editor.draft.value.garmentType) }}
        </p>
        <h1 class="mt-1 font-editorial text-4xl tracking-tight">
          {{
            editor.draft.value.locales.uz.name ||
            (editor.isNew.value ? 'Yangi mahsulot' : editor.detail.value?.document.id)
          }}
        </h1>
        <p class="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span v-if="editor.detail.value"
            >{{ editor.detail.value.document.id }} · versiya {{ editor.detail.value.revision }}</span
          >
          <StatusBadge :status="editor.draft.value.status" />
          <span v-if="openIssues" class="chip bg-warning-soft text-warning-soft-foreground"
            >{{ openIssues }} ochiq savol</span
          >
          <span
            v-for="c in editor.detail.value?.collectionIds ?? []"
            :key="c"
            class="chip bg-secondary text-secondary-foreground"
            >{{ c }}</span
          >
        </p>
      </div>
    </div>

    <ul
      v-if="editor.problems.value.length"
      class="mt-5 space-y-1 rounded-xl border border-warning/30 bg-warning-soft px-5 py-3 text-sm text-warning-soft-foreground"
    >
      <li v-for="p in editor.problems.value" :key="p">{{ p }}</li>
    </ul>

    <div class="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(340px,1fr)]">
      <div class="space-y-6">
        <section class="panel" aria-labelledby="basics-heading">
          <h2 id="basics-heading" class="font-editorial text-2xl">Asosiy</h2>
          <div class="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label class="label" for="garment">Kiyim turi</label>
              <select
                id="garment"
                v-model="editor.draft.value.garmentType"
                class="field"
                :disabled="!editor.isNew.value"
              >
                <option v-for="g in store.garmentTypes.value" :key="g.key" :value="g.key">
                  {{ g.uz }} · {{ g.en }}
                </option>
              </select>
            </div>
            <div>
              <label class="label" for="status">Holat</label>
              <select id="status" v-model="editor.draft.value.status" class="field">
                <option v-for="(label, key) in statusLabels" :key="key" :value="key">{{ label }}</option>
              </select>
            </div>
            <div v-if="editor.isNew.value">
              <label class="label" for="variant">Variant kodi (bo‘sh — avtomatik)</label>
              <input id="variant" v-model="editor.draft.value.variantCode" class="field" placeholder="v17" />
            </div>
            <div>
              <label class="label" for="sort">Katalogdagi tartib</label>
              <input id="sort" v-model.number="editor.draft.value.sortOrder" type="number" class="field" />
            </div>
            <div>
              <label class="label" for="design">Dizayn oilasi</label>
              <input id="design" v-model="editor.draft.value.designId" class="field" placeholder="ixtiyoriy" />
            </div>
          </div>
          <div class="mt-4 grid gap-4 sm:grid-cols-2">
            <TagInput
              v-model="editor.draft.value.sizes"
              label="O‘lchamlar"
              :suggestions="store.settings.value?.sizePreset"
            />
            <TagInput v-model="editor.draft.value.tags" label="Teglar" placeholder="kuz, to‘y, yangi…" />
            <TagInput v-model="editor.draft.value.fabricCodes" label="Mato kodlari" />
            <TagInput v-model="editor.draft.value.decorationCodes" label="Bezak kodlari" />
          </div>
          <div class="mt-4">
            <label class="label" for="notes">Ichki izoh (chop etilmaydi)</label>
            <textarea id="notes" v-model="editor.draft.value.notes" rows="2" class="field" />
          </div>
        </section>

        <section class="panel" aria-labelledby="copy-heading">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <h2 id="copy-heading" class="font-editorial text-2xl">Matn</h2>
            <div role="tablist" class="flex rounded-lg bg-muted p-1 text-sm">
              <button
                v-for="code in locales"
                :key="code"
                role="tab"
                :aria-selected="activeLocale === code"
                :class="[
                  'rounded-md px-3 py-1.5',
                  activeLocale === code ? 'bg-card font-semibold shadow-sm' : 'text-muted-foreground',
                ]"
                @click="activeLocale = code"
              >
                {{ localeLabels[code] }}
                <span v-if="editor.draft.value.locales[code].status === 'approved'" class="ml-1 text-success">✓</span>
              </button>
            </div>
          </div>
          <div class="mt-4 grid gap-4" role="tabpanel">
            <div>
              <label class="label" :for="`name-${activeLocale}`">Nomi</label>
              <input
                :id="`name-${activeLocale}`"
                v-model="editor.draft.value.locales[activeLocale].name"
                class="field"
              />
            </div>
            <div class="grid gap-4 sm:grid-cols-2">
              <div>
                <label class="label" :for="`fabric-${activeLocale}`">Mato</label>
                <input
                  :id="`fabric-${activeLocale}`"
                  v-model="editor.draft.value.locales[activeLocale].fabric"
                  class="field"
                />
              </div>
              <div>
                <label class="label" :for="`decoration-${activeLocale}`">Bezak</label>
                <input
                  :id="`decoration-${activeLocale}`"
                  v-model="editor.draft.value.locales[activeLocale].decoration"
                  class="field"
                />
              </div>
            </div>
            <div>
              <label class="label" :for="`description-${activeLocale}`">Tavsif</label>
              <textarea
                :id="`description-${activeLocale}`"
                v-model="editor.draft.value.locales[activeLocale].description"
                rows="4"
                class="field"
              />
            </div>
            <label class="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                :checked="editor.draft.value.locales[activeLocale].status === 'approved'"
                @change="
                  editor.draft.value.locales[activeLocale].status = ($event.target as HTMLInputElement).checked
                    ? 'approved'
                    : 'draft'
                "
              />
              {{ localeLabels[activeLocale] }} matn tasdiqlangan
            </label>
          </div>
          <div v-if="editor.detail.value?.document.sourceVerbatim" class="mt-5 rounded-xl bg-muted p-4 text-sm">
            <p class="label">Asl yozuv (o‘zgarmaydi)</p>
            <p class="whitespace-pre-line">{{ editor.detail.value.document.sourceVerbatim }}</p>
          </div>
        </section>

        <section class="panel" aria-labelledby="price-heading">
          <h2 id="price-heading" class="mb-4 font-editorial text-2xl">Narx</h2>
          <PriceTiersEditor v-model="editor.draft.value.pricing" />
        </section>

        <section class="panel" aria-labelledby="issues-heading">
          <div class="flex items-center justify-between">
            <h2 id="issues-heading" class="font-editorial text-2xl">Tekshiruv savollari</h2>
            <button class="btn-outline" @click="addIssue">Savol qo‘shish</button>
          </div>
          <p v-if="!editor.draft.value.issues.length" class="mt-3 text-sm text-muted-foreground">Savol yo‘q.</p>
          <ul class="mt-4 space-y-3">
            <li
              v-for="(issue, index) in editor.draft.value.issues"
              :key="issue.id || index"
              class="rounded-xl border border-border p-3"
            >
              <div class="flex flex-wrap items-center gap-2">
                <input v-model="issue.field" class="field w-40" aria-label="Maydon" />
                <button
                  :class="[
                    'chip',
                    issue.status === 'open'
                      ? 'bg-warning-soft text-warning-soft-foreground'
                      : 'bg-success-soft text-success-soft-foreground',
                  ]"
                  @click="issue.status = issue.status === 'open' ? 'resolved' : 'open'"
                >
                  {{ issue.status === 'open' ? 'Ochiq' : 'Hal qilingan' }}
                </button>
                <button
                  class="btn-ghost ml-auto"
                  aria-label="Savolni o‘chirish"
                  @click="editor.draft.value.issues.splice(index, 1)"
                >
                  <Icon :icon="Trash2" :size="14" />
                </button>
              </div>
              <textarea v-model="issue.question" rows="2" class="field mt-2" aria-label="Savol matni" />
            </li>
          </ul>
        </section>
      </div>

      <div class="space-y-6">
        <section class="panel" aria-labelledby="images-heading">
          <div class="flex items-center justify-between gap-3">
            <h2 id="images-heading" class="font-editorial text-2xl">Rasmlar</h2>
            <div class="flex items-center gap-2">
              <select v-model="uploadRole" class="field w-32" aria-label="Rasm turi">
                <option v-for="r in roles" :key="r" :value="r">{{ roleLabels[r] }}</option>
              </select>
              <button
                class="btn-outline"
                :disabled="!editor.detail.value || editor.uploading.value"
                @click="fileInput?.click()"
              >
                <Icon :icon="editor.uploading.value ? Upload : ImagePlus" :size="15" />{{
                  editor.uploading.value ? 'Yuklanmoqda…' : 'Qo‘shish'
                }}
              </button>
              <input
                ref="fileInput"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                multiple
                class="hidden"
                @change="onFiles"
              />
            </div>
          </div>
          <p v-if="!editor.detail.value" class="mt-3 text-sm text-muted-foreground">
            Rasm qo‘shish uchun avval saqlang.
          </p>
          <ul class="mt-4 grid grid-cols-2 gap-3">
            <li
              v-for="(item, index) in editor.draft.value.media ?? []"
              :key="item.id"
              class="rounded-xl border border-border bg-background p-2"
            >
              <div
                class="flex aspect-[3/4] items-center justify-center rounded-lg bg-[radial-gradient(circle_at_50%_35%,#fffdf8,#ece7dc)]"
              >
                <img
                  v-if="mediaById.get(item.id)"
                  :src="mediaById.get(item.id)!.previewUrl ?? mediaById.get(item.id)!.url"
                  :alt="item.alt ?? item.role"
                  class="h-full w-full object-contain p-2"
                  loading="lazy"
                />
              </div>
              <div class="mt-2 flex items-center gap-1">
                <select v-model="item.role" class="field px-2 py-1 text-xs" aria-label="Rasm turi">
                  <option v-for="r in roles" :key="r" :value="r">{{ roleLabels[r] }}</option>
                </select>
                <button class="btn-ghost px-1.5" aria-label="Oldinga" :disabled="index === 0" @click="move(index, -1)">
                  <Icon :icon="ArrowUp" :size="13" />
                </button>
                <button
                  class="btn-ghost px-1.5"
                  aria-label="Orqaga"
                  :disabled="index === (editor.draft.value.media?.length ?? 0) - 1"
                  @click="move(index, 1)"
                >
                  <Icon :icon="ArrowDown" :size="13" />
                </button>
                <button
                  class="btn-ghost px-1.5 text-destructive"
                  aria-label="Rasmni olib tashlash"
                  @click="editor.draft.value.media?.splice(index, 1)"
                >
                  <Icon :icon="Trash2" :size="13" />
                </button>
              </div>
              <input
                v-model="item.alt"
                class="field mt-1 px-2 py-1 text-xs"
                placeholder="Alt matn"
                aria-label="Alt matn"
              />
              <p v-if="index === 0" class="mt-1 text-[10px] font-semibold uppercase tracking-wider text-primary">
                Asosiy rasm
              </p>
            </li>
          </ul>
          <div
            v-if="detached.length"
            class="mt-4 rounded-xl border border-dashed border-border p-3 text-xs text-muted-foreground"
          >
            <p>Olib tashlangan (saqlasangiz ro‘yxatdan chiqadi; fayl joyida qoladi):</p>
            <div class="mt-2 flex flex-wrap gap-2">
              <button v-for="view in detached" :key="view.media.id" class="chip bg-muted" @click="reattach(view)">
                ↺ {{ view.media.fileName }}
              </button>
            </div>
          </div>
        </section>

        <section v-if="editor.history.value.length" class="panel" aria-labelledby="history-heading">
          <h2 id="history-heading" class="flex items-center gap-2 font-editorial text-2xl">
            <Icon :icon="History" :size="18" />Tarix
          </h2>
          <ol class="mt-4 max-h-96 space-y-2 overflow-y-auto pr-1 text-sm">
            <li
              v-for="entry in editor.history.value"
              :key="entry.revision"
              class="flex items-start justify-between gap-3 rounded-lg border border-border px-3 py-2"
            >
              <div>
                <p class="font-semibold">v{{ entry.revision }} · {{ entry.summary }}</p>
                <p class="text-xs text-muted-foreground">{{ formatWhen(entry.savedAt) }}</p>
              </div>
              <button
                v-if="entry.revision !== editor.detail.value?.revision"
                class="btn-ghost shrink-0 px-2 text-xs"
                :disabled="editor.dirty.value"
                :title="editor.dirty.value ? 'Avval saqlang yoki bekor qiling' : 'Shu versiyani tiklash'"
                @click="editor.restore(entry.revision)"
              >
                <Icon :icon="RotateCcw" :size="13" />Tiklash
              </button>
            </li>
          </ol>
        </section>

        <section v-if="editor.detail.value?.document.source" class="panel text-xs text-muted-foreground">
          <p class="label">Manba</p>
          <p class="break-all">{{ editor.detail.value.document.source.path }}</p>
          <p class="mt-1 break-all font-mono">sha256 {{ editor.detail.value.document.source.sha256 }}</p>
        </section>
      </div>
    </div>

    <div class="no-print fixed inset-x-0 bottom-0 z-30 border-t border-border bg-card/95 backdrop-blur lg:left-60">
      <div class="mx-auto flex max-w-[1600px] flex-wrap items-center gap-3 px-5 py-3 sm:px-8 xl:px-10">
        <span :class="['text-sm', editor.dirty.value ? 'font-semibold text-warning' : 'text-muted-foreground']">
          {{ editor.dirty.value ? 'Saqlanmagan o‘zgarishlar' : 'Hammasi saqlangan' }}
        </span>
        <input
          v-model="summary"
          class="field max-w-xs flex-1"
          placeholder="O‘zgarish izohi (ixtiyoriy)"
          aria-label="O‘zgarish izohi"
        />
        <button class="btn-ghost ml-auto" :disabled="!editor.dirty.value || editor.isNew.value" @click="editor.reset()">
          Bekor qilish
        </button>
        <button
          class="btn-primary"
          :disabled="(!editor.dirty.value && !editor.isNew.value) || editor.saving.value"
          @click="save"
        >
          <Icon :icon="Save" :size="15" />{{ editor.saving.value ? 'Saqlanmoqda…' : 'Saqlash' }}
        </button>
      </div>
    </div>
  </section>
  <p v-else-if="loaded" class="panel p-10 text-center text-sm text-muted-foreground">Mahsulot topilmadi.</p>
  <p v-else class="text-sm text-muted-foreground" role="status">Yuklanmoqda…</p>
</template>
