import { describe, expect, it, vi } from 'vitest';
import { createCatalogueApi, describeFailure } from '../src/integration/catalogue';

function reply(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
}

describe('catalogue api', () => {
  it('surfaces ProblemDetails detail on a revision conflict', async () => {
    const fetch = vi.fn(async () =>
      reply(409, {
        title: 'Catalogue command rejected',
        detail: 'The product was saved elsewhere (now revision 3). Reload before saving.',
      }),
    );
    const api = createCatalogueApi(fetch as unknown as typeof globalThis.fetch);
    const result = await api.updateProduct('nimcha-v06', {
      garmentType: 'nimcha',
      status: 'draft',
      sortOrder: 0,
      sizes: [],
      fabricCodes: [],
      decorationCodes: [],
      tags: [],
      locales: {
        uz: { status: 'draft', name: null, fabric: null, decoration: null, description: null },
        ru: { status: 'draft', name: null, fabric: null, decoration: null, description: null },
        en: { status: 'draft', name: null, fabric: null, decoration: null, description: null },
      },
      pricing: null,
      issues: [],
      notes: null,
      expectedRevision: 2,
    });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.failure.status).toBe(409);
      expect(describeFailure(result.failure)).toMatch(/revision 3/);
    }
  });

  it('rejects a non-array product list as a protocol failure', async () => {
    const api = createCatalogueApi((async () =>
      reply(200, { unexpected: true })) as unknown as typeof globalThis.fetch);
    const result = await api.listProducts({});
    expect(result.ok).toBe(false);
  });

  it('sends filters as query parameters', async () => {
    const fetch = vi.fn(async (input: RequestInfo | URL) => (void input, reply(200, [])));
    const api = createCatalogueApi(fetch as unknown as typeof globalThis.fetch);
    await api.listProducts({ q: 'nimcha', status: 'active' });
    const url = String(fetch.mock.calls[0]?.[0]);
    expect(url).toContain('q=nimcha');
    expect(url).toContain('status=active');
  });
});
