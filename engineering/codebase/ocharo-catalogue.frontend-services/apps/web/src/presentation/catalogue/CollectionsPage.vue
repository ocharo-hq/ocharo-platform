<script setup lang="ts">
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/ui-vue/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
import { requestConfirmation } from '../../application/common/UseFeedback';
import { computed, onMounted, ref } from 'vue';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-vue-next';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/ui-vue/presentation/overlays';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCollections, useProductList, type CollectionDraft } from '../../application/catalogue';

/** Ordered product selections: seasons, lookbooks, merchant sheets. */
defineOptions({ name: 'CollectionsPage' });
const collections = useCollections();
const list = useProductList();
const draft = ref<CollectionDraft | null>(null);
const pick = ref('');
const baseline = ref('');
const loading = ref(true);
const { refresh, refreshing } = useRefresh(() => Promise.all([collections.load(), list.load()]));
const resultBusy = computed(() => loading.value || collections.loading.value || refreshing.value);
function edit(value: CollectionDraft) {
  draft.value = value;
  baseline.value = JSON.stringify(value);
}
async function close() {
  if (collections.busy.value) return;
  if (
    draft.value &&
    JSON.stringify(draft.value) !== baseline.value &&
    !(await requestConfirmation(t('Discard unsaved changes?'), { confirmLabel: t('Discard changes') }))
  )
    return;
  draft.value = null;
}
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
  if (saved) {
    edit(collections.edit(saved));
    draft.value = null;
  }
}
async function remove(id: string) {
  if (
    !(await requestConfirmation(t('Delete this collection? Its products will remain.'), {
      confirmLabel: t('Delete collection'),
    }))
  )
    return;
  if (await collections.remove(id)) draft.value = null;
}
onMounted(async () => {
  await Promise.all([collections.load(), list.load()]);
  loading.value = false;
});
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">{{ t('Collections') }}</h1>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t('Seasonal selections, lookbooks and retailer collections.') }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <RefreshButton :busy="resultBusy" :disabled="collections.busy.value" @refresh="refresh" /><button
          class="btn-primary"
          @click="edit(collections.blank())"
        >
          <Icon :icon="Plus" :size="15" />{{ t('New collection') }}
        </button>
      </div>
    </div>
    <div class="catalogue-result-region">
      <ContentSkeleton
        v-if="resultBusy && !collections.loaded.value"
        :cards="3"
        variant="collection"
        :label="t('Loading collections')"
      />
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading collections')">
        <ResultEmpty
          v-if="!collections.collections.value.length"
          :label="collections.loaded.value ? t('No collections yet.') : t('Content unavailable')"
        />
        <ul v-else class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <li v-for="c in collections.collections.value" :key="c.document.id">
            <button
              :class="[
                'w-full rounded-xl border p-4 text-left transition',
                draft?.id === c.document.id
                  ? 'border-primary bg-primary-soft/50'
                  : 'border-border bg-card hover:bg-muted',
              ]"
              @click="edit(collections.edit(c))"
            >
              <p class="font-editorial text-lg">{{ c.document.names.uz }}</p>
              <SkeletonStateSlot class="catalogue-value" :is-block="true"
                ><p class="mt-1 text-xs text-muted-foreground">
                  {{ t('Products: {count}', { count: c.document.productIds.length }) }} ·
                  {{ t(c.document.isPublished ? 'Published' : 'Internal') }}
                </p></SkeletonStateSlot
              >
            </button>
          </li>
        </ul>
      </SkeletonStateGroup>
    </div>

    <Modal
      :open="!!draft"
      :dismiss-on-escape="!collections.busy.value"
      :dismiss-on-outside-click="!collections.busy.value"
      @update:open="
        (value) => {
          if (!value) void close();
        }
      "
      ><ModalContent
        v-if="draft"
        :is-blurred="true"
        class="catalogue-edit-modal w-[min(70rem,calc(100vw-3rem))] max-h-[90dvh] overflow-y-auto bg-card text-foreground"
      >
        <div class="flex items-start justify-between">
          <ModalTitle class="font-editorial text-2xl">
            {{ draft.expectedRevision === null ? t('New collection') : draft.names.uz }}
          </ModalTitle>
          <button class="btn-ghost" :disabled="collections.busy.value" :aria-label="t('Close')" @click="close">
            <Icon :icon="X" :size="16" />
          </button>
        </div>
        <fieldset :disabled="collections.busy.value" class="min-w-0 border-0 p-0">
          <div class="mt-4 grid gap-4 sm:grid-cols-3">
            <div>
              <label class="label" for="c-uz">{{ t('Name (uz)') }}</label
              ><input id="c-uz" v-model="draft.names.uz" class="field" />
            </div>
            <div>
              <label class="label" for="c-ru">{{ t('Name (ru)') }}</label
              ><input id="c-ru" v-model="draft.names.ru" class="field" />
            </div>
            <div>
              <label class="label" for="c-en">{{ t('Name (en)') }}</label
              ><input id="c-en" v-model="draft.names.en" class="field" />
            </div>
          </div>
          <div class="mt-4 grid gap-4 sm:grid-cols-[1fr_140px]">
            <div>
              <label class="label" for="c-desc">{{ t('Description') }}</label
              ><textarea id="c-desc" v-model="draft.description" rows="2" class="field" />
            </div>
            <div>
              <label class="label" for="c-order">{{ t('Order') }}</label
              ><input id="c-order" v-model.number="draft.sortOrder" type="number" class="field" />
            </div>
          </div>
          <label class="mt-4 flex items-center gap-2 text-sm"
            ><input v-model="draft.isPublished" type="checkbox" />{{ t('Show in the public catalogue feed') }}</label
          >

          <h3 class="mt-6 text-sm font-semibold">{{ t('Product order') }}</h3>
          <div class="mt-2 flex gap-2">
            <CatalogueSelect
              v-model="pick"
              :label="t('Choose a product…')"
              :options="[
                { value: '', label: t('Choose a product…') },
                ...available.map((p) => ({ value: p.id, label: (p.nameUz ?? p.id) + ' — ' + p.id })),
              ]"
            />
            <button class="btn-outline" :disabled="!pick" @click="add">{{ t('Add') }}</button>
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
              <button class="btn-ghost px-1.5" :aria-label="t('Move up')" @click="move(index, -1)">
                <Icon :icon="ArrowUp" :size="13" />
              </button>
              <button class="btn-ghost px-1.5" :aria-label="t('Move down')" @click="move(index, 1)">
                <Icon :icon="ArrowDown" :size="13" />
              </button>
              <button
                class="btn-ghost px-1.5 text-destructive"
                :aria-label="t('Remove product')"
                @click="draft.productIds.splice(index, 1)"
              >
                <Icon :icon="X" :size="13" />
              </button>
            </li>
          </ol>
          <div class="mt-6 flex items-center gap-3">
            <button class="btn-primary" :disabled="collections.busy.value || !draft.names.uz.trim()" @click="save">
              {{ collections.busy.value ? t('Saving…') : t('Save') }}
            </button>
            <button
              v-if="draft.expectedRevision !== null && draft.id"
              class="btn-ghost ml-auto text-destructive"
              :disabled="collections.busy.value"
              :aria-busy="collections.busy.value"
              @click="remove(draft.id)"
            >
              <Icon :icon="Trash2" :size="14" />{{ t('Delete') }}
            </button>
          </div>
        </fieldset>
      </ModalContent></Modal
    >
  </section>
</template>
