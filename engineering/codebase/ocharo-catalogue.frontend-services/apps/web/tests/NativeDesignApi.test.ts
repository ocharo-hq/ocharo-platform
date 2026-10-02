import { describe, it, expect, vi } from 'vitest';
import { createDesignApi } from '../src/integration/design/DesignApi';
import { documentFixture } from './NativeDesignFixture';
const reply = (status: number, value: unknown) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
describe('native catalogue design API', () => {
  it('uses the local catalogue endpoint and optimistic revision', async () => {
    const value = documentFixture(),
      fetch = vi.fn(async () => reply(200, value)),
      api = createDesignApi(fetch as typeof globalThis.fetch);
    await api.save(value.id, value.title, value.snapshot, 2);
    const [url, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/catalogue/designs/' + value.id);
    expect(JSON.parse(String(init.body)).expectedRevision).toBe(2);
    expect(String(init.body)).not.toContain('organizationId');
  });
  it('surfaces a conflict without retrying or overwriting', async () => {
    const fetch = vi.fn(async () => reply(409, { detail: 'A newer revision exists.' })),
      api = createDesignApi(fetch as typeof globalThis.fetch),
      value = documentFixture();
    await expect(api.save(value.id, value.title, value.snapshot, 2)).rejects.toThrow(
      'This item changed elsewhere. Reload before saving.',
    );
    expect(fetch).toHaveBeenCalledTimes(1);
  });
  it('sends fingerprint and exact page artifact filenames', async () => {
    const fetch = vi.fn(async () => reply(200, { id: 'release' })),
      api = createDesignApi(fetch as typeof globalThis.fetch);
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
    const fetch = vi.fn(async () => reply(200, { id: 'release' })),
      api = createDesignApi(fetch as typeof globalThis.fetch);
    await api.prepare('design', { expectedRevision: 2, fingerprint: 'abc' });
    const [, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(JSON.parse(String(init.body))).toEqual({ expectedRevision: 2, fingerprint: 'abc' });
  });
  it('rejects malformed snapshots received from the service', async () => {
    const fetch = vi.fn(async () => reply(200, { ...documentFixture(), snapshot: { kind: 'catalogue' } }));
    await expect(createDesignApi(fetch as typeof globalThis.fetch).get('design')).rejects.toThrow();
  });
});
