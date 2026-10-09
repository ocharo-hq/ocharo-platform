import { afterEach, describe, expect, it, vi } from 'vitest';
import { memoryStorageBroker, type StorageBroker } from '@wow-two-beta/ui-vue/foundation/storage';
import { defaultSourcingPreferences, orderedSourcingSections } from '@/domain/workspace/SourcingPreferences';
import { createSourcingPreferencesStorage } from '@/integration/workspace/SourcingPreferencesStorage';
import { createSourcingApi } from '@/integration/workspace/SourcingApi';

const userId = '11111111-1111-4111-8111-111111111111';
const organizationId = '22222222-2222-4222-8222-222222222222';
const other = '33333333-3333-4333-8333-333333333333';
const scope = { userId, organizationId };
const customized = { start: 'materials', order: 'contacts-first', visible: ['contacts', 'media'] } as const;
afterEach(() => vi.unstubAllGlobals());

describe('personal sourcing preferences', () => {
  it('isolates users and organizations and saves only known presentation fields', () => {
    const broker = memoryStorageBroker();
    const store = createSourcingPreferencesStorage(scope, broker);
    expect(store.save({ ...customized, visible: [...customized.visible], supplierNotes: 'private' } as unknown as ReturnType<typeof defaultSourcingPreferences>)).toBe(true);
    expect(store.read()).toEqual(customized);
    expect(createSourcingPreferencesStorage({ userId: other, organizationId }, broker).read()).toEqual(defaultSourcingPreferences());
    expect(createSourcingPreferencesStorage({ userId, organizationId: other }, broker).read()).toEqual(defaultSourcingPreferences());
    const persisted = broker.read(`ocharo.sourcing-view.v1:${userId}:${organizationId}`);
    expect(JSON.stringify(persisted)).not.toContain('private');
    expect(orderedSourcingSections(store.read())).toEqual(['contacts', 'media']);
  });
  it('rejects malformed, future-version and cross-scope payloads', () => {
    const broker = memoryStorageBroker();
    const key = `ocharo.sourcing-view.v1:${userId}:${organizationId}`;
    const store = createSourcingPreferencesStorage(scope, broker);
    for (const value of [null, 'wrong', { schemaVersion: 2, ...scope, preferences: customized },
      { schemaVersion: 1, userId: other, organizationId, preferences: customized },
      { schemaVersion: 1, ...scope, preferences: { ...customized, visible: ['contacts', 'contacts'] } },
      { schemaVersion: 1, ...scope, preferences: { ...customized, visible: ['private-data'] } }]) {
      broker.write(key, value);
      expect(store.read()).toEqual(defaultSourcingPreferences());
    }
    expect(createSourcingPreferencesStorage({ userId: '', organizationId }, broker).save(defaultSourcingPreferences())).toBe(false);
  });
  it('does not acknowledge blocked writes even when old values happen to match', () => {
    const memory = memoryStorageBroker();
    const storage = createSourcingPreferencesStorage(scope, memory);
    expect(storage.save(defaultSourcingPreferences())).toBe(true);
    const blocked: StorageBroker = { read: memory.read, write: () => {}, remove: () => {} };
    expect(createSourcingPreferencesStorage(scope, blocked).save(defaultSourcingPreferences())).toBe(false);
  });
});

describe('all-materials transport', () => {
  it('keeps all, inbox and supplier requests distinct', async () => {
    const fetch = vi.fn().mockImplementation(() => Promise.resolve(Response.json({ data: [] })));
    vi.stubGlobal('fetch', fetch);
    const api = createSourcingApi(() => '', () => {});
    for (const filter of [{ kind: 'all' }, { kind: 'inbox' }, { kind: 'supplier', supplierId: other }] as const) {
      expect((await api.listMedia(organizationId, filter, { limit: 100, offset: 0 }, new AbortController().signal)).ok).toBe(true);
    }
    const urls = fetch.mock.calls.map(([request]) => new URL(typeof request === 'string' ? request : request.url, 'https://desk.test'));
    expect(urls[0]!.searchParams.has('unassigned')).toBe(false);
    expect(urls[0]!.searchParams.has('supplierId')).toBe(false);
    expect(urls[1]!.searchParams.get('unassigned')).toBe('true');
    expect(urls[2]!.searchParams.get('supplierId')).toBe(other);
  });
});
