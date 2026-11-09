import { describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { translateUi } from '../src/domain/shared/UiMessages';
import { createCatalogueApi, describeFailure } from '../src/integration/catalogue';

function reply(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
}

describe('catalogue api', () => {
  it('preserves ProblemDetails diagnostics while showing safe localized revision conflict copy', async () => {
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
      expect(result.failure.problem?.['detail']).toMatch(/revision 3/);
      expect(describeFailure(result.failure)).toBe('This item changed elsewhere. Reload before saving.');
      const translate = vi.fn((source: string) => `translated: ${source}`);
      expect(describeFailure(result.failure, translate)).toBe(
        'translated: This item changed elsewhere. Reload before saving.',
      );
      expect(translate).toHaveBeenCalled();
    }
  });

  it.each([
    {
      code: 'protocol' as const,
      uz: 'Amalni bajarib bo‘lmadi.',
      ru: 'Не удалось выполнить операцию.',
    },
    {
      code: 'timeout' as const,
      uz: 'Amal uchun ajratilgan vaqt tugadi.',
      ru: 'Время выполнения операции истекло.',
    },
    {
      code: 'cancelled' as const,
      uz: 'Amal bekor qilindi.',
      ru: 'Операция отменена.',
    },
  ])('localizes the safe SDK $code fallback', ({ code, uz, ru }) => {
    const failure = ApiFailureFactory.create(code);
    expect(describeFailure(failure, (source, variables) => translateUi(source, 'uz', variables))).toBe(uz);
    expect(describeFailure(failure, (source, variables) => translateUi(source, 'ru', variables))).toBe(ru);
  });

  it('does not expose server diagnostics from 500 ProblemDetails', () => {
    const failure = ApiFailureFactory.create('http', new Response(null, { status: 500 }), {
      detail: 'SQL private connection diagnostic',
    });
    expect(describeFailure(failure)).toBe('The catalogue service returned error 500.');
  });
  it('rejects a non-array product list as a protocol failure', async () => {
    const api = createCatalogueApi((async () =>
      reply(200, { data: { unexpected: true } })) as unknown as typeof globalThis.fetch);
    const result = await api.listProducts({});
    expect(result.ok).toBe(false);
  });

  it('reads a product from the success envelope with its omitted null fields restored', async () => {
    const locale = { status: 'draft' };
    const api = createCatalogueApi((async () =>
      reply(200, {
        data: {
          document: {
            id: 'nimcha-v01',
            garmentType: 'nimcha',
            variantCode: 'v01',
            status: 'draft',
            sortOrder: 0,
            sizes: ['S', 'XL'],
            fabricCodes: [],
            decorationCodes: [],
            tags: [],
            locales: { uz: { ...locale, name: 'Nimcha' }, ru: locale, en: locale, additional: {} },
            pricing: { currency: 'UZS', status: 'draft', tiers: [{ minimumQuantity: 10, unitPrice: 1 }] },
            media: [],
            issues: [],
            stock: { XL: 2, S: 0 },
            madeToOrder: false,
            availability: 'in-stock',
            availableSizes: ['XL'],
          },
          revision: 1,
          createdAt: '2026-11-09T00:00:00Z',
          updatedAt: '2026-11-09T00:00:00Z',
          media: [],
          collectionIds: [],
        },
      })) as unknown as typeof globalThis.fetch);
    const result = await api.getProduct('nimcha-v01');
    expect(result.ok).toBe(true);
    if (result.ok) {
      const document = result.value.document;
      expect(document.designId).toBeNull();
      expect(document.notes).toBeNull();
      expect(document.source).toBeNull();
      expect(document.locales.ru.name).toBeNull();
      expect(document.pricing?.wording).toBeNull();
      expect(document.pricing?.tiers[0]?.maximumQuantity).toBeNull();
      expect(document.stock).toEqual({ XL: 2, S: 0 });
    }
  });

  it('reads the public feed unenveloped, as Marketing and Blog do', async () => {
    const api = createCatalogueApi((async () =>
      reply(200, { brand: 'Ocharo', products: [], collections: [] })) as unknown as typeof globalThis.fetch);
    const result = await api.publicCatalogue('uz');
    expect(result.ok && result.value.brand).toBe('Ocharo');
  });

  it('sends filters as query parameters', async () => {
    const fetch = vi.fn(async (input: RequestInfo | URL) => (void input, reply(200, { data: [] })));
    const api = createCatalogueApi(fetch as unknown as typeof globalThis.fetch);
    await api.listProducts({ q: 'nimcha', status: 'active' });
    const url = String(fetch.mock.calls[0]?.[0]);
    expect(url).toContain('q=nimcha');
    expect(url).toContain('status=active');
  });
});
