import { inject, type InjectionKey, type Plugin } from 'vue';
import {
  ConfigError,
  defineConfig,
  importMetaEnvSource,
  staticSource,
  url,
  type ConfigSchema,
  type ConfigSource,
  type InferConfig,
} from '@wow-two-beta/fe/foundation/config';

import {
  ocharoApps,
  resolveOcharoAppUrl,
  type OcharoApp,
  type OcharoAppAddresses,
  type OcharoAppKey,
} from '../../domain/shared/OcharoApps';
import { runtimeConfigApi, type RuntimeConfigValues } from '../../integration/common/RuntimeConfigApi';
import { notifyError } from '../common/UseFeedback';
import { useUiLocale } from './UseUiLocale';

/** The build reads every key under this prefix: `OCHARO_DESK_URL` is `VITE_OCHARO_DESK_URL` there. */
export const BuildTimePrefix = 'VITE_';

/**
 * Every address Catalogue reads, keyed as `/api/runtime-config` serves it; each default is the local-run address.
 * The app keys match Desk's. `OCHARO_BRAND_API_URL` is the Brand API the print sheet reads the published kit from —
 * a different service from the Brand app the menu links to with `OCHARO_BRAND_URL`.
 */
export const OcharoAppsConfigSchema = {
  OCHARO_DESK_URL: url({ default: 'https://localhost:8264/' }),
  OCHARO_CATALOGUE_URL: url({ default: 'https://localhost:8282/catalogues' }),
  OCHARO_STUDIO_URL: url({ default: 'https://localhost:8262/studio' }),
  OCHARO_MARKETING_URL: url({ default: 'https://localhost:8358/' }),
  OCHARO_EDITORIAL_URL: url({ default: 'https://localhost:8284/studio/' }),
  OCHARO_BRAND_URL: url({ default: 'https://localhost:8288/' }),
  OCHARO_ATLAS_URL: url({ default: 'https://localhost:8284/atlas/' }),
  OCHARO_WEAR_URL: url({ default: 'https://localhost:8284/wear/' }),
  OCHARO_BLOG_URL: url({ default: 'https://localhost:8284/' }),
  OCHARO_BRAND_API_URL: url({ default: 'https://localhost:8286/' }),
} satisfies ConfigSchema;

/** The resolved addresses, one per schema key. */
export type OcharoAppsConfig = InferConfig<typeof OcharoAppsConfigSchema>;

/** @internal The menu destination each app key addresses. */
const AppKeys: Readonly<Partial<Record<keyof OcharoAppsConfig, OcharoAppKey>>> = {
  OCHARO_DESK_URL: 'desk',
  OCHARO_CATALOGUE_URL: 'catalogue',
  OCHARO_STUDIO_URL: 'studio',
  OCHARO_MARKETING_URL: 'marketing',
  OCHARO_EDITORIAL_URL: 'editorial',
  OCHARO_BRAND_URL: 'brand',
  OCHARO_ATLAS_URL: 'atlas',
  OCHARO_WEAR_URL: 'wear',
  OCHARO_BLOG_URL: 'blog',
};

/** What the operator is told when startup could not use every source or value. */
export const OcharoAppsProblem = {
  Unavailable: 'Ocharo app links could not be loaded, so the app menu uses the built-in addresses.',
  Rejected: 'Some Ocharo app links are misconfigured, so the app menu leaves them out.',
} as const;

export type OcharoAppsProblem = (typeof OcharoAppsProblem)[keyof typeof OcharoAppsProblem];

/** The addresses Catalogue links to, resolved once at startup. */
export interface OcharoLinks {
  /** The app menu's destinations. */
  readonly addresses: OcharoAppAddresses;
  /** The Brand API root for the published brand kit; `null` when it is not a plain HTTPS link. */
  readonly brandApiUrl: string | null;
}

/** What startup resolved, and each problem owed to the operator. */
export interface OcharoAppsStartup extends OcharoLinks {
  /** The operator message and the technical detail behind it; empty when every source and value was usable. */
  readonly problems: readonly { readonly message: OcharoAppsProblem; readonly detail: string }[];
}

/** The runtime source: the served settings under the build prefix, so one lookup key reads every source. */
export function runtimeConfigSource(values: RuntimeConfigValues): ConfigSource {
  return staticSource(
    Object.fromEntries(Object.entries(values).map(([key, value]) => [`${BuildTimePrefix}${key}`, value])),
  );
}

/** The build-time source: the `VITE_` values Vite inlines into Catalogue's own modules. */
export function buildTimeConfigSource(): ConfigSource {
  return importMetaEnvSource(import.meta.env);
}

/** Resolves every address, earlier sources first; throws one `ConfigError` naming each malformed value. */
export function resolveOcharoAppsConfig(sources: readonly ConfigSource[]): OcharoAppsConfig {
  return defineConfig(OcharoAppsConfigSchema, { sources, prefix: BuildTimePrefix });
}

/** @internal Maps resolved keys onto menu destinations and the Brand API root. */
function toLinks(config: Readonly<Record<string, unknown>>): OcharoLinks {
  const addresses: Partial<Record<OcharoAppKey, string>> = {};
  for (const [key, app] of Object.entries(AppKeys)) {
    const value = config[key];
    if (typeof value === 'string' && app) addresses[app] = value;
  }
  const brandApi = config.OCHARO_BRAND_API_URL;
  return { addresses, brandApiUrl: typeof brandApi === 'string' ? resolveOcharoAppUrl(brandApi) : null };
}

/** @internal Resolves the links; a malformed value leaves its key out instead of yielding to a lower source. */
function resolveLinks(sources: readonly ConfigSource[]): { links: OcharoLinks; error: ConfigError | null } {
  try {
    return { links: toLinks(resolveOcharoAppsConfig(sources)), error: null };
  } catch (error) {
    if (!(error instanceof ConfigError)) throw error;
    const rejected = new Set(error.issues.map((issue) => issue.key));
    const usable = Object.fromEntries(Object.entries(OcharoAppsConfigSchema).filter(([key]) => !rejected.has(key)));
    return { links: toLinks(defineConfig(usable, { sources, prefix: BuildTimePrefix })), error };
  }
}

/** Reads the served settings once, then resolves every address over the build-time values and local defaults. */
export async function loadOcharoApps(signal: AbortSignal = new AbortController().signal): Promise<OcharoAppsStartup> {
  const runtime = await runtimeConfigApi.get(signal);
  const sources = runtime.ok
    ? [runtimeConfigSource(runtime.value), buildTimeConfigSource()]
    : [buildTimeConfigSource()];
  const { links, error } = resolveLinks(sources);
  return {
    ...links,
    problems: [
      ...(runtime.ok
        ? []
        : [
            {
              message: OcharoAppsProblem.Unavailable,
              detail: `GET /api/runtime-config failed: ${runtime.failure.code}.`,
            },
          ]),
      ...(error ? [{ message: OcharoAppsProblem.Rejected, detail: error.message }] : []),
    ],
  };
}

/** Reports each startup problem once through the app feedback; Catalogue keeps working with the addresses it has. */
export function reportOcharoAppsStartup(startup: OcharoAppsStartup): void {
  const { t } = useUiLocale();
  for (const problem of startup.problems) {
    console.warn(`Ocharo apps: ${problem.detail}`);
    notifyError(t(problem.message), `ocharo-apps:${problem.message}`);
  }
}

/** @internal Carries the startup resolution to every component. */
const OcharoLinksKey: InjectionKey<OcharoLinks> = Symbol('OcharoLinks');

/** Provides the links resolved at startup to the whole app. */
export function ocharoAppsPlugin(links: OcharoLinks): Plugin {
  return {
    install(app) {
      app.provide(OcharoLinksKey, { addresses: links.addresses, brandApiUrl: links.brandApiUrl });
    },
  };
}

/** @internal The startup resolution, or the build-time values and defaults where none is provided. */
function useOcharoLinks(): OcharoLinks {
  return inject(OcharoLinksKey, () => resolveLinks([buildTimeConfigSource()]).links, true);
}

/** The app menu's destinations. */
export function useOcharoApps(): OcharoApp[] {
  return ocharoApps(useOcharoLinks().addresses);
}

/** The Brand API root the print sheet reads the published brand kit from, or `null` when none is usable. */
export function useBrandApiUrl(): string | null {
  return useOcharoLinks().brandApiUrl;
}
