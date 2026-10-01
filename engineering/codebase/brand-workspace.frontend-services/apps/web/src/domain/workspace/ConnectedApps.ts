export type ConnectedAppId = 'studio' | 'marketing' | 'editorial';
export type IntegrationAudience = 'ocharo-studio' | 'ocharo-marketing';
export interface ConnectedApp { id: ConnectedAppId; name: string; audience: IntegrationAudience; description: string; url: string }

const platformIssuers: Readonly<Record<string, string>> = {
  'https://localhost:8264': 'https://localhost:8264',
  'https://localhost:8266': 'https://localhost:8264',
  'https://localhost:8340': 'https://localhost:8340',
  'https://localhost:8342': 'https://localhost:8340',
};

/** These built-in local apps are the only destinations allowed to receive a Platform ticket. */
export function connectedApps(deskOrigin: string): ConnectedApp[] {
  const issuer = platformIssuers[deskOrigin];
  const review = issuer === 'https://localhost:8340';
  if (!review && issuer !== 'https://localhost:8264') return [];
  return [
    { id: 'studio', name: 'Ocharo Studio', audience: 'ocharo-studio', description: 'Create garment looks and bring reviewed renders into your product library.', url: review ? 'https://localhost:8356/studio' : 'https://localhost:8262/studio' },
    { id: 'marketing', name: 'Ocharo Marketing', audience: 'ocharo-marketing', description: 'Prepare campaigns using your organization and approved brand releases.', url: review ? 'https://localhost:8344/' : 'https://localhost:8358/' },
    { id: 'editorial', name: 'Ocharo Editorial', audience: 'ocharo-marketing', description: 'Write and review stories in the editorial workspace.', url: review ? 'https://localhost:8352/studio/' : 'https://localhost:8284/studio/' },
  ];
}

/** Resolve by built-in ID again so a mutable UI object cannot introduce a token destination. */
export function connectionUrl(id: ConnectedAppId, deskOrigin: string, token: string): string {
  const issuer = platformIssuers[deskOrigin];
  const app = connectedApps(deskOrigin).find(candidate => candidate.id === id);
  if (!issuer || !app || !token || token.length > 8192) throw new Error('This connected app is unavailable.');
  const url = new URL(app.url);
  url.hash = app.id === 'studio'
    ? new URLSearchParams({ 'ocharo-ticket': token, audience: app.audience, issuer }).toString()
    : new URLSearchParams({ platformToken: token }).toString();
  return url.href;
}
