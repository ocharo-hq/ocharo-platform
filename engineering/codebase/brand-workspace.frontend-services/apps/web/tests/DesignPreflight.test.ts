import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { crossesPageEdge } from '@/domain/workspace/DesignPreflight';
import type { DesignElement, DesignPage, DesignSnapshot } from '@/domain/workspace/EditorModels';
import { inspectDesign } from '@/integration/workspace/DesignPreflight';
import { layoutDesignText } from '@/integration/workspace/DesignTextLayout';

const text = (patch: Partial<DesignElement> = {}): DesignElement => ({ id: 'text', type: 'text', label: 'Description', x: 10, y: 10, width: 30, height: 10, text: 'One line', fontSize: 3, ...patch });
const page = (elements: DesignElement[], id = 'first'): DesignPage => ({ id, title: id, width: 210, height: 297, background: '#ffffff', elements });
const snapshot = (...pages: DesignPage[]): DesignSnapshot => ({ schemaVersion: 1, kind: 'catalogue', locale: 'en', templateId: 'test', pages });
const photo = (id: string, src = '/templates/photo.jpg'): DesignElement => ({ id, type: 'image', label: id, x: 10, y: 10, width: 40, height: 40, ...(src ? { src } : {}) });
const imageLoader = vi.fn();
function measuredContext(): CanvasRenderingContext2D {
  const context = { font: '', measureText(value: string) {
    const size = Number(this.font.match(/([\d.]+)px/)?.[1] ?? 3);
    return { width: value.length * size * 0.5, fontBoundingBoxAscent: size * 0.8, fontBoundingBoxDescent: size * 0.2,
      actualBoundingBoxAscent: value ? size * 0.75 : 0, actualBoundingBoxDescent: value ? size * 0.18 : 0 };
  } };
  return context as unknown as CanvasRenderingContext2D;
}
beforeEach(() => {
  Object.defineProperty(document, 'fonts', { configurable: true, value: { ready: Promise.resolve() } });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(measuredContext());
  imageLoader.mockReset().mockResolvedValue({ width: 300, height: 400 });
});
afterEach(() => vi.restoreAllMocks());
describe('catalogue export preflight', () => {
  it('finds vertical clipping and unbreakable words using the PDF text layout', async () => {
    const input = snapshot(page([text({ text: 'First line\nSecond line\nThird line', height: 4 }), text({ id: 'long', text: 'anunbreakableword', width: 8 })]));
    const result = await inspectDesign(input, undefined, undefined, imageLoader);
    expect(result.issues.map(issue => [issue.elementId, issue.code, issue.severity])).toEqual([
      ['text', 'text-overflow', 'warning'], ['long', 'text-overflow', 'warning'],
    ]);
    const layout = layoutDesignText(measuredContext(), text({ text: 'First line\nSecond line', height: 20 }), 1);
    expect(layout.lines).toEqual(['First line', 'Second line']); expect(layout.overflow).toBe(false);
  });
  it('detects rotated frames beyond the sheet while allowing import rounding', () => {
    const sheet = page([]);
    expect(crossesPageEdge(text({ x: 0, y: 0, rotation: 45 }), sheet)).toBe(true);
    expect(crossesPageEdge(text({ x: -0.02, y: 0 }), sheet)).toBe(false);
    expect(crossesPageEdge(text({ x: -0.2, y: 0 }), sheet)).toBe(true);
  });
  it('checks only selected pages, keeping actual document page numbers', async () => {
    const input = snapshot(page([photo('broken')]), page([text({ x: -1 })], 'second'));
    const result = await inspectDesign(input, ['second'], undefined, imageLoader);
    expect(result.pageCount).toBe(1); expect(result.issues).toHaveLength(1);
    expect(result.issues[0]).toMatchObject({ pageId: 'second', pageNumber: 2, code: 'outside-page' });
    expect(imageLoader).not.toHaveBeenCalled();
  });
  it('blocks empty and unavailable images with their page and element labels', async () => {
    imageLoader.mockRejectedValue(new Error('unavailable'));
    const result = await inspectDesign(snapshot(page([photo('not-selected', ''), photo('unavailable'), photo('same-media')])), undefined, undefined, imageLoader);
    expect(result.issues.map(issue => issue.elementId)).toEqual(['not-selected', 'unavailable', 'same-media']);
    expect(result.issues.every(issue => issue.code === 'missing-image' && issue.severity === 'error')).toBe(true);
    expect(imageLoader).toHaveBeenCalledTimes(1);
  });
  it('ignores fully transparent decorations and checks each source once', async () => {
    const input = snapshot(page([{ ...photo('hidden', ''), opacity: 0, x: -400 }, photo('first'), photo('second')]));
    const before = JSON.stringify(input), result = await inspectDesign(input, undefined, undefined, imageLoader);
    expect(result.issues).toEqual([]); expect(imageLoader).toHaveBeenCalledTimes(1); expect(JSON.stringify(input)).toBe(before);
  });
  it('blocks missing translated names and quantity labels without accepting an unrelated locale', async () => {
    const input=snapshot(page([text({text:'',binding:{kind:'product',field:'name',productId:'coat'}}),text({id:'quantity',text:'1–9',binding:{kind:'product',field:'price-label',productId:'coat',index:0}})]));
    input.locale='fr'; input.catalogueSettings={mediaGroup:'all',priceDisplay:{show:true,showRetail:true,showWholesale:true,showCurrency:true,showQuantity:true},copyByLocale:{}};
    const result=await inspectDesign(input,undefined,undefined,imageLoader);
    expect(result.issues.filter(issue=>issue.code==='missing-translation')).toHaveLength(2);
    expect(result.issues.every(issue=>issue.severity==='error')).toBe(true);
  });
  it('keeps independent extra-language copy explicitly reviewable', async () => {
    const input=snapshot(page([text({text:'Review'})]));input.locale='ja';
    const result=await inspectDesign(input,undefined,undefined,imageLoader);
    expect(result.issues).toContainEqual(expect.objectContaining({code:'independent-copy',severity:'warning'}));
  });
  it('keeps intentional bleed as reviewable warnings and propagates cancellation', async () => {
    const input = snapshot(page([{ id: 'bleed', type: 'shape', label: 'Background', x: -3, y: -3, width: 216, height: 303 }]));
    expect((await inspectDesign(input, undefined, undefined, imageLoader)).issues[0]?.severity).toBe('warning');
    const controller = new AbortController(); controller.abort();
    await expect(inspectDesign(input, undefined, controller.signal, imageLoader)).rejects.toThrow();
  });
});
