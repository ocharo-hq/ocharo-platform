import { execFileSync } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { Agent } from 'node:https';
import { STATUS_CODES, ServerResponse, type IncomingMessage, type IncomingHttpHeaders } from 'node:http';
import { pipeline } from 'node:stream';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import type { ProxyOptions } from 'vite';

interface DevelopmentApiOptions {
  target: string;
  development: boolean;
  certificatePath?: string;
}

/** Reads only the public certificate; never creates, trusts, or exports a private key. */
function dotNetDevelopmentCertificate(): Buffer {
  const directory = mkdtempSync(join(tmpdir(), 'ocharo-catalogue-ca-'));
  const certificate = join(directory, 'api.pem');
  try {
    const options = { timeout: 15_000, stdio: 'pipe' as const };
    execFileSync('dotnet', ['dev-certs', 'https', '--check'], options);
    execFileSync('dotnet', ['dev-certs', 'https', '--export-path', certificate, '--format', 'Pem'], options);
    return readFileSync(certificate);
  } catch (cause) {
    throw new Error(
      'Cannot load the existing .NET HTTPS development certificate. Set VITE_API_CA_PATH to the backend public PEM certificate, or install an existing .NET development certificate before starting Catalogue.',
      { cause },
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

interface ProxyProblem {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance?: string;
  [key: string]: unknown;
}

function fallbackProblem(status: number, instance?: string): ProxyProblem {
  return {
    type: 'about:blank',
    title: STATUS_CODES[status] ?? 'Request failed',
    status,
    requestId: randomUUID(),
    traceId: randomBytes(16).toString('hex'),
    detail:
      status >= 500 ? 'The catalogue service is unavailable. Please try again.' : 'The request could not be completed.',
    ...(instance ? { instance: instance.split('?')[0] } : {}),
  };
}

/** Never forwards empty errors, HTML, stack traces, or certificate diagnostics to an API consumer. */
export function proxyProblem(status: number, body: string, instance?: string): ProxyProblem {
  const fallback = fallbackProblem(status, instance);
  try {
    const value: unknown = JSON.parse(body);
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      const problem = value as Record<string, unknown>;
      if (problem.status === status && typeof problem.title === 'string' && typeof problem.type === 'string') {
        const identifiers = Object.fromEntries(
          ['traceId', 'requestId', 'code']
            .filter(
              (key) => typeof problem[key] === 'string' && /^[A-Za-z0-9_.:-]{1,128}$/.test(problem[key] as string),
            )
            .map((key) => [key, problem[key]]),
        );
        if (status >= 500) return { ...fallback, ...identifiers };
        return {
          ...problem,
          ...fallback,
          ...identifiers,
          detail: typeof problem.detail === 'string' ? problem.detail : fallback.detail,
        };
      }
    }
  } catch {
    /* An upstream error may have no JSON body. */
  }
  return fallback;
}

/** HEAD cannot contain a response body; all other completed API failures use ProblemDetails. */
function sendProblem(
  response: ServerResponse,
  request: IncomingMessage,
  problem: ProxyProblem,
  upstreamHeaders: IncomingHttpHeaders = {},
): void {
  if (response.headersSent || response.writableEnded || response.destroyed) return;
  const body = JSON.stringify(problem);
  for (const name of [
    'www-authenticate',
    'allow',
    'retry-after',
    'set-cookie',
    'x-request-id',
    'access-control-allow-origin',
    'access-control-allow-credentials',
    'vary',
  ]) {
    const value = upstreamHeaders[name];
    if (value !== undefined) response.setHeader(name, value);
  }
  response.setHeader('x-request-id', String(problem.requestId));
  response.writeHead(problem.status, {
    'Content-Type': 'application/problem+json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
    'Cache-Control': 'no-store',
  });
  response.end(request.method === 'HEAD' ? undefined : body);
}

/** Keeps API proxy verification enabled, including the local .NET development certificate. */
export function developmentApiProxy(options: DevelopmentApiOptions): ProxyOptions {
  const target = new URL(options.target);
  if (target.protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  const proxy: ProxyOptions = {
    target: options.target,
    changeOrigin: false,
    secure: true,
    selfHandleResponse: true,
    configure(server) {
      server.on('error', (_cause, request, response) => {
        if (response instanceof ServerResponse) sendProblem(response, request, fallbackProblem(502, request.url));
      });
      server.on('proxyRes', (upstream, request, response) => {
        if (response.writableEnded || response.destroyed) {
          upstream.resume();
          return;
        }
        const status = upstream.statusCode ?? 502;
        if (status < 400) {
          response.writeHead(status, upstream.headers);
          pipeline(upstream, response, () => {
            /* Streaming cancellation is owned by the transport. */
          });
          return;
        }
        const chunks: Buffer[] = [];
        let size = 0;
        upstream.on('data', (chunk: Buffer) => {
          size += chunk.length;
          if (size <= 65_536) chunks.push(chunk);
        });
        upstream.on('end', () =>
          sendProblem(
            response,
            request,
            proxyProblem(status, size <= 65_536 ? Buffer.concat(chunks).toString('utf8') : '', request.url),
            upstream.headers,
          ),
        );
        upstream.on('error', () => sendProblem(response, request, fallbackProblem(502, request.url)));
        upstream.on('aborted', () => sendProblem(response, request, fallbackProblem(502, request.url)));
      });
    },
  };
  if (!options.development) return proxy;

  if (options.certificatePath) {
    proxy.agent = new Agent({ ca: readFileSync(options.certificatePath) });
  } else if (['localhost', '127.0.0.1', '[::1]'].includes(target.hostname)) {
    proxy.agent = new Agent({ ca: dotNetDevelopmentCertificate() });
  }
  return proxy;
}
