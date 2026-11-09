# ventures.ocharo-catalogue — Frontend Development Guidelines

*Last updated: 2026-11-09*

Defer to `wow-two-ws/conventions/development/frontend/`.

## Workspace

- Frontend root: `engineering/codebase/ocharo-catalogue.frontend-services/`.
- Product app: private `@ocharocatalogue/web` under `apps/web/`, on `@wow-two-beta/fe 0.0.41` with its optional peer
  `@tanstack/vue-query`.
- Runtime: Node `24.11.0` from `.nvmrc`; package manager: `pnpm@10.33.2`.
- Shared packages use `packages/*` only when there is shared code; no runtime federation is configured.
- App source uses `bootstrap`, `presentation`, `application`, `domain`, and `integration` layers.
- API clients use the SDK's `wowTwoEnvelope`: payloads arrive under `{ data }`, failures as ProblemDetails. The API's
  JSON leaves null fields out, so `integration/catalogue/CatalogueWire.ts` (and the edition decoder) restore them as
  `null` for the app's models. The public feed (`/api/public/catalogue`) alone stays unenveloped (`unwrap: false`).
- Every endpoint returns a `Result`: `integration/catalogue/CatalogueApi.ts`, `integration/design/DesignApi.ts` (multipart
  release preparation included), the design images an export reads (`fetchDesignImage` in `DesignExport.ts`) and the API
  status (`BuildIdentityApi.ts`); none throws for a request failure. `describeFailure` maps a failure to safe copy.
- Session: `integration/common/IdentitySession.ts` holds the SDK `createBffStrategy` (`/api/identity`: `GET /session`,
  `GET /sign-in?returnUrl=`, `POST /sign-out`, tab sync, refresh before `expiresAt`), the `createAuthBridge` keyed by
  person and organization, and `catalogueHttp`, the one `createApiClient` every private call rides (CSRF token on unsafe
  requests, the session's request scope, 401 to the bridge; a 403 or a stale-token 400 re-reads the session).
  `AppRoot.vue` mounts the SDK `AuthProvider`; `UseIdentitySession.ts` reads the session through the bridge and its
  commands through `useBffAuth`. The layout route's guard (`requireCatalogueSession` in `CatalogueRouter.ts`) awaits the
  session and leaves for sign-in without one; a `signInError` return or a session read the API did not answer opens the
  frame, which says so and offers sign-in in another tab. The published reader reads no session until the frame is entered.
- Server reads: the catalogue settings (`application/catalogue/UseCatalogueSettings.ts`, key
  `CatalogueQueryKeys.settings`) and the Brand kit sit in the SDK query cache; `bootstrap/CatalogueQueryClient.ts` binds
  the client to the bridge's scope, so sign-out, expiry or another person or organization clears them. Reads do not
  retry on their own. Page grids keep their own reads, refreshed through the toolbar.
- Local state goes through the SDK storage seam (JSON): the interface language (`ocharo.operator.locale`) and a guest's
  saved pieces (`ocharo.catalogue.saved-pieces`) in `localStorage`, a designer's recovery copy
  (`ocharo-catalogue-design:{id}`) in this tab's `sessionStorage` through `integration/common/SessionStorageBroker.ts`.
- Controls: SDK `Button`, `Card`, `Badge`, `Field`, `LabelText` and form fields (`TextInput`, `TextAreaInput`,
  `NumberInput`, `SearchInput`, `CheckboxField`, `SliderInput`, `ColorPicker`, `TagsInput`, `SegmentedPicker`,
  `SelectPicker` through `CatalogueSelect`); `index.css` keeps no hand-styled control utilities. The designer canvases,
  the published reader's catalogue pages and the hidden image file input keep their own markup.

## Frame and configuration

- `bootstrap/AppLayout.vue` is the one operator frame; `bootstrap/PublicReaderLayout.vue` frames only the published reader.
- `index.html` carries `theme-ocharo`, the Ocharo symbol icons (`favicon.png`, `favicon-dark.png`,
  `apple-touch-icon.png`) and the splash's static twin; `vite.config.ts` serves and emits `/theme.js`, the SDK
  `colorModeScript` for `ocharo.catalogue.theme`. The sidebar rail choice is stored under
  `ocharo.catalogue.sidebar-collapsed`.
- `application/shared/UseOcharoApps.ts` is the config seam. Startup reads `/api/runtime-config` (3 s bound) ahead of
  the `VITE_` build values and the local defaults. Keys: `OCHARO_DESK_URL`, `OCHARO_CATALOGUE_URL`, `OCHARO_STUDIO_URL`,
  `OCHARO_MARKETING_URL`, `OCHARO_EDITORIAL_URL`, `OCHARO_BRAND_URL`, `OCHARO_ATLAS_URL`, `OCHARO_WEAR_URL`,
  `OCHARO_BLOG_URL` (served at runtime) and `OCHARO_BRAND_API_URL` (build-time only, the print sheet's Brand API).
- The account menu holds appearance, interface language and the Catalogue and API versions; with an Ocharo session it
  also names the person by their name, then email or phone, from `/api/identity/session`, and offers sign-out.
- The frame glass comes from the SDK `ocharo` theme: `variant="glass"` on the top bars, sidebar and phone bar, and
  `variant="glass-menu"` on the menus, notifications and phone sheet. The phone bar's `BottomNavMenu` supplies its top
  edge, upward `--glass-bottom-shadow` and safe-area padding. `index.css` keeps no glass copy.
- Frame copy uses English message keys with uz/ru entries; an `en` entry is set only where the key is not the English
  phrase, as for the apps menu's `Apps menu: Editorial`, apart from the designer's Editorial style.

## Commands

Run from the frontend root:

```sh
nvm use
pnpm install --frozen-lockfile
pnpm dev
```

The Vite app uses HTTPS on `8282` and proxies `/api` to `OcharoCatalogue.Api` on `8278`.
The local proxy checks for an existing .NET development certificate, exports only its public PEM
into a temporary directory, and uses that certificate for verified loopback HTTPS. Temporary files
are removed immediately; no certificate is created or trusted, and no private key is exported.
For a custom backend certificate, supply its public PEM path through `VITE_API_CA_PATH`.
Remote API targets retain Node's normal certificate verification. Frontend builds need no local certificates.
`VITE_API_TARGET` can select another HTTPS backend; the default is `https://localhost:8278`.
Keep Rider and WebStorm runtimes user-owned; restart the frontend when certificate configuration changes.

```sh
pnpm typecheck
pnpm test
pnpm format:check
pnpm lint
pnpm build
```

Typecheck includes the Vue SFC compiler. Tests cover the frame, the account menu and the SDK BFF session (sign-in
redirect, failed sign-in, unanswered session, sign-out, CSRF, expiry, refusals, the reader without a session), the
query-backed settings and their session lifetime, the storage seam, the first paint (icons, splash twin), the API
clients, the designers and the reader. `tests/tests.md` describes the setup. The browser suite (`pnpm e2e`,
`apps/web/e2e/e2e.md`) drives a real `OcharoCatalogue.Api`, which since 2026-11-03 starts only with Ocharo's Identity
configured (`Catalogue:OrganizationId`, `Identity:Client`) and answers every authoring route only to a session.

## Delivery

`pnpm build` produces `apps/web/dist/` with `theme.js`, the Ocharo symbol icons and the splash wordmarks under `brand/`.
`pnpm deploy` rebuilds and replaces the generated API `wwwroot/` artifact.
Run `deploy` only when replacing that generated frontend is intended.
Docker installs from the workspace lockfile and copies the same `apps/web/dist/` artifact into the .NET image.
API calls remain same-origin in development and production.
