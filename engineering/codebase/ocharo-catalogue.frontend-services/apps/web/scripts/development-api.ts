import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { Agent } from 'node:https';
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

/** Keeps API proxy verification enabled, including the local .NET development certificate. */
export function developmentApiProxy(options: DevelopmentApiOptions): ProxyOptions {
  const target = new URL(options.target);
  if (target.protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  const proxy: ProxyOptions = { target: options.target, changeOrigin: false, secure: true };
  if (!options.development) return proxy;

  if (options.certificatePath) {
    proxy.agent = new Agent({ ca: readFileSync(options.certificatePath) });
  } else if (['localhost', '127.0.0.1', '[::1]'].includes(target.hostname)) {
    proxy.agent = new Agent({ ca: dotNetDevelopmentCertificate() });
  }
  return proxy;
}
