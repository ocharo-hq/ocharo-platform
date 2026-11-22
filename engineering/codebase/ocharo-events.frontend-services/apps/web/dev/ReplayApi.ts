import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import type { IncomingMessage, ServerResponse } from 'node:http';
import { join } from 'node:path';
import type { Plugin } from 'vite';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';
import {
  buildFacets,
  buildSummary,
  filterTracking,
  pageTracking,
  parseSummaryQuery,
  parseTrackingQuery,
  sortTracking,
  type ReplayCatalog,
} from './ReplayQuery';

/**
 * Replay mode (`pnpm dev:replay`): the dev server answers the whole `/api` surface the app calls from a snapshot of the
 * dev API's real responses, so the layout can be worked on with real events, no backend and no sign-in. Reads only: the
 * list, facets and summary are computed over the snapshot (`ReplayQuery.ts`), the session is synthetic, every write
 * answers 403.
 */

/** What the snapshot folder holds, read once when the server starts. */
interface ReplaySnapshot {
  readonly organizationId: string;
  readonly items: readonly TrackedEventModel[];
  readonly catalogSlug: string;
  readonly catalog: ReplayCatalog;
  /** The workflow route's recorded answer: a problem body and its status. */
  readonly workflow: { readonly status: number; readonly body: unknown };
  readonly runtimeConfig: unknown;
  readonly systemStatus: unknown;
}

/** Ocharo's two lost stages (`statusClasses` in `src/domain/events/Events.ts`): the summary's deadlines skip them. */
const lostStages: readonly string[] = ['skipped', 'rejected'];

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const folderHint = 'See apps/web/dev/dev.md.';

function readJson(dir: string, name: string): unknown {
  const file = join(dir, name);
  if (!existsSync(file)) throw new Error(`Replay mode needs ${name} in ${dir}. ${folderHint}`);
  return JSON.parse(readFileSync(file, 'utf8')) as unknown;
}

/** The `value` of each entry of a facet list, in the order the service listed them. */
function facetValues(facets: Record<string, unknown>, key: string): string[] {
  const list = facets[key];
  if (!Array.isArray(list)) throw new Error(`Replay mode: facets-0.json lists no "${key}". ${folderHint}`);
  return list.flatMap((entry: unknown) => (isRecord(entry) && typeof entry.value === 'string' ? [entry.value] : []));
}

function loadSnapshot(dir: string): ReplaySnapshot {
  const replay = readJson(dir, 'replay.json');
  const organizationId = isRecord(replay) ? replay.organizationId : undefined;
  if (typeof organizationId !== 'string' || !identifier.test(organizationId))
    throw new Error(
      `Replay mode needs replay.json in ${dir} with {"organizationId": "<the organization's id>"}. ${folderHint}`,
    );

  const tracking = readJson(dir, 'tracking-all.json');
  const page = isRecord(tracking) && isRecord(tracking.data) ? tracking.data : undefined;
  const listed: unknown = page?.items;
  const items: readonly unknown[] = Array.isArray(listed) ? listed : [];
  if (items.length === 0 || !items.every((item) => isRecord(item) && isRecord(item.event)))
    throw new Error(`Replay mode: tracking-all.json holds no events. ${folderHint}`);
  const events = items as readonly TrackedEventModel[];

  const facets = readJson(dir, 'facets-0.json');
  const counted = isRecord(facets) && isRecord(facets.data) ? facets.data : undefined;
  if (!counted) throw new Error(`Replay mode: facets-0.json is not a facets answer. ${folderHint}`);
  const stages = facetValues(counted, 'stages');
  const defaultStage = stages[0];
  if (defaultStage === undefined) throw new Error(`Replay mode: facets-0.json lists no stages. ${folderHint}`);

  const workflow = readJson(dir, 'workflow.json');
  return {
    organizationId,
    items: events,
    catalogSlug: events[0]!.event.catalog,
    catalog: { kinds: facetValues(counted, 'kinds'), stages, defaultStage, lostStages },
    workflow: {
      status: isRecord(workflow) && typeof workflow.status === 'number' ? workflow.status : 404,
      body: workflow,
    },
    runtimeConfig: readJson(dir, 'runtime-config.json'),
    systemStatus: readJson(dir, 'system-status.json'),
  };
}

/** The signed-in session the replay serves: a synthetic reviewer of the snapshot's organization who reads and tracks. */
function replaySession(organizationId: string): Record<string, unknown> {
  return {
    isAuthenticated: true,
    person: { sub: 'replay-reviewer', name: 'Replay Reviewer', picture: null, email: null, phoneNumber: null },
    organization: { id: organizationId, name: 'Replay Organization', role: 'editor', slug: 'replay-organization' },
    permissions: ['events:read', 'events:track'],
    csrfToken: 'replay-csrf-token',
    expiresAt: '2099-01-01T00:00:00Z',
  };
}

function send(response: ServerResponse, status: number, body: unknown, contentType = 'application/json'): void {
  response.writeHead(status, { 'Content-Type': `${contentType}; charset=utf-8`, 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(body));
}

/** Answers a structured API failure, shaped as the service's problem bodies. */
function sendProblem(response: ServerResponse, instance: string, status: number, title: string, detail?: string): void {
  const requestId = randomUUID();
  send(
    response,
    status,
    {
      type: 'about:blank',
      title,
      status,
      ...(detail ? { detail } : {}),
      code: title.toLowerCase().replace(/[^a-z0-9]+/g, '_'),
      instance,
      requestId,
      traceId: requestId,
    },
    'application/problem+json',
  );
}

function redirect(response: ServerResponse, location: string): void {
  response.writeHead(302, { Location: location, 'Cache-Control': 'no-store', 'Referrer-Policy': 'no-referrer' });
  response.end();
}

/** Whether `target` is a same-origin path, as the SDK's sign-in accepts a return address. */
const isLocalPath = (target: string): boolean =>
  target.startsWith('/') &&
  !target.startsWith('//') &&
  ![...target].some((character) => character === '\\' || character.charCodeAt(0) < 32);

/** An image's own address when it is an absolute https one; the stored-image route sends the browser there. */
function httpsAddress(value: string): string | null {
  try {
    const address = new URL(value);
    return address.protocol === 'https:' ? address.href : null;
  } catch {
    return null;
  }
}

function decodedSegments(pathname: string): string[] | null {
  try {
    return pathname.split('/').filter(Boolean).map(decodeURIComponent);
  } catch {
    return null;
  }
}

/** Serves the mirrored Events routes (`…/events-api/api/events/v1/…`); `parts` follow `events-api`. */
function handleEvents(
  snapshot: ReplaySnapshot,
  parts: readonly string[],
  url: URL,
  today: string,
  response: ServerResponse,
): void {
  const missing = () => sendProblem(response, url.pathname, 404, 'Not Found');
  if (parts[0] !== 'api' || parts[1] !== 'events' || parts[2] !== 'v1') return missing();
  const [area, ...rest] = parts.slice(3);
  if (area === 'workflow' && rest.length === 0)
    return send(response, snapshot.workflow.status, snapshot.workflow.body, 'application/problem+json');
  const [slug, resource, ...tail] = rest;
  if (area !== 'catalogs' || slug !== snapshot.catalogSlug) return missing();
  const find = (id: string | undefined) =>
    snapshot.items.find((item) => item.event.id.toLowerCase() === id?.toLowerCase());

  if (resource === 'tracking') {
    const [next, ...after] = tail;
    if (next === undefined) {
      const query = parseTrackingQuery(url.searchParams);
      if (!query.ok) return sendProblem(response, url.pathname, 400, 'Bad Request', query.detail);
      const sort = query.value.sort ?? 'starts';
      const page = pageTracking(
        sortTracking(filterTracking(snapshot.items, query.value, snapshot.catalog, today), sort, today),
        sort,
        query.value.cursor,
        query.value.limit,
      );
      if (!page.ok) return sendProblem(response, url.pathname, 400, 'Bad Request', page.detail);
      return send(response, 200, {
        data: { items: page.value.items, ...(page.value.nextCursor ? { nextCursor: page.value.nextCursor } : {}) },
      });
    }
    if (next === 'facets' && after.length === 0) {
      const query = parseTrackingQuery(url.searchParams);
      if (!query.ok) return sendProblem(response, url.pathname, 400, 'Bad Request', query.detail);
      return send(response, 200, { data: buildFacets(snapshot.items, query.value, snapshot.catalog, today) });
    }
    if (next === 'summary' && after.length === 0) {
      const query = parseSummaryQuery(url.searchParams, today);
      if (!query.ok) return sendProblem(response, url.pathname, 400, 'Bad Request', query.detail);
      return send(response, 200, {
        data: buildSummary(snapshot.items, snapshot.catalog, query.value.today, query.value.deadlineKinds),
      });
    }
    const item = find(next);
    if (!item) return missing();
    if (after.length === 0) return send(response, 200, { data: item });
    if (after.length === 1 && after[0] === 'history') return send(response, 200, { data: [] });
    return missing();
  }

  if (resource === 'events') {
    const [id, images, index, ...extra] = tail;
    const item = find(id);
    if (!item) return missing();
    if (images === undefined) return send(response, 200, { data: item.event });
    if (images === 'images' && index !== undefined && /^\d+$/.test(index) && extra.length === 0) {
      const target = httpsAddress(item.event.facts.images?.[Number(index)]?.url ?? '');
      return target ? redirect(response, target) : missing();
    }
  }
  return missing();
}

/** Answers one `/api` request: reads from the snapshot, every other method refused. */
function handle(snapshot: ReplaySnapshot, request: IncomingMessage, response: ServerResponse, url: URL): void {
  if (request.method !== 'GET') return sendProblem(response, url.pathname, 403, 'Replay mode is read-only');
  const parts = decodedSegments(url.pathname);
  if (parts === null) return sendProblem(response, url.pathname, 400, 'Bad Request', 'The address is not valid');
  const today = new Date().toISOString().slice(0, 10);
  const route = parts.slice(1).join('/');

  if (route === 'runtime-config') return send(response, 200, snapshot.runtimeConfig);
  if (route === 'system/status') return send(response, 200, snapshot.systemStatus);
  if (route === 'identity/session') return send(response, 200, { data: replaySession(snapshot.organizationId) });
  if (route === 'identity/sign-in') {
    // Always signed in: sign-in only returns to where the person came from.
    const returnUrl = url.searchParams.get('returnUrl') ?? '/';
    return redirect(response, isLocalPath(returnUrl) ? returnUrl : '/');
  }
  if (parts[1] === 'organizations' && parts[3] === 'events-api') {
    if (parts[2]?.toLowerCase() !== snapshot.organizationId.toLowerCase())
      return sendProblem(response, url.pathname, 404, 'Not Found');
    return handleEvents(snapshot, parts.slice(4), url, today, response);
  }
  return sendProblem(response, url.pathname, 404, 'Not Found');
}

/** The replay's Vite plugin: a dev-server middleware on `/api` over the snapshot in `dir`, never part of a build. */
export function replayApi(options: { readonly dir: string }): Plugin {
  return {
    name: 'ocharo-events-replay-api',
    apply: 'serve',
    configureServer(server) {
      const snapshot = loadSnapshot(options.dir);
      server.config.logger.info(
        `  Replay mode: ${snapshot.items.length} events from the snapshot; /api is answered here, read only.`,
      );
      server.middlewares.use((request, response, next) => {
        const url = new URL(request.url ?? '/', 'https://localhost');
        if (url.pathname !== '/api' && !url.pathname.startsWith('/api/')) return next();
        try {
          handle(snapshot, request, response, url);
        } catch (error) {
          server.config.logger.error(`Replay mode: ${error instanceof Error ? error.message : String(error)}`);
          if (!response.headersSent) sendProblem(response, url.pathname, 500, 'Internal Server Error');
        }
      });
    },
  };
}
