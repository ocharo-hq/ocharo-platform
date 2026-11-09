import { createApiClient } from '@wow-two-beta/fe/foundation/http';
import { AppErrorType, ResultExtensions, type AppError, type Result } from '@wow-two-beta/fe/foundation/results';

import { selectDesignPages } from '@/domain/design/DesignPageSelection';
import { isSafeDesignSource, type DesignElement, type DesignPage, type DesignSnapshot } from '@/domain/common/models/EditorModels';

import { layoutDesignText } from './DesignTextLayout';

/** Why a design could not be rendered or exported; `message` is the display-safe English source. */
export type DesignExportFailure = AppError;

/** How long one image may take to arrive. @internal */
const ImageTimeoutMs = 30_000;

/** @internal The shared SDK transport; images come only from this origin's protected routes, with its cookies. */
const client = createApiClient({ credentials: 'same-origin', retry: false });

/** @internal A failed export with its display-safe reason. */
function exportFailure(message: string, type: AppErrorType = AppErrorType.Unavailable): Result<never, DesignExportFailure> {
  return ResultExtensions.fail({ type, message });
}

/** Loads only bundled or organization-protected image routes; never remote URLs. */
export async function loadDesignImage(src: string, signal?: AbortSignal): Promise<Result<HTMLImageElement, DesignExportFailure>> {
  if (!isSafeDesignSource(src)) return exportFailure('This image source cannot be exported.', AppErrorType.Validation);
  const timeout = AbortSignal.timeout(ImageTimeoutMs);
  const response = await client.get(src, { response: 'blob', signal: signal ? AbortSignal.any([signal, timeout]) : timeout });
  if (!response.ok) return exportFailure('An image is unavailable. Sign in and retry the export.');
  const blob = response.value;
  if (!/^image\/(png|jpeg|svg\+xml|webp)/.test(blob.type)) return exportFailure('Unsupported image in design.', AppErrorType.Validation);
  const url = URL.createObjectURL(blob);
  try {
    const image = new Image(); image.src = url;
    await image.decode();
    return ResultExtensions.ok(image);
  } catch {
    return exportFailure('Unsupported image in design.', AppErrorType.Validation);
  } finally { URL.revokeObjectURL(url); }
}
async function drawElement(context: CanvasRenderingContext2D, element: DesignElement, unit: number, imageLoader: typeof loadDesignImage): Promise<Result<void, DesignExportFailure>> {
  if (element.opacity === 0) return ResultExtensions.ok(undefined);
  const { x, y, width, height } = element;
  context.save(); context.translate((x + width / 2) * unit, (y + height / 2) * unit);
  context.rotate((element.rotation ?? 0) * Math.PI / 180);
  context.translate(-width * unit / 2, -height * unit / 2);
  context.globalAlpha = element.opacity ?? 1;
  const w = width * unit, h = height * unit;
  context.beginPath(); context.roundRect(0, 0, w, h, Math.min(element.radius ?? 0, width / 2, height / 2) * unit);
  if (element.background || element.type === 'shape') {
    context.fillStyle = element.background ?? element.color ?? '#ffffff'; context.fill();
  }
  context.clip();
  if (element.type === 'image' && element.src) {
    const loaded = await imageLoader(element.src);
    if (!loaded.ok) { context.restore(); return loaded; }
    const image = loaded.value;
    const fit = element.fit === 'contain' ? Math.min(w / image.width, h / image.height) : Math.max(w / image.width, h / image.height);
    const scale = fit * (element.zoom ?? 1);
    const dw = image.width * scale, dh = image.height * scale;
    context.drawImage(image, (w - dw) * (element.cropX ?? 50) / 100, (h - dh) * (element.cropY ?? 50) / 100, dw, dh);
  } else if (element.type === 'text') {
    context.fillStyle = element.color ?? '#292922';
    const { lines, anchor, baseline, lineHeight } = layoutDesignText(context, element, unit);
    lines.forEach((line, i) => context.fillText(line, anchor, baseline + i * lineHeight));
  }
  if (element.borderColor && element.borderWidth) {
    context.strokeStyle = element.borderColor; context.lineWidth = element.borderWidth * unit;
    const inset = element.borderWidth * unit / 2;
    context.beginPath(); context.roundRect(inset, inset, Math.max(0, w - inset * 2), Math.max(0, h - inset * 2), Math.max(0, (element.radius ?? 0) * unit - inset));
    context.stroke();
  }
  context.restore();
  return ResultExtensions.ok(undefined);
}
/** Renders the same normalized millimetre geometry used by the editable preview. */
export async function renderDesignPage(page: DesignPage, dpi = 240, imageLoader: typeof loadDesignImage = loadDesignImage): Promise<Result<HTMLCanvasElement, DesignExportFailure>> {
  await document.fonts.ready;
  const unit = Math.min(dpi / 25.4, 8192 / Math.max(page.width, page.height), Math.sqrt(12_000_000 / (page.width * page.height)));
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(page.width * unit); canvas.height = Math.round(page.height * unit);
  const context = canvas.getContext('2d');
  if (!context) return exportFailure('Canvas export is unavailable in this browser.');
  context.fillStyle = page.background; context.fillRect(0, 0, canvas.width, canvas.height);
  for (const element of page.elements) {
    const drawn = await drawElement(context, element, unit, imageLoader);
    if (!drawn.ok) { canvas.width = 0; canvas.height = 0; return drawn; }
  }
  return ResultExtensions.ok(canvas);
}
/** @internal Encodes a rendered page; a browser that cannot encode it is a failed export. */
function canvasBlob(canvas: HTMLCanvasElement, type: string, message: string): Promise<Result<Blob, DesignExportFailure>> {
  return new Promise(resolve => canvas.toBlob(value => resolve(value ? ResultExtensions.ok(value) : exportFailure(message)), type));
}
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob); const link = document.createElement('a');
  link.href = url; link.download = filename; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}
export function safeFilename(title: string): string {
  return title.replace(/[^\p{L}\p{N}\s_-]/gu, '').trim().replace(/\s+/g, '-').slice(0, 100) || 'ocharo-design';
}
/** PDF uses high-resolution page images to retain source scripts and exact image crops. */
export async function exportDesignPdf(snapshot: DesignSnapshot, title: string, progress: (page: number, total: number) => void, pageIds?: readonly string[]): Promise<Result<void, DesignExportFailure>> {
  const checked = selectDesignPages(snapshot, pageIds), first = checked.pages[0]!;
  const loadedImages = new Map<string, Promise<Result<HTMLImageElement, DesignExportFailure>>>();
  const imageLoader: typeof loadDesignImage = src => {
    let image = loadedImages.get(src);
    if (!image) { image = loadDesignImage(src); loadedImages.set(src, image); }
    return image;
  };
  const { jsPDF } = await import('jspdf');
  const pdf = new jsPDF({ orientation: first.width > first.height ? 'landscape' : 'portrait', unit: 'mm', format: [first.width, first.height], compress: true });
  pdf.setProperties({ title, creator: 'Ocharo Brand Workspace' });
  for (let i = 0; i < checked.pages.length; i++) {
    const page = checked.pages[i]!; progress(i + 1, checked.pages.length);
    if (i) pdf.addPage([page.width, page.height], page.width > page.height ? 'landscape' : 'portrait');
    const rendered = await renderDesignPage(page, checked.kind === 'business-card' ? 600 : 240, imageLoader);
    if (!rendered.ok) return rendered;
    const canvas = rendered.value;
    pdf.addImage(canvas.toDataURL('image/jpeg', 0.96), 'JPEG', 0, 0, page.width, page.height);
    canvas.width = 0; canvas.height = 0;
  }
  downloadBlob(pdf.output('blob'), `${safeFilename(title)}${pageIds ? `-pages-${checked.pages.map(page => snapshot.pages.findIndex(item => item.id === page.id) + 1).join('-')}` : ''}.pdf`);
  return ResultExtensions.ok(undefined);
}
export async function exportDesignPng(page: DesignPage, title: string): Promise<Result<void, DesignExportFailure>> {
  const rendered = await renderDesignPage(page, 300);
  if (!rendered.ok) return rendered;
  const canvas = rendered.value;
  const blob = await canvasBlob(canvas, 'image/png', 'PNG export failed.');
  canvas.width = 0; canvas.height = 0;
  if (!blob.ok) return blob;
  downloadBlob(blob.value, `${safeFilename(title)}-${safeFilename(page.title)}.png`);
  return ResultExtensions.ok(undefined);
}


export interface CatalogueRenderBundle { pdf: Blob; pages: { id: string; png: Blob; pdf: Blob }[] }
/** Uses one rendered page for its preview PNG, standalone PDF and place inside the complete PDF. */
export async function renderCatalogueBundle(snapshot: DesignSnapshot, title: string,
  progress: (page: number, total: number) => void, signal?: AbortSignal): Promise<Result<CatalogueRenderBundle, DesignExportFailure>> {
  const checked = selectDesignPages(snapshot), first = checked.pages[0]!;
  if (checked.kind !== 'catalogue') return exportFailure('Only catalogues can become buyer editions.', AppErrorType.Validation);
  const { jsPDF } = await import('jspdf');
  const create = (page: DesignPage) => {
    const pdf = new jsPDF({ orientation: page.width > page.height ? 'landscape' : 'portrait', unit: 'mm', format: [page.width, page.height], compress: true });
    pdf.setProperties({ title, creator: 'Ocharo Catalogue' }); return pdf;
  };
  const full = create(first), pages: CatalogueRenderBundle['pages'] = [], images = new Map<string, Promise<Result<HTMLImageElement, DesignExportFailure>>>();
  const imageLoader: typeof loadDesignImage = src => { let image = images.get(src); if (!image) { image = loadDesignImage(src); images.set(src, image); } return image; };
  for (let index = 0; index < checked.pages.length; index++) {
    signal?.throwIfAborted(); const page = checked.pages[index]!; progress(index + 1, checked.pages.length);
    const rendered = await renderDesignPage(page, 240, imageLoader);
    if (!rendered.ok) return rendered;
    const canvas = rendered.value;
    try {
      signal?.throwIfAborted();
      const encoded = await canvasBlob(canvas, 'image/png', 'Unable to render page image.');
      if (!encoded.ok) return encoded;
      const png = encoded.value;
      const jpeg = canvas.toDataURL('image/jpeg', 0.96), single = create(page);
      single.addImage(jpeg, 'JPEG', 0, 0, page.width, page.height);
      if (index) full.addPage([page.width, page.height], page.width > page.height ? 'landscape' : 'portrait');
      full.addImage(jpeg, 'JPEG', 0, 0, page.width, page.height);
      pages.push({ id: page.id, png, pdf: single.output('blob') });
    } finally { canvas.width = 0; canvas.height = 0; }
  }
  return ResultExtensions.ok({ pdf: full.output('blob'), pages });
}
