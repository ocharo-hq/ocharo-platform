<script setup lang="ts">
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { computed, ref, watch, type CSSProperties } from 'vue';
import type { DesignElement, DesignPage } from '../../domain/design/EditorModels';

const { t } = useUiLocale();

defineOptions({ name: 'DesignCanvas' });
const props = withDefaults(
  defineProps<{ page: DesignPage; selectedId?: string | null; editable?: boolean; scale?: number }>(),
  {
    selectedId: null,
    editable: false,
    scale: 1,
  },
);
const emit = defineEmits<{ select: [id: string | null]; change: [elements: DesignElement[]] }>();
const surface = ref<HTMLElement>();
const draft = ref<DesignElement[] | null>(null);
const dimensions = ref<Record<string, { width: number; height: number }>>({});
const elements = computed(() => draft.value ?? props.page.elements);
const selected = computed(() => elements.value.find((element) => element.id === props.selectedId));
interface Gesture {
  pointer: number;
  x: number;
  y: number;
  mmX: number;
  mmY: number;
  original: DesignElement;
  mode: 'move' | 'resize';
  changed: boolean;
}
let gesture: Gesture | undefined;
watch(
  () => props.page,
  () => {
    gesture = undefined;
    draft.value = null;
  },
);

function frame(element: DesignElement): CSSProperties {
  return {
    left: `${(element.x / props.page.width) * 100}%`,
    top: `${(element.y / props.page.height) * 100}%`,
    width: `${(element.width / props.page.width) * 100}%`,
    height: `${(element.height / props.page.height) * 100}%`,
    transform: `rotate(${element.rotation ?? 0}deg)`,
    opacity: element.opacity ?? 1,
    pointerEvents: element.opacity === 0 ? 'none' : undefined,
    borderRadius: `${((element.radius ?? 0) / props.page.width) * 100}cqw`,
    background: element.background ?? (element.type === 'shape' ? (element.color ?? '#FFFFFF') : undefined),
    color: element.color ?? '#292922',
    '--element-border': element.borderWidth
      ? `inset 0 0 0 ${(element.borderWidth / props.page.width) * 100}cqw ${element.borderColor ?? '#000000'}`
      : 'none',
  };
}
function typography(element: DesignElement): CSSProperties {
  return {
    fontFamily: element.fontFamily === 'serif' ? 'Georgia, Times New Roman, serif' : 'Arial, sans-serif',
    fontSize: `${((element.fontSize ?? 3) / props.page.width) * 100}cqw`,
    fontWeight: element.fontWeight ?? 'normal',
    lineHeight: element.lineHeight ?? 1.2,
    letterSpacing: `${((element.letterSpacing ?? 0) / props.page.width) * 100}cqw`,
    textAlign: element.align ?? (element.direction === 'rtl' ? 'right' : 'left'),
    direction: element.direction ?? 'ltr',
  };
}
function imageStyle(element: DesignElement): CSSProperties {
  const natural = dimensions.value[element.src ?? ''];
  if (!natural)
    return {
      width: '100%',
      height: '100%',
      objectFit: element.fit ?? 'cover',
      objectPosition: `${element.cropX ?? 50}% ${element.cropY ?? 50}%`,
    };
  const factor =
    (element.fit === 'contain' ? Math.min : Math.max)(element.width / natural.width, element.height / natural.height) *
    (element.zoom ?? 1);
  const width = natural.width * factor,
    height = natural.height * factor;
  return {
    width: `${(width / element.width) * 100}%`,
    height: `${(height / element.height) * 100}%`,
    left: `${((element.width - width) * (element.cropX ?? 50)) / element.width}%`,
    top: `${((element.height - height) * (element.cropY ?? 50)) / element.height}%`,
  };
}
function loaded(event: Event, source?: string): void {
  const image = event.target as HTMLImageElement;
  if (source && image.naturalWidth)
    dimensions.value[source] = { width: image.naturalWidth, height: image.naturalHeight };
}
function start(event: PointerEvent, element: DesignElement, mode: 'move' | 'resize' = 'move'): void {
  if (!props.editable || event.button !== 0) return;
  event.stopPropagation();
  emit('select', element.id);
  if (element.locked || !surface.value) return;
  event.preventDefault();
  const bounds = surface.value.getBoundingClientRect();
  gesture = {
    pointer: event.pointerId,
    x: event.clientX,
    y: event.clientY,
    mmX: props.page.width / bounds.width,
    mmY: props.page.height / bounds.height,
    original: { ...element },
    mode,
    changed: false,
  };
  draft.value = props.page.elements.map((item) => ({ ...item }));
  surface.value.setPointerCapture(event.pointerId);
  surface.value.focus({ preventScroll: true });
}
function move(event: PointerEvent): void {
  if (!gesture || gesture.pointer !== event.pointerId || !draft.value) return;
  const dx = (event.clientX - gesture.x) * gesture.mmX,
    dy = (event.clientY - gesture.y) * gesture.mmY;
  const element = draft.value.find((item) => item.id === gesture?.original.id);
  if (!element) return;
  const source = gesture.original;
  if (gesture.mode === 'move') {
    element.x = Math.max(-1000, Math.min(1000, source.x + dx));
    element.y = Math.max(-1000, Math.min(1000, source.y + dy));
  } else {
    const radians = (-(source.rotation ?? 0) * Math.PI) / 180;
    const localX = dx * Math.cos(radians) - dy * Math.sin(radians),
      localY = dx * Math.sin(radians) + dy * Math.cos(radians);
    element.width = Math.max(0.5, Math.min(2000, source.width + localX));
    element.height = event.shiftKey
      ? Math.max(0.5, Math.min(2000, (element.width * source.height) / source.width))
      : Math.max(0.5, Math.min(2000, source.height + localY));
    const angle = -radians,
      dw = element.width - source.width,
      dh = element.height - source.height;
    element.x = Math.max(-1000, Math.min(1000, source.x + (dw * (Math.cos(angle) - 1) - dh * Math.sin(angle)) / 2));
    element.y = Math.max(-1000, Math.min(1000, source.y + (dw * Math.sin(angle) + dh * (Math.cos(angle) - 1)) / 2));
  }
  gesture.changed = Math.abs(dx) + Math.abs(dy) > 0.05;
}
function finish(event: PointerEvent, cancelled = false): void {
  if (!gesture || event.pointerId !== gesture.pointer) return;
  const changed = gesture.changed;
  gesture = undefined;
  if (surface.value?.hasPointerCapture(event.pointerId)) surface.value.releasePointerCapture(event.pointerId);
  if (!cancelled && changed && draft.value)
    emit(
      'change',
      draft.value.map((element) => ({ ...element })),
    );
  draft.value = null;
}
function keyboard(event: KeyboardEvent): void {
  if (!props.editable) return;
  if (event.key === 'Escape') {
    draft.value = null;
    gesture = undefined;
    emit('select', null);
    return;
  }
  const element = selected.value;
  if (!element || element.locked || !['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return;
  event.preventDefault();
  const step = event.shiftKey ? 5 : 0.5;
  const x = Math.max(
    -1000,
    Math.min(1000, element.x + (event.key === 'ArrowRight' ? step : event.key === 'ArrowLeft' ? -step : 0)),
  );
  const y = Math.max(
    -1000,
    Math.min(1000, element.y + (event.key === 'ArrowDown' ? step : event.key === 'ArrowUp' ? -step : 0)),
  );
  emit(
    'change',
    props.page.elements.map((item) => (item.id === element.id ? { ...item, x, y } : { ...item })),
  );
}
</script>

<template>
  <div
    ref="surface"
    class="design-canvas"
    :class="{ 'is-editable': editable }"
    :style="{ width: `${scale * 100}%`, aspectRatio: `${page.width} / ${page.height}`, background: page.background }"
    :tabindex="editable ? 0 : -1"
    role="group"
    :aria-label="t('{title} design canvas', { title: page.title })"
    @pointerdown.self="emit('select', null)"
    @pointermove="move"
    @pointerup="finish($event)"
    @pointercancel="finish($event, true)"
    @keydown="keyboard"
  >
    <div class="design-canvas-clip" @pointerdown.self="emit('select', null)">
      <div
        v-for="element in elements"
        :key="element.id"
        class="design-element"
        :class="{
          'is-text': element.type === 'text',
          'is-image': element.type === 'image',
          'is-locked': element.locked,
        }"
        :style="[frame(element), element.type === 'text' ? typography(element) : {}]"
        :title="editable ? element.label : undefined"
        @pointerdown="start($event, element)"
      >
        <img
          v-if="element.type === 'image'"
          :src="element.src"
          :style="imageStyle(element)"
          alt=""
          draggable="false"
          @load="loaded($event, element.src)"
        />
        <template v-else-if="element.type === 'text'">{{ element.text }}</template>
      </div>
    </div>
    <div
      v-if="editable && selected"
      class="design-selection"
      :style="{ ...frame(selected), opacity: 1, background: 'transparent', boxShadow: undefined }"
      aria-hidden="true"
    >
      <span class="design-selection-label">{{ selected.label }}</span>
      <button
        v-if="!selected.locked"
        type="button"
        class="design-resize"
        :aria-label="t('Resize selected element')"
        @pointerdown="start($event, selected, 'resize')"
      />
    </div>
  </div>
</template>

<style scoped>
.design-canvas {
  container-type: inline-size;
  position: relative;
  flex-shrink: 0;
  max-width: 100%;
  box-shadow: 0 12px 36px #21180e26;
  outline: none;
}
.design-canvas-clip {
  position: absolute;
  inset: 0;
  overflow: hidden;
}
.design-element {
  position: absolute;
  box-sizing: border-box;
  transform-origin: center;
}
.design-element::after {
  content: '';
  position: absolute;
  inset: 0;
  box-shadow: var(--element-border);
  border-radius: inherit;
  pointer-events: none;
}
.design-element.is-image {
  overflow: hidden;
}
.design-element img {
  position: absolute;
  display: block;
  max-width: none;
  user-select: none;
  pointer-events: none;
}
.design-element.is-text {
  white-space: pre-wrap;
  overflow-wrap: normal;
  overflow: hidden;
}
.is-editable {
  touch-action: none;
}
.is-editable .design-element {
  cursor: move;
  user-select: none;
}
.is-editable .is-locked {
  cursor: default;
}
.design-selection {
  position: absolute;
  outline: 1.5px solid #756b50;
  pointer-events: none;
  z-index: 1;
}
.design-selection-label {
  position: absolute;
  left: -1px;
  bottom: 100%;
  margin-bottom: 4px;
  max-width: 210px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  padding: 3px 6px;
  border-radius: 3px;
  color: white;
  background: #514a37;
  font:
    10px/1.3 Arial,
    sans-serif;
}
.design-resize {
  position: absolute;
  width: 12px;
  height: 12px;
  right: -6px;
  bottom: -6px;
  background: #fff;
  border: 2px solid #756b50;
  border-radius: 2px;
  pointer-events: auto;
  cursor: nwse-resize;
  padding: 0;
  touch-action: none;
}
</style>
