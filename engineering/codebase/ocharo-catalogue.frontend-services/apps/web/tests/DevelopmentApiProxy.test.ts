// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { Agent } from 'node:https';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { developmentApiProxy } from '../scripts/development-api';

vi.mock('node:child_process', () => ({ execFileSync: vi.fn() }));

const pem = 'public certificate fixture';
const run = vi.mocked(execFileSync);
const agents: Agent[] = [];

beforeEach(() => {
  run.mockReset();
  run.mockImplementation((_file, args) => {
    const values = args as string[];
    const exportIndex = values.indexOf('--export-path');
    if (exportIndex >= 0) writeFileSync(values[exportIndex + 1]!, pem);
    return Buffer.from('');
  });
});

afterEach(() => {
  agents.splice(0).forEach((agent) => agent.destroy());
});

describe('Catalogue development HTTPS proxy', () => {
  it.each(['localhost', '127.0.0.1', '[::1]'])('validates %s with the existing public .NET certificate', (host) => {
    const proxy = developmentApiProxy({ target: `https://${host}:8278`, development: true });
    const agent = proxy.agent as Agent;
    agents.push(agent);
    expect(proxy.secure).toBe(true);
    expect(agent.options.ca).toEqual(Buffer.from(pem));
    expect(run.mock.calls[0]?.[1]).toEqual(['dev-certs', 'https', '--check']);
    const exportArguments = run.mock.calls[1]?.[1] as string[];
    expect(exportArguments).toEqual(['dev-certs', 'https', '--export-path', expect.any(String), '--format', 'Pem']);
    expect(existsSync(dirname(exportArguments[3]!))).toBe(false);
  });

  it('honors an explicit certificate for a custom backend without accessing the certificate store', () => {
    const directory = mkdtempSync(join(tmpdir(), 'catalogue-proxy-test-'));
    try {
      const certificatePath = join(directory, 'custom.pem');
      writeFileSync(certificatePath, pem);
      const proxy = developmentApiProxy({ target: 'https://api.example.test', development: true, certificatePath });
      const agent = proxy.agent as Agent;
      agents.push(agent);
      expect(agent.options.ca).toEqual(Buffer.from(pem));
      expect(proxy.secure).toBe(true);
      expect(run).not.toHaveBeenCalled();
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('leaves remote endpoints on normal Node certificate verification', () => {
    const proxy = developmentApiProxy({ target: 'https://api.example.test', development: true });
    expect(proxy).toEqual({ target: 'https://api.example.test', changeOrigin: false, secure: true });
    expect(run).not.toHaveBeenCalled();
  });

  it('does not require local certificates for frontend builds', () => {
    const proxy = developmentApiProxy({
      target: 'https://localhost:8278',
      development: false,
      certificatePath: '/missing.pem',
    });
    expect(proxy.secure).toBe(true);
    expect(proxy.agent).toBeUndefined();
    expect(run).not.toHaveBeenCalled();
  });

  it('fails without creating or exporting a certificate when the existing certificate is missing', () => {
    run.mockImplementationOnce(() => {
      throw new Error('No existing certificate');
    });
    expect(() => developmentApiProxy({ target: 'https://localhost:8278', development: true })).toThrow(
      'Set VITE_API_CA_PATH',
    );
    expect(run).toHaveBeenCalledTimes(1);
    expect(run.mock.calls[0]?.[1]).toEqual(['dev-certs', 'https', '--check']);
  });

  it('rejects plaintext API targets before accessing any certificate', () => {
    expect(() => developmentApiProxy({ target: 'http://localhost:8278', development: true })).toThrow('must use HTTPS');
    expect(run).not.toHaveBeenCalled();
  });
});
