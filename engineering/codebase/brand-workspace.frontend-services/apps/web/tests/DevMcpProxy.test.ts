// @vitest-environment node
import { describe, expect, it } from 'vitest';
import type { ProxyOptions, UserConfigFnObject } from 'vite';
import config from '../vite.config';

describe('Desk development MCP transport', () => {
  it('routes API and MCP to the same certificate-verified backend', () => {
    const resolved = (config as UserConfigFnObject)({ command: 'build', mode: 'test' });
    const proxy = resolved.server!.proxy!;
    const api = proxy['/api'] as ProxyOptions;
    const mcp = proxy['/mcp'] as ProxyOptions;
    expect(mcp.target).toBe(api.target);
    expect(new URL(String(mcp.target)).protocol).toBe('https:');
    expect(mcp.secure).toBe(true);
    expect(mcp.changeOrigin).not.toBe(true);
    expect(mcp.configure).toBeTypeOf('function');
  });
});
