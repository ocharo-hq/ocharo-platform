import { describe, expect, it } from 'vitest';
import { fitTextFrame } from '@/domain/workspace/FitTextFrame';
import { createCardTemplate } from '@/domain/workspace/Templates';

describe('editable text frames', () => {
  it('gives an ordinary location edit enough width without mutating the source', () => {
    const page = createCardTemplate().pages[1]!;
    const location = page.elements.find((element) => element.id === 'designer-location')!;
    const result = fitTextFrame(location, page, 'Toshkent · O‘zbekiston');
    expect(result.width).toBeGreaterThan(60);
    expect(result.text).toBe('Toshkent · O‘zbekiston');
    expect(result.x).toBe(location.x);
    expect(location.width).toBeLessThan(20);
    expect(result.x + result.width).toBeCloseTo(79);
  });

  it('preserves centered text anchors and reserves multiline height', () => {
    const page = createCardTemplate().pages[0]!;
    const source = page.elements.find((element) => element.id === 'front-descriptor')!;
    const centered = { ...source, align: 'center' as const };
    const result = fitTextFrame(centered, page, 'First line\nSecond line\nThird line');
    expect(result.x + result.width / 2).toBeCloseTo(centered.x + centered.width / 2);
    expect(result.height).toBeGreaterThan((source.fontSize ?? 3) * 3);
  });
});
