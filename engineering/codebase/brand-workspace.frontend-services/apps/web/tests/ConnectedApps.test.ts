import { describe, expect, it } from 'vitest';
import { connectedApps, connectionUrl } from '@/domain/workspace/ConnectedApps';

describe('built-in connected app destinations', () => {
  it.each([
    ['https://localhost:8340','https://localhost:8340'],
    ['https://localhost:8342','https://localhost:8340'],
    ['https://localhost:8264','https://localhost:8264'],
    ['https://localhost:8378','https://localhost:8264'],
  ])('passes each ticket only in the destination fragment for %s', (deskOrigin, issuer) => {
    for (const app of connectedApps(deskOrigin)) {
      const url = new URL(connectionUrl(app.id,deskOrigin,'ticket-value'));
      expect(url.origin).toBe(new URL(app.url).origin);
      expect(url.search).toBe('');
      expect(url.hash).toContain('ticket-value');
      expect(url.username).toBe('');
    }
    const studio = new URL(connectionUrl('studio',deskOrigin,'ticket-value'));
    expect(new URLSearchParams(studio.hash.slice(1)).get('issuer')).toBe(issuer);
    expect(studio.pathname).toBe('/studio');
    expect(studio.origin).toBe(issuer.endsWith(':8340') ? 'https://localhost:8356' : 'https://localhost:8262');
  });
  it.each(['https://attacker.test','http://localhost:8340','https://localhost:8340.evil.test','https://localhost:8340/path'])('rejects an unknown Platform origin %s', issuer => {
    expect(connectedApps(issuer)).toEqual([]);
    expect(() => connectionUrl('studio',issuer,'secret')).toThrow();
  });
  it('uses the dedicated Marketing port and excludes the unrelated 8242 app', () => {
    expect(new URL(connectionUrl('marketing','https://localhost:8264','ticket')).origin).toBe('https://localhost:8358');
    expect(new URL(connectionUrl('marketing','https://localhost:8340','ticket')).origin).toBe('https://localhost:8344');
    expect(connectedApps('https://localhost:8264').some(app => new URL(app.url).port === '8242')).toBe(false);
  });
  it('rejects unknown destination IDs and oversized tokens', () => {
    expect(() => connectionUrl('https://attacker.test' as 'studio','https://localhost:8340','secret')).toThrow();
    expect(() => connectionUrl('studio','https://localhost:8340','x'.repeat(8193))).toThrow();
  });
});
