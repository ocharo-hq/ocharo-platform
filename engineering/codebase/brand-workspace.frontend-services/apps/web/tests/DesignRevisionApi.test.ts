import { afterEach, describe, expect, it, vi } from 'vitest';
import { createWorkspaceApi } from '@/integration/workspace/WorkspaceApi';
import { isDesignRevision } from '@/integration/workspace/Decoders';
import { createCatalogueTemplate } from '@/domain/workspace/Templates';

afterEach(() => vi.unstubAllGlobals());
const metadata = { revision: 2, title: 'Edition', archived: false, savedBy: 'Maftuna', savedAtUtc: '2026-09-30T10:00:00Z' };
describe('authenticated saved revision reads', () => {
  it('uses organization-scoped encoded GET routes and decodes both metadata and snapshots', async () => {
    const document = { id: 'design/id', organizationId: 'org/id', kind: 'catalogue', title: 'Edition', revision: 2, archived: false,
      createdAtUtc: '2026-09-29T10:00:00Z', updatedAtUtc: metadata.savedAtUtc, snapshot: createCatalogueTemplate('en') };
    const fetch = vi.fn().mockResolvedValueOnce(Response.json({ data: [metadata] })).mockResolvedValueOnce(Response.json({ data: document }));
    vi.stubGlobal('fetch', fetch); const api = createWorkspaceApi(() => ''), controller = new AbortController();
    expect(await api.getDesignRevisions('org/id', 'design/id', controller.signal, 20, 20)).toMatchObject({ ok: true, value: [metadata] });
    expect(await api.getDesignRevision('org/id', 'design/id', 2, controller.signal)).toMatchObject({ ok: true, value: document });
    expect(fetch.mock.calls[0]![0]).toBe('/api/organizations/org%2Fid/design-documents/design%2Fid/revisions?limit=20&offset=20');
    expect(fetch.mock.calls[1]![0]).toBe('/api/organizations/org%2Fid/design-documents/design%2Fid/revisions/2');
    expect(fetch.mock.calls[0]![1]).toMatchObject({ method: 'GET', credentials: 'same-origin' });
    // The SDK links the caller's signal for the request's lifetime; cancellation applies while it is in flight.
    fetch.mockImplementationOnce((_: string, init: RequestInit) => new Promise((_resolve, reject) => init.signal!.addEventListener('abort', () => reject(init.signal!.reason))));
    const pending = api.getDesignRevision('org/id', 'design/id', 2, controller.signal);
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledTimes(3));
    const signal = fetch.mock.calls[2]![1].signal as AbortSignal; controller.abort(); expect(signal.aborted).toBe(true);
    expect(await pending).toMatchObject({ ok: false });
  });
  it('rejects malformed metadata without trusting casts', () => {
    expect(isDesignRevision(metadata)).toBe(true);
    for (const patch of [{ revision: 0 }, { revision: 1.2 }, { archived: 'no' }, { savedAtUtc: 'invalid' }, { savedBy: null }])
      expect(isDesignRevision({ ...metadata, ...patch })).toBe(false);
  });
  it('rejects malformed metadata collections and unsafe historical snapshots', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(Response.json({ data: [{ ...metadata, revision: -1 }] }))
      .mockResolvedValueOnce(Response.json({ data: { id: 'id', organizationId: 'org', title: 'Invalid', kind: 'catalogue', revision: 2,
        archived: false, createdAtUtc: '', updatedAtUtc: '', snapshot: { pages: [] } } })));
    const api = createWorkspaceApi(() => ''), signal = new AbortController().signal;
    expect(await api.getDesignRevisions('org', 'id', signal)).toMatchObject({ ok: false, failure: { code: 'protocol' } });
    expect(await api.getDesignRevision('org', 'id', 2, signal)).toMatchObject({ ok: false, failure: { code: 'protocol' } });
  });
});
