import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { nextTick } from 'vue';
import CataloguePreviewViewport from '../src/presentation/design/CataloguePreviewViewport.vue';

class PreviewResizeObserver {
  static instances: PreviewResizeObserver[] = [];
  target?: Element;
  disconnect = vi.fn();

  constructor(private readonly callback: ResizeObserverCallback) {
    PreviewResizeObserver.instances.push(this);
  }

  observe(target: Element): void {
    this.target = target;
  }

  resize(width: number, height: number): void {
    this.callback(
      [{ target: this.target, contentRect: new DOMRectReadOnly(0, 0, width, height) } as ResizeObserverEntry],
      this as unknown as ResizeObserver,
    );
  }
}

let wrapper: VueWrapper;
function observer(selector: string): PreviewResizeObserver {
  const target = wrapper.get(selector).element;
  return PreviewResizeObserver.instances.find((item) => item.target === target)!;
}
function dimension(name: string): string {
  return (wrapper.element as HTMLElement).style.getPropertyValue(`--preview-${name}`);
}

beforeEach(() => {
  PreviewResizeObserver.instances = [];
  vi.stubGlobal('ResizeObserver', PreviewResizeObserver);
});
afterEach(() => {
  wrapper?.unmount();
  vi.unstubAllGlobals();
});

describe('Catalogue preview viewport', () => {
  it('fits desktop content without changing its responsive CSS width', async () => {
    wrapper = mount(CataloguePreviewViewport, {
      props: { width: 1440 },
      slots: { default: '<article>Catalogue page</article>' },
    });
    await nextTick();
    observer('.catalogue-preview-viewport').resize(720, 0);
    observer('.catalogue-preview-content').resize(1440, 1800);
    await nextTick();

    expect(dimension('width')).toBe('1440px');
    expect(dimension('scale')).toBe('0.5');
    expect(dimension('fit-width')).toBe('720px');
    expect(dimension('fit-height')).toBe('900px');
    expect(wrapper.get('article').text()).toBe('Catalogue page');
  });

  it('updates reserved space after pane resizing, image loading, and device changes', async () => {
    wrapper = mount(CataloguePreviewViewport, { props: { width: 1440 } });
    await nextTick();
    const pane = observer('.catalogue-preview-viewport');
    const content = observer('.catalogue-preview-content');
    pane.resize(720, 0);
    content.resize(1440, 1800);
    await nextTick();

    pane.resize(1080, 0);
    content.resize(1440, 2400);
    await nextTick();
    expect(dimension('scale')).toBe('0.75');
    expect(dimension('fit-height')).toBe('1800px');

    await wrapper.setProps({ width: 390 });
    content.resize(390, 3000);
    await nextTick();
    expect(dimension('width')).toBe('390px');
    expect(dimension('scale')).toBe('1');
    expect(dimension('fit-width')).toBe('390px');
    expect(dimension('fit-height')).toBe('3000px');

    pane.resize(312, 0);
    await nextTick();
    expect(dimension('scale')).toBe('0.8');
    expect(dimension('fit-height')).toBe('2400px');
  });

  it('disconnects both SDK observers when the designer leaves the viewport', async () => {
    wrapper = mount(CataloguePreviewViewport, { props: { width: 768 } });
    await nextTick();
    const observers = [...PreviewResizeObserver.instances];
    expect(observers).toHaveLength(2);
    wrapper.unmount();
    observers.forEach((item) => expect(item.disconnect).toHaveBeenCalledOnce());
  });
});
