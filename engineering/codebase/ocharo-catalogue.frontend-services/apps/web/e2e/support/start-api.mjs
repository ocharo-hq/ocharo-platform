import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Starts OcharoCatalogue.Api for the browser suite on its own port with a fresh PostgreSQL database and media root, so
 * seeded products, catalogues and releases never reach a developer's database, App_Data or the machine's local settings.
 *
 * The API runs as a local run (Development), which creates its database and applies the `catalogue` set at boot. The
 * server is `CATALOGUE_E2E_DATABASE` (a connection string to a disposable database) or, by default, a throwaway
 * `postgres:18-alpine` container started here and removed when the run ends; Docker is then required.
 */
const port = process.env.CATALOGUE_E2E_API_PORT ?? '18278';
const root = mkdtempSync(join(tmpdir(), 'ocharo-catalogue-e2e-'));
const project = fileURLToPath(
  new URL('../../../../../ocharo-catalogue.backend-services/OcharoCatalogue.Api/', import.meta.url),
);

/** Runs a Docker command and returns its trimmed stdout; a failure ends the run with Docker's own message. */
const docker = (...args) => {
  const result = spawnSync('docker', args, { encoding: 'utf8' });
  if (result.status !== 0) {
    throw new Error(`docker ${args[0]} failed: ${(result.stderr || result.stdout || '').trim()}`);
  }
  return result.stdout.trim();
};

let container;
const cleanup = () => {
  if (container) spawnSync('docker', ['rm', '--force', container], { stdio: 'ignore' });
  container = undefined;
  rmSync(root, { recursive: true, force: true });
};

/** Starts the throwaway server and waits until it accepts TCP connections (not only the init phase's socket). */
const startPostgres = () => {
  // An inert fixture password for a container that lives only as long as this run.
  const password = 'catalogue-browser-tests';
  container = docker(
    'run', '--detach', '--rm', '--env', `POSTGRES_PASSWORD=${password}`, '--publish', '127.0.0.1::5432', 'postgres:18-alpine',
  );
  const hostPort = docker('port', container, '5432/tcp').split('\n')[0].split(':').pop();
  const deadline = Date.now() + 60_000;
  while (spawnSync('docker', ['exec', container, 'pg_isready', '-h', '127.0.0.1', '-U', 'postgres', '-q']).status !== 0) {
    if (Date.now() > deadline) throw new Error('PostgreSQL did not become ready within 60 seconds.');
    spawnSync('sleep', ['1']);
  }
  return `Host=127.0.0.1;Port=${hostPort};Username=postgres;Password=${password};Database=ocharo_catalogue_e2e`;
};

let database;
try {
  database = process.env.CATALOGUE_E2E_DATABASE ?? startPostgres();
} catch (error) {
  cleanup();
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
}

const api = spawn('dotnet', ['run', '--project', project, '--no-launch-profile', '--property:BuildSpa=false'], {
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: `https://localhost:${port}`,
    ConnectionStrings__Database: database,
    Catalogue__DataRoot: join(root, 'data'),
    Catalogue__SeedOnEmpty: 'false',
    DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DOTNET_NOLOGO: '1',
  },
});
const stop = (signal) => api.kill(signal);
process.on('SIGINT', () => stop('SIGINT'));
process.on('SIGTERM', () => stop('SIGTERM'));
api.on('exit', (code) => {
  cleanup();
  process.exit(code ?? 0);
});
