import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Starts OcharoCatalogue.Api for the browser suite on its own port with a fresh temporary SQLite store and media root,
 * so seeded products, catalogues and releases never reach a developer's App_Data or the machine's local settings.
 */
const port = process.env.CATALOGUE_E2E_API_PORT ?? '18278';
const root = mkdtempSync(join(tmpdir(), 'ocharo-catalogue-e2e-'));
const project = fileURLToPath(
  new URL('../../../../../ocharo-catalogue.backend-services/OcharoCatalogue.Api/', import.meta.url),
);
const api = spawn('dotnet', ['run', '--project', project, '--no-launch-profile'], {
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: `https://localhost:${port}`,
    ConnectionStrings__OcharoCatalogue: `Data Source=${join(root, 'catalogue.db')}`,
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
  rmSync(root, { recursive: true, force: true });
  process.exit(code ?? 0);
});
