// Copies apps/web/dist into the .NET host for a single deployment.
import { rm, mkdir, cp, access } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const distDir = fileURLToPath(new URL('../apps/web/dist/', import.meta.url));
const wwwroot = fileURLToPath(new URL('../../ocharo-catalogue.backend-services/OcharoCatalogue.Api/wwwroot/', import.meta.url));

try {
  await access(new URL('../apps/web/dist/index.html', import.meta.url));
} catch {
  console.error(`[deploy] Built app not found at ${distDir}; run "pnpm build" first.`);
  process.exit(1);
}

await rm(wwwroot, { recursive: true, force: true });
await mkdir(wwwroot, { recursive: true });
await cp(distDir, wwwroot, { recursive: true });
console.log(`[deploy] Copied ${distDir} to ${wwwroot}`);
