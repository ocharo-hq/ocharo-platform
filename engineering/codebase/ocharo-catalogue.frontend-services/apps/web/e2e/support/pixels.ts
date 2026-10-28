import type { BrowserContext } from '@playwright/test';

/** Runs a pixel measurement in a scratch tab, using Chromium's own image decoder and canvas. */
async function inScratchTab<T, A>(context: BrowserContext, action: (args: A) => Promise<T>, args: A): Promise<T> {
  const tab = await context.newPage();
  try {
    return await tab.evaluate(action as never, args as never);
  } finally {
    await tab.close();
  }
}
const dataUrl = (png: Buffer, type = 'image/png') => `data:${type};base64,${png.toString('base64')}`;

/**
 * Mean absolute RGB difference (0–255) after scaling both images to the same small width. Two renderings of the same
 * page differ by a few points (JPEG and anti-aliasing); a different page or a blank render differs by tens.
 */
export function imageDifference(context: BrowserContext, first: Buffer, second: Buffer, width = 160): Promise<number> {
  return inScratchTab(
    context,
    async ({ a, b, width }) => {
      const load = async (source: string) => {
        const image = new Image();
        image.src = source;
        await image.decode();
        return image;
      };
      const [left, right] = await Promise.all([load(a), load(b)]);
      const height = Math.round((left.height / left.width) * width);
      const pixels = (image: HTMLImageElement) => {
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext('2d')!;
        context.drawImage(image, 0, 0, width, height);
        return context.getImageData(0, 0, width, height).data;
      };
      const p = pixels(left),
        q = pixels(right);
      let total = 0;
      for (let i = 0; i < p.length; i += 4)
        total += (Math.abs(p[i]! - q[i]!) + Math.abs(p[i + 1]! - q[i + 1]!) + Math.abs(p[i + 2]! - q[i + 2]!)) / 3;
      return total / (p.length / 4);
    },
    { a: dataUrl(first), b: dataUrl(second), width },
  );
}

export interface InkBounds {
  left: number;
  right: number;
  top: number;
  bottom: number;
  /** Share of dark pixels in the region; zero means nothing was drawn there. */
  coverage: number;
}
/**
 * Where dark "ink" pixels fall inside a region given as fractions of the image. Used to tell whether text was drawn
 * at all and which side of its frame it is aligned to.
 */
export function inkBounds(
  context: BrowserContext,
  png: Buffer,
  region: { x: number; y: number; width: number; height: number },
  threshold = 110,
): Promise<InkBounds> {
  return inScratchTab(
    context,
    async ({ source, region, threshold }) => {
      const image = new Image();
      image.src = source;
      await image.decode();
      const x = Math.round(region.x * image.width),
        y = Math.round(region.y * image.height),
        w = Math.max(1, Math.round(region.width * image.width)),
        h = Math.max(1, Math.round(region.height * image.height));
      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      const context = canvas.getContext('2d')!;
      context.drawImage(image, x, y, w, h, 0, 0, w, h);
      const data = context.getImageData(0, 0, w, h).data;
      let left = w,
        right = -1,
        top = h,
        bottom = -1,
        ink = 0;
      for (let row = 0; row < h; row++)
        for (let column = 0; column < w; column++) {
          const i = (row * w + column) * 4;
          if ((data[i]! + data[i + 1]! + data[i + 2]!) / 3 < threshold) {
            ink++;
            left = Math.min(left, column);
            right = Math.max(right, column);
            top = Math.min(top, row);
            bottom = Math.max(bottom, row);
          }
        }
      return ink
        ? { left: left / w, right: (right + 1) / w, top: top / h, bottom: (bottom + 1) / h, coverage: ink / (w * h) }
        : { left: 0, right: 0, top: 0, bottom: 0, coverage: 0 };
    },
    { source: dataUrl(png), region, threshold },
  );
}

/** Converts a PNG to a compact JPEG for evidence kept in the repository. */
export function toJpeg(context: BrowserContext, png: Buffer, maxWidth = 900, quality = 0.72): Promise<Buffer> {
  return inScratchTab(
    context,
    async ({ source, maxWidth, quality }) => {
      const image = new Image();
      image.src = source;
      await image.decode();
      const scale = Math.min(1, maxWidth / image.width);
      const canvas = document.createElement('canvas');
      canvas.width = Math.round(image.width * scale);
      canvas.height = Math.round(image.height * scale);
      canvas.getContext('2d')!.drawImage(image, 0, 0, canvas.width, canvas.height);
      return canvas.toDataURL('image/jpeg', quality).split(',')[1]!;
    },
    { source: dataUrl(png), maxWidth, quality },
  ).then((base64) => Buffer.from(base64, 'base64'));
}
