import { afterEach, describe, expect, it, vi } from 'vitest';

import { systemApi } from '@/integration/system/SystemApi';
import { createEventsApi } from '@/integration/events/EventsApi';
import { createSourcingApi } from '@/integration/sourcing/SourcingApi';
import { createWorkspaceApi } from '@/integration/common/WorkspaceApi';

afterEach(() => vi.unstubAllGlobals());

const signal = () => new AbortController().signal;
const respond = (body: unknown) => vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(body)));

describe('decoders on the SDK JSON, which leaves null members out', () => {
  it('restores an event\'s omitted members as null', async () => {
    respond({ data: {
      id: 'e1', title: 'Tashkent Fashion Week', scope: 'local', format: 'runway', datePrecision: 'unknown',
      participation: '', status: 'shortlisted', priority: 'normal', notes: '', revision: 1,
      createdAt: '2026-10-08T00:00:00Z', updatedAt: '2026-10-08T00:00:00Z',
    } });
    const result = await createEventsApi(() => 'token').getEvent('org', 'e1', signal());
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value).toMatchObject({ key: null, startsOn: null, cost: null, sourceUrl: null });
  });

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

  it('reads a waiting worker without a heartbeat timestamp', async () => {
    respond({ service: 'brand-workspace', stage: 'foundation', persistence: 'PostgreSQL', worker: { state: 'waiting' } });
    const result = await systemApi.getStatus(signal());
    expect(result).toEqual({ ok: true, value: expect.objectContaining({ worker: { state: 'waiting', lastSeenAtUtc: null } }) });
  });
});
