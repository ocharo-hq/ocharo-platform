import { describe, it, expect, vi } from 'vitest';
import { describeFailure } from '../src/integration/catalogue';
import { createDesignApi } from '../src/integration/design/DesignApi';
import { fetchDesignImage } from '../src/integration/design/DesignExport';
import { documentFixture } from './NativeDesignFixture';
import { httpOver } from './helpers/TestHttp';
const reply = (status: number, value: unknown) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
describe('native catalogue design API', () => {
  it('uses the local catalogue endpoint and optimistic revision', async () => {
    const value = documentFixture(),
      fetch = vi.fn(async () => reply(200, { data: value })),
      api = createDesignApi(httpOver(fetch as typeof globalThis.fetch));
    await api.save(value.id, value.title, value.snapshot, 2);
    const [url, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/catalogue/designs/' + value.id);
    expect(JSON.parse(String(init.body)).expectedRevision).toBe(2);
    expect(String(init.body)).not.toContain('organizationId');
  });
  it('surfaces a conflict without retrying or overwriting', async () => {
    const fetch = vi.fn(async () => reply(409, { detail: 'A newer revision exists.' })),
      api = createDesignApi(httpOver(fetch as typeof globalThis.fetch)),
      value = documentFixture();
    const saved = await api.save(value.id, value.title, value.snapshot, 2);
    expect(saved.ok).toBe(false);
    if (!saved.ok) {
      expect(saved.failure.status).toBe(409);
      expect(describeFailure(saved.failure)).toBe('This item changed elsewhere. Reload before saving.');
    }
    expect(fetch).toHaveBeenCalledTimes(1);
  });
  it('sends fingerprint and exact page artifact filenames', async () => {
    const fetch = vi.fn(async () => reply(200, { data: { id: 'release' } })),
      api = createDesignApi(httpOver(fetch as typeof globalThis.fetch));
    await api.prepare(
      'design',
      { expectedRevision: 2, fingerprint: 'abc' },
      { pdf: new Blob(['pdf']), pages: [{ id: 'cover', pdf: new Blob(['pdf']), png: new Blob(['png']) }] },
    );
    const [, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    const form = init.body as FormData;
    expect(JSON.parse(form.get('metadata') as string)).toEqual({ expectedRevision: 2, fingerprint: 'abc' });
    expect(form.getAll('files').map((file) => (file as File).name)).toEqual([
      'catalogue.pdf',
      'page-cover.pdf',
      'page-cover.png',
    ]);
  });
  it('sends metadata without files for a web-only edition', async () => {
    const fetch = vi.fn(async () => reply(200, { data: { id: 'release' } })),
      api = createDesignApi(httpOver(fetch as typeof globalThis.fetch));
    await api.prepare('design', { expectedRevision: 2, fingerprint: 'abc' });
    const [, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(JSON.parse(String(init.body))).toEqual({ expectedRevision: 2, fingerprint: 'abc' });
  });
  it('rejects malformed snapshots received from the service', async () => {
    const fetch = vi.fn(async () => reply(200, { data: { ...documentFixture(), snapshot: { kind: 'catalogue' } } }));
    const read = await createDesignApi(httpOver(fetch as typeof globalThis.fetch)).get('design');
    expect(read.ok).toBe(false);
    if (!read.ok) expect(read.failure.code).toBe('protocol');
  });
  it('reads design images through the SDK client and refuses remote sources and other types', async () => {
    const media = '/api/catalogue/media/' + 'a'.repeat(64);
    const image = vi.fn(async () => new Response(new Blob(['png'], { type: 'image/png' }), { status: 200 }));
    const read = await fetchDesignImage(media, undefined, httpOver(image as unknown as typeof globalThis.fetch));
    expect(read.ok && read.value.type).toBe('image/png');
    expect(String((image.mock.calls[0] as unknown as [string])[0])).toBe(media);

    const remote = await fetchDesignImage('https://example.test/a.png', undefined, httpOver(image as never));
    expect(!remote.ok && remote.failure.code).toBe('validation');
    expect(image).toHaveBeenCalledTimes(1);

    const html = vi.fn(async () => new Response('<p>', { status: 200, headers: { 'content-type': 'text/html' } }));
    const other = await fetchDesignImage(media, undefined, httpOver(html as unknown as typeof globalThis.fetch));
    expect(!other.ok && other.failure.code).toBe('protocol');
  });
});
