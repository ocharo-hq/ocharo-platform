import { describe, expect, it, vi } from 'vitest';
import { createApiClient, wowTwoEnvelope } from '@wow-two-beta/fe/foundation/http';
import { createBrandApi, describeFailure } from '../src/integration/brand';

/** Brand's endpoints over the SDK client, as `brandHttp` builds it, on a mocked transport. */
function brandApiOver(fetch: unknown) {
  return createBrandApi(
    createApiClient({ envelope: wowTwoEnvelope, retry: false, fetch: fetch as typeof globalThis.fetch }),
  );
}

function reply(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
}

describe('brand api', () => {
  it('maps publish conflict to safe localized feedback', async () => {
    const api = brandApiOver(async () => reply(409, { detail: 'Nothing changed since the last release.' }));
    const result = await api.publish(3, null);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.failure.status).toBe(409);
      expect(describeFailure(result.failure)).not.toContain('Nothing changed');
      expect(describeFailure(result.failure)).not.toContain('409');
    }
  });

  it('sends the draft revision with a current-logo change', async () => {
    const fetch = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => (
        void input,
        void init,
        reply(200, { data: { draft: {}, revision: 5 } })
      ),
    );
    const api = brandApiOver(fetch);
    await api.setCurrent('on-light', 4, 'abc');
    const [url, init] = fetch.mock.calls[0]!;
    expect(String(url)).toContain('/api/brand/logos/on-light/current');
    expect(JSON.parse(String(init?.body))).toEqual({ expectedRevision: 4, versionId: 'abc' });
  });

  it('reads the private API from its data envelope and the public kit bare', async () => {
    const fetch = vi.fn(async (input: RequestInfo | URL) =>
      String(input).startsWith('/api/public/')
        ? reply(200, { name: 'Ocharo', release: 2, logos: {} })
        : reply(200, { data: { revision: 7, logos: [] } }),
    );
    const api = brandApiOver(fetch);
    const draft = await api.draft();
    const kit = await api.publicKit();
    expect(draft.ok && draft.value.revision).toBe(7);
    expect(kit.ok && kit.value.release).toBe(2);
  });

  it('uploads multipart and keeps a refused image on the generic failure copy', async () => {
    const fetch = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => (
        void input,
        void init,
        reply(415, {
          status: 415,
          code: 'Validation',
          brandCode: 'image_type_unsupported',
          detail: 'Only PNG, JPEG and WebP.',
        })
      ),
    );
    const api = brandApiOver(fetch);
    const result = await api.upload('on-light', new File(['x'], 'notes.txt', { type: 'text/plain' }), 'note');
    const [url, init] = fetch.mock.calls[0]!;
    expect(String(url)).toContain('/api/brand/logos/on-light');
    expect(init?.body).toBeInstanceOf(FormData);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.failure.status).toBe(415);
      expect(result.failure.problem?.['brandCode']).toBe('image_type_unsupported');
      expect(describeFailure(result.failure)).toBe(
        'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.',
      );
    }
  });
});
