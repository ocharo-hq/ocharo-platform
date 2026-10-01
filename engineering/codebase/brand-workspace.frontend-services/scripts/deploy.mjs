import { cp, mkdir, rm } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
const source = fileURLToPath(new URL('../apps/web/dist/', import.meta.url));
const target = fileURLToPath(new URL('../../brand-workspace.backend-services/BrandWorkspace.Api/wwwroot/', import.meta.url));
await rm(target, { recursive: true, force: true });
await mkdir(target, { recursive: true });
await cp(source, target, { recursive: true });
