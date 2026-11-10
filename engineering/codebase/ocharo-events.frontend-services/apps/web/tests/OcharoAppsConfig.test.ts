import { describe, expect, it } from 'vitest';
import { staticSource } from '@wow-two-beta/fe/foundation/config';

import { resolveOcharoAppsConfig, runtimeConfigSource } from '../src/application/shared/UseOcharoApps';
import { ocharoApps } from '../src/domain/shared/OcharoApps';

describe('the Events entry of the Ocharo apps menu', () => {
  it('defaults to the local-run address', () => {
    expect(resolveOcharoAppsConfig([]).OCHARO_EVENTS_URL).toBe('https://localhost:8292/');
  });

  it('reads OCHARO_EVENTS_URL from the runtime settings before VITE_OCHARO_EVENTS_URL from the build', () => {
    const config = resolveOcharoAppsConfig([
      runtimeConfigSource({ OCHARO_EVENTS_URL: 'https://events.example.test/' }),
      staticSource({ VITE_OCHARO_EVENTS_URL: 'https://build.example.test/' }),
    ]);
    expect(config.OCHARO_EVENTS_URL).toBe('https://events.example.test/');
    expect(
      resolveOcharoAppsConfig([staticSource({ VITE_OCHARO_EVENTS_URL: 'https://build.example.test/' })])
        .OCHARO_EVENTS_URL,
    ).toBe('https://build.example.test/');
  });

  it('is an app with its summary and sits after Brand, and a missing address leaves it out', () => {
    const apps = ocharoApps({ brand: 'https://brand.example.test/', events: 'https://events.example.test/' });
    expect(apps.map((app) => app.key)).toEqual(['brand', 'events']);
    expect(apps[1]).toMatchObject({
      label: 'Events',
      summary: 'Trade events and deadlines',
      kind: 'workspace',
      url: 'https://events.example.test/',
    });
    expect(ocharoApps({ brand: 'https://brand.example.test/' }).map((app) => app.key)).toEqual(['brand']);
    expect(ocharoApps({ events: 'http://insecure.example.test/' })).toEqual([]);
  });
});
