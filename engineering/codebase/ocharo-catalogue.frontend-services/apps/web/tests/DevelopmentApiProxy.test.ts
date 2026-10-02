// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { EventEmitter } from 'node:events';
import { createServer } from 'node:http';
import { PassThrough } from 'node:stream';
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { Agent } from 'node:https';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { developmentApiProxy, proxyProblem } from '../scripts/development-api';

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
    expect(proxy).toMatchObject({
      target: 'https://api.example.test',
      changeOrigin: false,
      secure: true,
      selfHandleResponse: true,
    });
    expect(proxy.configure).toBeTypeOf('function');
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

describe('Catalogue proxy error contract', () => {
  it.each(['', '<html>private backend diagnostic</html>', '{"detail":"SQL secret"}'])(
    'normalizes unsafe or empty 500 errors',
    (body) => {
      const problem = proxyProblem(500, body, '/api/catalogue/designs?secret=excluded');
      expect(problem).toMatchObject({
        type: 'about:blank',
        status: 500,
        title: 'Internal Server Error',
        instance: '/api/catalogue/designs',
      });
      expect(JSON.stringify(problem)).not.toMatch(/SQL|secret|private|html/);
    },
  );
  it('retains structured 400 field validation without losing the actual HTTP status', () => {
    const problem = proxyProblem(
      400,
      JSON.stringify({ type: 'about:blank', title: 'Validation failed', status: 400, errors: { title: ['Required'] } }),
    );
    expect(problem.status).toBe(400);
    expect(problem.errors).toEqual({ title: ['Required'] });
    expect(proxyProblem(404, '')).toMatchObject({ status: 404, title: 'Not Found' });
  });
});

describe('Catalogue proxy wire responses', () => {
  async function request(
    mode: 'connection' | 'empty' | 'html' | 'diagnostic' | 'success' | 'validation',
    method = 'GET',
  ) {
    const proxy = new EventEmitter();
    const options = developmentApiProxy({ target: 'https://api.example.test', development: false });
    options.configure!(proxy as Parameters<NonNullable<typeof options.configure>>[0], options);
    const server = createServer((incoming, response) => {
      if (mode === 'connection') {
        proxy.emit('error', new Error('TLS private certificate diagnostic'), incoming, response);
        return;
      }
      const upstream = Object.assign(new PassThrough(), {
        statusCode: mode === 'success' ? 200 : mode === 'validation' ? 400 : 500,
        headers: { 'content-type': 'application/json' },
      });
      proxy.emit('proxyRes', upstream, incoming, response);
      upstream.end(
        mode === 'diagnostic'
          ? JSON.stringify({
              type: 'about:blank',
              status: 500,
              title: 'SQL private diagnostic',
              detail: 'Private connection credentials',
              requestId: 'catalogue-request-123',
              traceId: '00-0123456789abcdef0123456789abcdef-0123456789abcdef-01',
              code: 'catalogue.persistence_failed',
              exception: 'SQL secret',
              context: { credentials: 'private' },
            })
          : mode === 'html'
            ? '<html>private diagnostic</html>'
            : mode === 'validation'
              ? JSON.stringify({
                  type: 'about:blank',
                  status: 400,
                  title: 'Validation failed',
                  errors: { title: ['Required'] },
                })
              : mode === 'success'
                ? '{"items":["saved"]}'
                : '',
      );
    });
    await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Fixture did not bind a port');
      const response = await fetch(`http://127.0.0.1:${address.port}/api/catalogue/designs`, { method });
      return {
        status: response.status,
        type: response.headers.get('content-type'),
        requestId: response.headers.get('x-request-id'),
        body: await response.text(),
      };
    } finally {
      server.closeAllConnections();
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  }
  it.each(['connection', 'empty', 'html'] as const)('returns safe ProblemDetails for %s failures', async (mode) => {
    const result = await request(mode);
    expect(result.type).toContain('application/problem+json');
    expect(JSON.parse(result.body)).toMatchObject({
      type: 'about:blank',
      status: mode === 'connection' ? 502 : 500,
      instance: '/api/catalogue/designs',
    });
    const problem = JSON.parse(result.body);
    expect(problem.requestId).toMatch(/^[A-Za-z0-9-]{36}$/);
    expect(problem.traceId).toMatch(/^[a-f0-9]{32}$/);
    expect(result.requestId).toBe(problem.requestId);
    expect(result.body).not.toMatch(/TLS|private|html/);
  });
  it('retains only safe upstream correlation fields on server failures', async () => {
    const result = await request('diagnostic');
    const problem = JSON.parse(result.body);
    expect(problem).toEqual({
      type: 'about:blank',
      status: 500,
      title: 'Internal Server Error',
      detail: 'The catalogue service is unavailable. Please try again.',
      instance: '/api/catalogue/designs',
      requestId: 'catalogue-request-123',
      traceId: '00-0123456789abcdef0123456789abcdef-0123456789abcdef-01',
      code: 'catalogue.persistence_failed',
    });
    expect(result.requestId).toBe(problem.requestId);
    expect(result.body).not.toMatch(/SQL|private|credentials|exception|context/i);
  });
  it('rejects diagnostic text masquerading as a correlation identifier', () => {
    const problem = proxyProblem(
      500,
      JSON.stringify({
        type: 'about:blank',
        title: 'Failure',
        status: 500,
        traceId: 'SQL private detail',
        requestId: {},
        code: 'x'.repeat(129),
      }),
    );
    expect(problem.requestId).toMatch(/^[A-Za-z0-9-]{36}$/);
    expect(problem.traceId).toMatch(/^[a-f0-9]{32}$/);
    expect(problem.code).toBeUndefined();
  });
  it('streams the success projection unchanged', async () => {
    expect(await request('success')).toMatchObject({
      status: 200,
      type: 'application/json',
      body: '{"items":["saved"]}',
    });
  });
  it('preserves field validation and the HEAD protocol exception', async () => {
    expect(JSON.parse((await request('validation')).body).errors).toEqual({ title: ['Required'] });
    expect(await request('connection', 'HEAD')).toMatchObject({
      status: 502,
      type: expect.stringContaining('application/problem+json'),
      body: '',
    });
  });
});
