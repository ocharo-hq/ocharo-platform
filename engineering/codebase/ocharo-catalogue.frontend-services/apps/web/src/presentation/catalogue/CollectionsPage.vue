<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCollections, useProductList, type CollectionDraft } from '../../application/catalogue';

/** Ordered product selections: seasons, lookbooks, merchant sheets. */
defineOptions({ name: 'CollectionsPage' });
const collections = useCollections();
const list = useProductList();
const draft = ref<CollectionDraft | null>(null);
const pick = ref('');
const byId = computed(() => new Map(list.products.value.map((p) => [p.id, p])));
const available = computed(() =>
  list.products.value.filter((p) => !draft.value?.productIds.includes(p.id) && p.status !== 'archived'),
);
function add() {
  if (draft.value && pick.value) draft.value.productIds.push(pick.value);
  pick.value = '';
}
function move(index: number, delta: number) {
  const ids = draft.value?.productIds;
  if (!ids) return;
  const target = index + delta;
  if (target < 0 || target >= ids.length) return;
  const [id] = ids.splice(index, 1);
  if (id) ids.splice(target, 0, id);
}
async function save() {
  if (!draft.value) return;
  const saved = await collections.save(draft.value);
  if (saved) draft.value = collections.edit(saved);
}
async function remove(id: string) {
  if (!window.confirm('To‘plam o‘chirilsinmi? Mahsulotlar o‘chmaydi.')) return;
  await collections.remove(id);
  draft.value = null;
}
onMounted(async () => {
  await Promise.all([collections.load(), list.load()]);
});
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">To‘plamlar</h1>
        <p class="mt-2 text-sm text-muted-foreground">
          Mavsum, lookbook yoki do‘konlar uchun tanlovlar. Chop etilganlari Marketing va Blogda ko‘rinadi.
        </p>
      </div>
      <button class="btn-primary" @click="draft = collections.blank()">
        <Icon :icon="Plus" :size="15" />Yangi to‘plam
      </button>
    </div>
    <div class="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
      <ul class="space-y-2">
        <li v-for="c in collections.collections.value" :key="c.document.id">
          <button
            :class="[
              'w-full rounded-xl border p-4 text-left transition',
              draft?.id === c.document.id
                ? 'border-primary bg-primary-soft/50'
                : 'border-border bg-card hover:bg-muted',
            ]"
            @click="draft = collections.edit(c)"
          >
            <p class="font-editorial text-lg">{{ c.document.names.uz }}</p>
            <p class="mt-1 text-xs text-muted-foreground">
              {{ c.document.productIds.length }} ta mahsulot · {{ c.document.isPublished ? 'chop etilgan' : 'ichki' }}
            </p>
          </button>
        </li>
        <li
          v-if="!collections.collections.value.length"
          class="rounded-xl border border-dashed border-border p-6 text-sm text-muted-foreground"
        >
          Hali to‘plam yo‘q.
        </li>
      </ul>

      <div v-if="draft" class="panel">
        <div class="flex items-start justify-between">
          <h2 class="font-editorial text-2xl">
            {{ draft.expectedRevision === null ? 'Yangi to‘plam' : draft.names.uz }}
          </h2>
          <button class="btn-ghost" aria-label="Yopish" @click="draft = null"><Icon :icon="X" :size="16" /></button>
        </div>
        <div class="mt-4 grid gap-4 sm:grid-cols-3">
          <div>
            <label class="label" for="c-uz">Nomi (uz)</label><input id="c-uz" v-model="draft.names.uz" class="field" />
          </div>
          <div>
            <label class="label" for="c-ru">Nomi (ru)</label><input id="c-ru" v-model="draft.names.ru" class="field" />
          </div>
          <div>
            <label class="label" for="c-en">Nomi (en)</label><input id="c-en" v-model="draft.names.en" class="field" />
          </div>
        </div>
        <div class="mt-4 grid gap-4 sm:grid-cols-[1fr_140px]">
          <div>
            <label class="label" for="c-desc">Tavsif</label
            ><textarea id="c-desc" v-model="draft.description" rows="2" class="field" />
          </div>
          <div>
            <label class="label" for="c-order">Tartib</label
            ><input id="c-order" v-model.number="draft.sortOrder" type="number" class="field" />
          </div>
        </div>
        <label class="mt-4 flex items-center gap-2 text-sm"
          ><input v-model="draft.isPublished" type="checkbox" />Ommaviy katalog lentasida ko‘rsatish</label
        >

        <h3 class="mt-6 text-sm font-semibold">Mahsulotlar tartibi</h3>
        <div class="mt-2 flex gap-2">
          <select v-model="pick" class="field" aria-label="Mahsulot qo‘shish">
            <option value="">Mahsulot tanlang…</option>
            <option v-for="p in available" :key="p.id" :value="p.id">{{ p.nameUz ?? p.id }} — {{ p.id }}</option>
          </select>
          <button class="btn-outline" :disabled="!pick" @click="add">Qo‘shish</button>
        </div>
        <ol class="mt-3 space-y-2">
          <li
            v-for="(id, index) in draft.productIds"
            :key="id"
            class="flex items-center gap-3 rounded-lg border border-border px-3 py-2"
          >
            <span class="w-6 text-xs text-muted-foreground">{{ index + 1 }}</span>
            <img
              v-if="byId.get(id)?.primaryPreviewUrl"
              :src="byId.get(id)!.primaryPreviewUrl!"
              alt=""
              class="h-10 w-8 object-contain"
            />
            <span class="flex-1 text-sm"
              >{{ byId.get(id)?.nameUz ?? id }} <span class="text-xs text-muted-foreground">{{ id }}</span></span
            >
            <button class="btn-ghost px-1.5" aria-label="Yuqoriga" @click="move(index, -1)">
              <Icon :icon="ArrowUp" :size="13" />
            </button>
            <button class="btn-ghost px-1.5" aria-label="Pastga" @click="move(index, 1)">
              <Icon :icon="ArrowDown" :size="13" />
            </button>
            <button
              class="btn-ghost px-1.5 text-destructive"
              aria-label="Olib tashlash"
              @click="draft.productIds.splice(index, 1)"
            >
              <Icon :icon="X" :size="13" />
            </button>
          </li>
        </ol>
        <div class="mt-6 flex items-center gap-3">
          <button class="btn-primary" :disabled="collections.busy.value || !draft.names.uz.trim()" @click="save">
            Saqlash
          </button>
          <button
            v-if="draft.expectedRevision !== null && draft.id"
            class="btn-ghost ml-auto text-destructive"
            @click="remove(draft.id)"
          >
            <Icon :icon="Trash2" :size="14" />O‘chirish
          </button>
        </div>
      </div>
      <p v-else class="panel flex items-center justify-center p-10 text-sm text-muted-foreground">
        Tahrirlash uchun to‘plamni tanlang.
      </p>
    </div>
  </section>
</template>
