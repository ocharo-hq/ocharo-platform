import type { DesignElement } from '@/domain/workspace/EditorModels';

const fonts = { serif: 'Georgia, Times New Roman, serif', sans: 'Arial, sans-serif' };
/** Both the PDF renderer and preflight measure exactly these lines and baselines. */
export function layoutDesignText(context: CanvasRenderingContext2D, element: DesignElement, unit: number) {
  const size = (element.fontSize ?? 3) * unit;
  context.font = `${element.fontWeight ?? 'normal'} ${size}px ${fonts[element.fontFamily ?? 'sans']}`;
  context.direction = element.direction ?? 'ltr';
  const alignment = element.align ?? (element.direction === 'rtl' ? 'right' : 'left');
  context.textBaseline = 'alphabetic'; context.textAlign = alignment;
  context.letterSpacing = `${(element.letterSpacing ?? 0) * unit}px`;
  const width = element.width * unit, lines: string[] = [];
  for (const paragraph of (element.text ?? '').split('\n')) {
    let line = '';
    const tokens = paragraph.split(/(\s+|[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}])/u).filter(Boolean);
    for (const token of tokens) {
      const candidate = line + token;
      if (context.measureText(candidate).width > width && line.trim() && token.trim()) { lines.push(line.trimEnd()); line = token.trimStart(); }
      else line = candidate;
    }
    lines.push(line);
  }
  const anchor = alignment === 'center' ? width / 2 : alignment === 'right' ? width : 0;
  const metrics = context.measureText('Hg'), lineHeight = size * (element.lineHeight ?? 1.2);
  const ascent = metrics.fontBoundingBoxAscent ?? size * 0.8, descent = metrics.fontBoundingBoxDescent ?? size * 0.2;
  const baseline = (lineHeight - ascent - descent) / 2 + ascent;
  const tolerance = unit * 0.05;
  const overflow = lines.some((line, index) => {
    if (!line.trim()) return false;
    const measured = context.measureText(line), y = baseline + index * lineHeight;
    return measured.width > width + tolerance ||
      (measured.actualBoundingBoxLeft !== undefined && anchor - measured.actualBoundingBoxLeft < -tolerance) ||
      (measured.actualBoundingBoxRight !== undefined && anchor + measured.actualBoundingBoxRight > width + tolerance) ||
      y - (measured.actualBoundingBoxAscent ?? ascent) < -tolerance ||
      y + (measured.actualBoundingBoxDescent ?? descent) > element.height * unit + tolerance;
  });
  return { lines, anchor, baseline, lineHeight, overflow };
}
