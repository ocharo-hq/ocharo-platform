<script setup lang="ts">
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed } from 'vue';
import { cloneSnapshot, type DesignSnapshot } from '@/domain/workspace/EditorModels';
import { brandAssets } from '@/domain/workspace/Templates';
import { fitTextFrame } from '@/domain/workspace/FitTextFrame';

const { t } = useUiLocale();

defineOptions({ name: 'CardTools' });
const props = defineProps<{ snapshot: DesignSnapshot; pageId: string; selectedId: string | null }>();
const emit = defineEmits<{ 'update:snapshot': [snapshot: DesignSnapshot]; 'update:pageId': [id: string]; 'update:selectedId': [id: string | null] }>();
const front = computed(() => props.snapshot.pages.find((page) => page.id === 'card-front'));
const back = computed(() => props.snapshot.pages.find((page) => page.id === 'card-back'));
const descriptor = computed(() => front.value?.elements.find((element) => element.id === 'front-descriptor'));
const photo = computed(() => front.value?.elements.find((element) => element.id === 'front-photo'));
const fields = computed(() => back.value?.elements.filter((element) => element.type === 'text') ?? []);
const labels: Record<string, string> = { 'designer-name': 'Designer name', 'designer-role': 'Role', 'designer-location': 'Location', 'contact-0': 'Instagram', 'contact-1': 'Telegram', 'contact-2': 'Phone' };
const logo = computed(() => props.snapshot.pages.find((page) => page.id === props.pageId)?.elements.find((element) => element.id === props.selectedId && element.role === 'logo') ?? front.value?.elements.find((element) => element.role === 'logo'));
function patch(id: string, values: Record<string, unknown>): void {
  const next = cloneSnapshot(props.snapshot);
  for (const page of next.pages) {
    const element = page.elements.find((item) => item.id === id);
    if (element) {
      const updated = typeof values.text === 'string' ? fitTextFrame(element, page, values.text) : element;
      Object.assign(element, updated, values);
      if (labels[id]) element.label = labels[id];
    }
  }
  emit('update:snapshot', next);
}
function setLogo(src: string): void {
  if (!logo.value) return;
  patch(logo.value.id, { src });
  emit('update:selectedId', logo.value.id);
  const owner = props.snapshot.pages.find((page) => page.elements.some((element) => element.id === logo.value?.id));
  if (owner) emit('update:pageId', owner.id);
}
const palettes = [
  { name: 'Ivory', front: '#F6F0E5', back: '#F6F0E5', ink: '#302B25', logo: 'charcoal' },
  { name: 'Charcoal', front: '#302B25', back: '#F6F0E5', ink: '#302B25', logo: 'ivory' },
  { name: 'Olive', front: '#5D6654', back: '#F0F0E7', ink: '#414C39', logo: 'ivory' },
  { name: 'Clay', front: '#AA7560', back: '#F5EBE5', ink: '#654A3E', logo: 'ivory' },
] as const;
function palette(theme: typeof palettes[number] | null): void {
  const next = cloneSnapshot(props.snapshot);
  for (const page of next.pages) {
    if (page.id === 'card-front') {
      page.background = theme?.front ?? '#302B25';
      for (const element of page.elements) {
        if (element.id === 'front-photo') element.opacity = theme ? 0 : 1;
        if (element.id === 'front-tint') element.opacity = theme ? 0 : .52;
        if (element.role === 'logo') element.src = theme ? brandAssets.logoColors[theme.logo] : brandAssets.whiteLogo;
        if (element.type === 'text') element.color = theme ? theme.logo === 'charcoal' ? '#302B25' : '#F6F0E5' : '#FFFFFF';
      }
    } else if (page.id === 'card-back') {
      page.background = theme?.back ?? '#F6F0E5';
      for (const element of page.elements) {
        if (element.type === 'text') element.color = theme?.ink ?? (['designer-role','designer-location'].includes(element.id) ? '#6C5D4B' : '#302B25');
      }
    }
  }
  emit('update:snapshot', next);
}
function photoBackground(show: boolean): void {
  const next = cloneSnapshot(props.snapshot), page = next.pages.find((item) => item.id === 'card-front');
  if (!page) return;
  for (const element of page.elements) {
    if (element.id === 'front-photo') element.opacity = show ? 1 : 0;
    if (element.id === 'front-tint') element.opacity = show ? .52 : 0;
    if (show && element.role === 'logo') element.src = brandAssets.whiteLogo;
    if (show && element.type === 'text') element.color = '#FFFFFF';
  }
  emit('update:snapshot', next);
}
</script>

<template>
  <section class="template-tools">
    <h3>{{ t("Business card tools") }}</h3>
    <div class="template-tool-actions"><button v-if="front" type="button" :aria-pressed="pageId === front.id" @click="emit('update:pageId', front.id); emit('update:selectedId', null)">{{ t("Front") }}</button><button v-if="back" type="button" :aria-pressed="pageId === back.id" @click="emit('update:pageId', back.id); emit('update:selectedId', null)">{{ t("Back") }}</button></div>
    <label v-if="descriptor" class="descriptor-toggle"><input type="checkbox" :checked="descriptor.opacity !== 0" @change="patch(descriptor.id, {opacity: ($event.target as HTMLInputElement).checked ? 1 : 0})"> {{ t("Show front descriptor") }}</label>
    <label v-if="photo" class="descriptor-toggle"><input type="checkbox" :checked="photo.opacity !== 0" @change="photoBackground(($event.target as HTMLInputElement).checked)"> {{ t("Use photographic background") }}</label>
    <button v-if="photo && front" type="button" class="photo-control" @click="emit('update:pageId', front.id); emit('update:selectedId', photo.id)">{{ t("Edit front photograph") }}</button>
    <div class="palette-options" :aria-label="t('Card color palettes')"><button type="button" :title="t('Restore original Charcoal veil colors')" @click="palette(null)">{{ t("Original v5") }}</button><button v-for="theme in palettes" :key="theme.name" type="button" :style="{background: theme.front, color: theme.logo === 'charcoal' ? '#302B25' : '#F6F0E5'}" @click="palette(theme)">{{ t(theme.name) }}</button></div>
    <label v-if="logo" class="field-label">{{ t("Logo color") }}<select :value="logo.src" @change="setLogo(($event.target as HTMLSelectElement).value)"><option :value="brandAssets.whiteLogo">{{ t("Original white") }}</option><option :value="brandAssets.blackLogo">{{ t("Original black") }}</option><option v-for="(src, name) in brandAssets.logoColors" :key="name" :value="src">{{ name }}</option></select></label>
    <label v-for="field in fields" :key="field.id" class="field-label">{{ t(labels[field.id] ?? field.label) }}<input type="text" :value="field.text" @change="patch(field.id, {text: ($event.target as HTMLInputElement).value})"></label>
    <p class="template-help">{{ t("85 × 55 mm. The logo remains the original artwork. Change the front photo or back color with the page controls.") }}</p>
    <p class="template-source">{{ t("Based on Charcoal veil v5 ·") }} {{ snapshot.locale.toUpperCase() }}</p>
  </section>
</template>

<style scoped>
.template-tools { border-top: 1px solid #ded8cb; padding-top: 20px; margin-top: 20px; display: grid; gap: 12px; }
h3 { font-size: 14px; margin: 0; }
.template-tool-actions { display: flex; gap: 8px; }
.template-tool-actions button { border: 1px solid #d7d1c4; background: #fbf9f3; border-radius: 6px; padding: 7px 10px; font-size: 12px; cursor: pointer; }
.template-tool-actions button[aria-pressed=true] { background: #e9e3d3; border-color: #9d927b; }
.palette-options { display: flex; flex-wrap: wrap; gap: 6px; }
.palette-options button { font-size: 10px; padding: 7px 9px; border-radius: 5px; border: 1px solid #d7d1c4; cursor: pointer; }
.descriptor-toggle { display: flex; align-items: center; gap: 8px; font-size: 12px; }
.photo-control { text-align: left; font-size: 11px; color: #685b42; cursor: pointer; }
.template-help, .template-source { font-size: 11px; line-height: 1.6; color: #7b7569; margin: 0; }
.template-source { font-size: 10px; }
</style>
