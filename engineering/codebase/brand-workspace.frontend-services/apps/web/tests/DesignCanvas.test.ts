import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import DesignCanvas from '@/presentation/workspace/DesignCanvas.vue';
import type { DesignPage } from '@/domain/workspace/EditorModels';

function canvas(locked = false) {
  const page: DesignPage = { id: 'page', title: 'Canvas', width: 100, height: 100, background: '#FFFFFF',
    elements: [{ id: 'photo', type: 'image', label: 'Garment', x: 10, y: 20, width: 30, height: 40, src: '/templates/photo.png', locked }] };
  const wrapper = mount(DesignCanvas, { props: { page, selectedId: 'photo', editable: true } });
  const surface = wrapper.get('.design-canvas').element as HTMLElement;
  surface.setPointerCapture = vi.fn(); surface.releasePointerCapture = vi.fn(); surface.hasPointerCapture = () => true;
  vi.spyOn(surface, 'getBoundingClientRect').mockReturnValue({ width: 200, height: 200, x: 0, y: 0, top: 0, left: 0, right: 200, bottom: 200, toJSON: () => ({}) });
  return { wrapper, page };
}

describe('design canvas transactions', () => {
  it('commits exactly one immutable change at the end of a drag', async () => {
    const { wrapper, page } = canvas();
    await wrapper.get('.design-element').trigger('pointerdown', { pointerId: 1, button: 0, clientX: 20, clientY: 40 });
    await wrapper.get('.design-canvas').trigger('pointermove', { pointerId: 1, clientX: 40, clientY: 50 });
    expect(wrapper.emitted('change')).toBeUndefined();
    expect(page.elements[0]?.x).toBe(10);
    await wrapper.get('.design-canvas').trigger('pointerup', { pointerId: 1 });
    expect(wrapper.emitted('change')).toHaveLength(1);
    expect(wrapper.emitted('change')?.[0]?.[0]).toEqual([expect.objectContaining({ x: 20, y: 25, width: 30, height: 40 })]);
    wrapper.unmount();
  });

  it('discards a cancelled pointer movement', async () => {
    const { wrapper } = canvas();
    await wrapper.get('.design-element').trigger('pointerdown', { pointerId: 1, button: 0, clientX: 20, clientY: 40 });
    await wrapper.get('.design-canvas').trigger('pointermove', { pointerId: 1, clientX: 60, clientY: 60 });
    await wrapper.get('.design-canvas').trigger('pointercancel', { pointerId: 1 });
    expect(wrapper.emitted('change')).toBeUndefined();
    wrapper.unmount();
  });

  it('keeps locked artwork selectable without allowing movement', async () => {
    const { wrapper } = canvas(true);
    await wrapper.get('.design-element').trigger('pointerdown', { pointerId: 1, button: 0, clientX: 20, clientY: 40 });
    await wrapper.get('.design-canvas').trigger('pointermove', { pointerId: 1, clientX: 60, clientY: 60 });
    await wrapper.get('.design-canvas').trigger('pointerup', { pointerId: 1 });
    expect(wrapper.emitted('select')?.[0]).toEqual(['photo']);
    expect(wrapper.emitted('change')).toBeUndefined();
    expect(wrapper.find('.design-resize').exists()).toBe(false);
    wrapper.unmount();
  });
});
