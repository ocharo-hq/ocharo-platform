/** The central server validates the registered client; only its exact local endpoint is accepted. */
export function signInDestination(search: string, fallback: string): string {
  const value = new URLSearchParams(search).get('returnUrl');
  if (!value || !value.startsWith('/api/sso/authorize?') || /[\\\r\n]/.test(value)) return fallback;
  const parsed = new URL(value, 'https://local.invalid');
  return parsed.origin === 'https://local.invalid' && parsed.pathname === '/api/sso/authorize'
    ? parsed.pathname + parsed.search : fallback;
}
