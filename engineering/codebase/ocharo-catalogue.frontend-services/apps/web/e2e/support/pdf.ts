import { execFile } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { promisify } from 'node:util';
import { PDFDocument } from 'pdf-lib';

const run = promisify(execFile);
const millimetres = (points: number) => (points * 25.4) / 72;

export interface PdfInspection {
  pageCount: number;
  /** Page sizes in millimetres, rounded to 0.1 mm. */
  pages: { widthMm: number; heightMm: number; images: number }[];
  title: string | undefined;
  creator: string | undefined;
  /** Text the PDF carries as text; empty when every page is a raster image. */
  text: string[];
}

/** Opens a PDF with pdf-lib (structure, sizes, metadata) and pdf.js (text layer). */
export async function inspectPdf(bytes: Buffer): Promise<PdfInspection> {
  const document = await PDFDocument.load(bytes, { updateMetadata: false });
  const pdfjs = await import('pdfjs-dist/legacy/build/pdf.mjs');
  const task = pdfjs.getDocument({ data: new Uint8Array(bytes), verbosity: 0 }),
    loaded = await task.promise;
  const text: string[] = [],
    images: number[] = [];
  for (let number = 1; number <= loaded.numPages; number++) {
    const page = await loaded.getPage(number);
    const content = await page.getTextContent();
    text.push(
      content.items
        .map((item) => ('str' in item ? item.str : ''))
        .join(' ')
        .trim(),
    );
    const operators = await page.getOperatorList();
    images.push(operators.fnArray.filter((code) => code === pdfjs.OPS.paintImageXObject).length);
  }
  await task.destroy();
  const pages = document.getPages().map((page, index) => {
    const { width, height } = page.getSize();
    return {
      widthMm: Math.round(millimetres(width) * 10) / 10,
      heightMm: Math.round(millimetres(height) * 10) / 10,
      images: images[index] ?? 0,
    };
  });
  return {
    pageCount: document.getPageCount(),
    pages,
    title: document.getTitle(),
    creator: document.getCreator(),
    text,
  };
}

/** Renders every page with pdf.js, the way a PDF viewer shows it, into PNG images. */
export async function renderPdf(bytes: Buffer, scale = 1.5): Promise<Buffer[]> {
  const pdfjs = await import('pdfjs-dist/legacy/build/pdf.mjs');
  const task = pdfjs.getDocument({ data: new Uint8Array(bytes), verbosity: 0 }),
    loaded = await task.promise;
  const images: Buffer[] = [];
  for (let number = 1; number <= loaded.numPages; number++) {
    const page = await loaded.getPage(number);
    const viewport = page.getViewport({ scale });
    const factory = (loaded as unknown as { canvasFactory: CanvasFactory }).canvasFactory;
    const { canvas, context } = factory.create(Math.ceil(viewport.width), Math.ceil(viewport.height));
    await page.render({ canvas, canvasContext: context, viewport } as never).promise;
    images.push(canvas.toBuffer('image/png'));
    factory.destroy({ canvas, context });
  }
  await task.destroy();
  return images;
}
interface CanvasFactory {
  create(width: number, height: number): { canvas: { toBuffer(type: string): Buffer }; context: unknown };
  destroy(value: unknown): void;
}

let tesseract: Promise<string[] | null> | undefined;
/** Installed OCR languages, or null when the optional `tesseract` command is not available. */
export function ocrLanguages(): Promise<string[] | null> {
  tesseract ??= run('tesseract', ['--list-langs'])
    .then(({ stdout }) =>
      stdout
        .split('\n')
        .slice(1)
        .map((line) => line.trim())
        .filter(Boolean),
    )
    .catch(() => null);
  return tesseract;
}

/** Reads text from a rendered page image with tesseract; the exported PDF carries its text only as pixels. */
export async function ocr(image: Buffer, language: string): Promise<string> {
  const directory = await mkdtemp(join(tmpdir(), 'catalogue-ocr-'));
  try {
    const file = join(directory, 'page.png');
    await writeFile(file, image);
    const { stdout } = await run('tesseract', [file, 'stdout', '-l', language, '--psm', '3'], {
      maxBuffer: 8 * 1024 * 1024,
    });
    return stdout;
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}

export const readDownload = async (path: string | null) => {
  if (!path) throw new Error('The download has no local file.');
  return readFile(path);
};
