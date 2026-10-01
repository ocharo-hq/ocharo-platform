import type { DesignElement, DesignPage } from './EditorModels';

/** Gives edited copy usable room instead of retaining the original string's tight bounds. */
export function fitTextFrame(element: DesignElement, page: DesignPage, text: string): DesignElement {
  if (element.type !== 'text') return { ...element, text };
  if (element.x < 0 || element.x >= page.width) return { ...element, text };
  const margin = Math.min(6, page.width / 10);
  const align = element.align ?? (element.id === 'front-descriptor' ? 'center' : 'left');
  let x = element.x, width = Math.max(.1, page.width - margin - x);
  if (align === 'center') {
    const anchor = element.x + element.width / 2;
    width = Math.max(.1, 2 * Math.min(anchor - margin, page.width - margin - anchor));
    x = anchor - width / 2;
  } else if (align === 'right') {
    const anchor = element.x + element.width;
    x = Math.min(margin, element.x);
    width = Math.max(.1, anchor - x);
  }
  const size = element.fontSize ?? 3, lineHeight = size * (element.lineHeight ?? 1.2);
  // Reserve extra height conservatively; the shared renderer performs the final font-aware wrapping.
  const lines = text.split('\n').reduce((total, line) => total + Math.max(1, Math.ceil(line.length * size * .65 / width)), 0);
  const height = Math.min(2000, Math.max(element.height, lineHeight * lines + size * .25));
  return { ...element, x, width, height, text, align };
}
