import { describe, expect, it, vi } from 'vitest';
import { createBrandApi, describeFailure } from '../src/integration/brand';

function reply(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
}

describe('brand api', () => {
  it('maps publish conflict to safe localized feedback', async () => {
    const api = createBrandApi((async () =>
      reply(409, { detail: 'Nothing changed since the last release.' })) as unknown as typeof fetch);
    const result = await api.publish(3, null);
    expect(result.ok).toBe(false);
    if (!result.ok) { expect(result.failure.status).toBe(409); expect(describeFailure(result.failure)).not.toContain('Nothing changed'); expect(describeFailure(result.failure)).not.toContain('409'); }
  });

  it('sends the draft revision with a current-logo change', async () => {
    const fetch = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => (
        void input,
        void init,
        reply(200, { draft: {}, revision: 5 })
      ),
    );
    const api = createBrandApi(fetch as unknown as typeof globalThis.fetch);
    await api.setCurrent('on-light', 4, 'abc');
    const [url, init] = fetch.mock.calls[0]!;
    expect(String(url)).toContain('/api/brand/logos/on-light/current');
    expect(JSON.parse(String(init?.body))).toEqual({ expectedRevision: 4, versionId: 'abc' });
  });
});
