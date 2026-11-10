import { afterEach, describe, expect, it, vi } from 'vitest';

import { systemApi } from '@/integration/system/SystemApi';
import { createSourcingApi } from '@/integration/sourcing/SourcingApi';
import { createWorkspaceApi } from '@/integration/common/WorkspaceApi';
import { createCommerceApi } from '@/integration/common/CommerceApi';

afterEach(() => vi.unstubAllGlobals());

const signal = () => new AbortController().signal;
const respond = (body: unknown) => vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(body)));

describe('decoders on the SDK JSON, which leaves null members out', () => {
  it('restores a supplier\'s omitted members, its people and routes included', async () => {
    respond({ data: {
      id: 's1', name: 'Kashmir Weaves', stage: 'quoted', notes: '', revision: 1,
      createdAt: '2026-10-08T00:00:00Z', updatedAt: '2026-10-08T00:00:00Z',
      people: [{ id: 'p1', supplierId: 's1', name: 'Navit', notes: '', revision: 1, createdAt: 'x', updatedAt: 'x' }],
      routes: [{ id: 'r1', supplierId: 's1', channel: 'whatsapp', destination: '+91', verified: false, isPrimary: true,
        notes: '', revision: 1, createdAt: 'x', updatedAt: 'x' }],
    } });
    const result = await createSourcingApi(() => 'token').getSupplier('org', 's1', signal());
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.value).toMatchObject({ key: null, country: null, city: null, website: null });
      expect(result.value.people[0]?.role).toBeNull();
      expect(result.value.routes[0]).toMatchObject({ personId: null, label: null, sourceUrl: null });
    }
  });

  it('restores a draft\'s omitted review state', async () => {
    respond({ data: {
      id: 'd1', brandId: 'b1', title: 'Launch', revision: 2, status: 'draft', updatedAtUtc: '2026-10-08T00:00:00Z',
      caption: '', language: 'uz', destination: 'telegram', sourceNotes: '', media: [],
    } });
    const result = await createWorkspaceApi(() => 'token').getDraft('org', 'd1', signal());
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value).toMatchObject({ requestedReviewRevision: null, approval: null });
  });

  it('reads a commerce draft left out before its first save as none', async () => {
    respond({});
    const result = await createCommerceApi(() => 'token').get('org', 'b1', signal());
    expect(result).toEqual({ ok: true, value: null });
  });

  it('refuses a commerce answer without its envelope', async () => {
    respond([]);
    const result = await createCommerceApi(() => 'token').get('org', 'b1', signal());
    expect(result.ok).toBe(false);
  });

  it('restores a commerce draft\'s omitted members, its products and editions included', async () => {
    respond({ data: {
      brandId: 'b1', revision: 2, updatedAt: '2026-11-09T00:00:00Z',
      snapshot: {
        slug: 'ocharo', identity: { name: 'Ocharo', tagline: { uz: 'Libos' }, descriptor: {}, palette: [],
          links: [{ key: 'instagram', label: 'Instagram', url: 'https://instagram.com/ocharo' }] },
        logos: { master: { id: 'l1', sha256: 'a'.repeat(64) } },
        products: [{ revision: 1, document: {
          id: 'vest', locales: { uz: { status: 'approved', name: 'Nimcha' }, ru: { status: 'draft' }, en: { status: 'draft' } },
          pricing: { currency: 'UZS', status: 'confirmed', tiers: [{ minimumQuantity: 1, unitPrice: 100 }] },
          media: [{ id: 'm1', role: 'cutout', sha256: 'b'.repeat(64) }],
        } }],
        collections: [{ id: 'spring', names: { uz: 'Bahor' }, productIds: [] }],
        mediaApprovals: { ['b'.repeat(64)]: { kind: 'photo', approvedForPublication: true } },
        catalogueEditions: [{ id: 'e1', pages: [] }],
        verifiedLinkKeys: [], sources: [],
      },
    } });
    const result = await createCommerceApi(() => 'token').get('org', 'b1', signal());
    expect(result.ok).toBe(true);
    if (!result.ok || result.value === null) return;
    const snapshot = result.value.snapshot;
    expect(snapshot.identity.tagline).toEqual({ uz: 'Libos', ru: null, en: null });
    expect(snapshot.identity.links[0]?.handle).toBeNull();
    expect(snapshot.logos.master).toMatchObject({ width: null, height: null, sourcePath: null });
    const [product] = snapshot.products;
    expect(product).toMatchObject({ sourceRevision: null, document: { notes: null, stock: null } });
    expect(product?.document.locales.ru).toEqual({ status: 'draft', name: null, fabric: null, decoration: null, description: null });
    expect(product?.document.pricing).toMatchObject({ wording: null, tiers: [{ maximumQuantity: null }] });
    expect(product?.document.media[0]).toMatchObject({ width: null, alt: null, sourcePath: null });
    expect(snapshot.collections[0]).toMatchObject({ description: null, names: { ru: null, en: null } });
    expect(snapshot.mediaApprovals['b'.repeat(64)]).toMatchObject({ sourceUrl: null, credit: null, rights: null });
    expect(snapshot.catalogueEditions?.[0]?.pdfSha256).toBeNull();
  });

  it('restores a release\'s omitted withdrawal', async () => {
    respond({ data: [{ id: 'r1', brandId: 'b1', revision: 1, sourceRevision: 1, publishedAt: 'x', note: '', snapshot: {
      slug: 'ocharo', identity: { name: 'Ocharo', tagline: {}, descriptor: {}, palette: [], links: [] }, logos: {},
      products: [], collections: [], mediaApprovals: {}, verifiedLinkKeys: [], sources: [],
    } }] });
    const result = await createCommerceApi(() => 'token').releases('org', 'b1', signal());
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value[0]?.withdrawnAt).toBeNull();
  });

  it('reads a waiting worker without a heartbeat timestamp', async () => {
    respond({ service: 'brand-workspace', stage: 'foundation', persistence: 'PostgreSQL', worker: { state: 'waiting' } });
    const result = await systemApi.getStatus(signal());
    expect(result).toEqual({ ok: true, value: expect.objectContaining({ worker: { state: 'waiting', lastSeenAtUtc: null } }) });
  });
});
