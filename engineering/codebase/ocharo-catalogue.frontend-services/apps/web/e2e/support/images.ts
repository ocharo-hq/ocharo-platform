import { crc32, deflateSync } from 'node:zlib';

/** One PNG chunk: length, type, data and the CRC of type plus data. */
function chunk(type: string, data: Buffer): Buffer {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(body) >>> 0);
  return Buffer.concat([length, body, checksum]);
}

export type Rgb = readonly [number, number, number];

/**
 * A synthetic garment photograph: a solid field with a darker centre band, so every seeded image has its own bytes
 * (and media hash) and stays recognisable in rendered pages. No real product photography is used.
 */
export function syntheticPhoto(color: Rgb, width = 240, height = 320): Buffer {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header.set([8, 2, 0, 0, 0], 8);
  const row = 1 + width * 3,
    pixels = Buffer.alloc(row * height);
  for (let y = 0; y < height; y++) {
    const band = y > height * 0.35 && y < height * 0.65;
    for (let x = 0; x < width; x++) {
      const offset = y * row + 1 + x * 3;
      const shade = band && x > width * 0.2 && x < width * 0.8 ? 0.55 : 1;
      pixels[offset] = Math.round(color[0] * shade);
      pixels[offset + 1] = Math.round(color[1] * shade);
      pixels[offset + 2] = Math.round(color[2] * shade);
    }
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(pixels)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/** Reads width and height from a PNG header. */
export function pngSize(bytes: Buffer): { width: number; height: number } {
  if (bytes.subarray(1, 4).toString('ascii') !== 'PNG') throw new Error('Not a PNG file.');
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}
