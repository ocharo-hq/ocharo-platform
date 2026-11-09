<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit } = useIdentitySession();
import { useRefresh } from '@wow-two-beta/fe/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/fe/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
const { t } = useUiLocale();
import { requestConfirmation } from '../../application/common/UseFeedback';
import { computed, onMounted, ref } from 'vue';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-vue-next';
import { Modal, ModalContent, ModalTitle } from '@wow-two-beta/fe/presentation/overlays';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { LabelText } from '@wow-two-beta/fe/presentation/display';
import { CheckboxField, NumberInput, TextAreaInput, TextInput } from '@wow-two-beta/fe/presentation/forms';
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
        <RefreshButton :busy="resultBusy" :disabled="collections.busy.value" @refresh="refresh" /><Button
          :is-disabled="!sessionCanEdit"
          @click="edit(collections.blank())"
        >
          <template #leading><Icon :icon="Plus" :size="15" /></template>
          {{ t('New collection') }}
        </Button>
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
            <Button
              :variant="draft?.id === c.document.id ? 'soft' : 'outline'"
              :tone="draft?.id === c.document.id ? 'primary' : 'neutral'"
              is-full-width
              is-multiline
              class="h-auto flex-col items-start rounded-xl p-4 text-left font-normal text-foreground"
              :aria-pressed="draft?.id === c.document.id"
              @click="edit(collections.edit(c))"
            >
              <p class="font-editorial text-lg">{{ c.document.names.uz }}</p>
              <SkeletonStateSlot class="catalogue-value" :is-block="true"
                ><p class="mt-1 text-xs text-muted-foreground">
                  {{ t('Products: {count}', { count: c.document.productIds.length }) }} ·
                  {{ t(c.document.isPublished ? 'Published' : 'Internal') }}
                </p></SkeletonStateSlot
              >
            </Button>
          </li>
        </ul>
      </SkeletonStateGroup>
    </div>

    <Modal
      :open="!!draft"
      :can-dismiss-on-escape="!collections.busy.value"
      :can-dismiss-on-outside-click="!collections.busy.value"
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
          <Button
            variant="ghost"
            tone="neutral"
            shape="square"
            :is-disabled="collections.busy.value"
            :aria-label="t('Close')"
            @click="close"
          >
            <Icon :icon="X" :size="16" />
          </Button>
        </div>
        <fieldset :disabled="!sessionCanEdit || collections.busy.value" class="min-w-0 border-0 p-0">
          <div class="mt-4 grid gap-4 sm:grid-cols-3">
            <div>
              <LabelText for="c-uz" class="mb-1.5 block">{{ t('Name (uz)') }}</LabelText>
              <TextInput id="c-uz" v-model="draft.names.uz" />
            </div>
            <div>
              <LabelText for="c-ru" class="mb-1.5 block">{{ t('Name (ru)') }}</LabelText>
              <TextInput id="c-ru" :model-value="draft.names.ru ?? ''" @update:model-value="draft.names.ru = $event" />
            </div>
            <div>
              <LabelText for="c-en" class="mb-1.5 block">{{ t('Name (en)') }}</LabelText>
              <TextInput id="c-en" :model-value="draft.names.en ?? ''" @update:model-value="draft.names.en = $event" />
            </div>
          </div>
          <div class="mt-4 grid gap-4 sm:grid-cols-[1fr_140px]">
            <div>
              <LabelText for="c-desc" class="mb-1.5 block">{{ t('Description') }}</LabelText>
              <TextAreaInput
                id="c-desc"
                :rows="2"
                :model-value="draft.description ?? ''"
                @update:model-value="draft.description = $event"
              />
            </div>
            <div>
              <LabelText for="c-order" class="mb-1.5 block">{{ t('Order') }}</LabelText>
              <NumberInput
                id="c-order"
                :model-value="draft.sortOrder"
                @update:model-value="draft.sortOrder = $event ?? 0"
              />
            </div>
          </div>
          <CheckboxField
            v-model="draft.isPublished"
            wrapper-class-name="mt-4"
            :label="t('Show in the public catalogue feed')"
          />

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
            <Button variant="outline" tone="neutral" :is-disabled="!pick" @click="add">{{ t('Add') }}</Button>
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
              <Button
                variant="ghost"
                tone="neutral"
                shape="square"
                size="sm"
                :aria-label="t('Move up')"
                @click="move(index, -1)"
              >
                <Icon :icon="ArrowUp" :size="13" />
              </Button>
              <Button
                variant="ghost"
                tone="neutral"
                shape="square"
                size="sm"
                :aria-label="t('Move down')"
                @click="move(index, 1)"
              >
                <Icon :icon="ArrowDown" :size="13" />
              </Button>
              <Button
                variant="ghost"
                tone="danger"
                shape="square"
                size="sm"
                :aria-label="t('Remove product')"
                @click="draft.productIds.splice(index, 1)"
              >
                <Icon :icon="X" :size="13" />
              </Button>
            </li>
          </ol>
          <div class="mt-6 flex items-center gap-3">
            <Button
              :is-disabled="!sessionCanEdit || !draft.names.uz.trim()"
              :is-loading="collections.busy.value"
              @click="save"
            >
              {{ collections.busy.value ? t('Saving…') : t('Save') }}
            </Button>
            <Button
              v-if="draft.expectedRevision !== null && draft.id"
              variant="ghost"
              tone="danger"
              class="ml-auto"
              :is-disabled="!sessionCanEdit || collections.busy.value"
              @click="remove(draft.id)"
            >
              <template #leading><Icon :icon="Trash2" :size="14" /></template>
              {{ t('Delete') }}
            </Button>
          </div>
        </fieldset>
      </ModalContent></Modal
    >
  </section>
</template>
