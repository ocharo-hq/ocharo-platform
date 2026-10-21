# ventures.ocharo-catalogue — Frontend Development Guidelines

Defer to `wow-two-ws/conventions/development/frontend/`.

## Workspace

- Frontend root: `engineering/codebase/ocharo-catalogue.frontend-services/`.
- Product app: private `@ocharocatalogue/web` under `apps/web/`.
- Runtime: Node `24.11.0` from `.nvmrc`; package manager: `pnpm@10.33.2`.
- Shared packages use `packages/*` only when there is shared code; no runtime federation is configured.
- App source uses `bootstrap`, `presentation`, `application`, `domain`, and `integration` layers.
- The greeting endpoint returns plain `{ message }`; the SDK client uses `rawEnvelope` and validates that wire shape.

## Frame and configuration

- `bootstrap/AppLayout.vue` is the one operator frame; `bootstrap/PublicReaderLayout.vue` frames only the published reader.
- `index.html` carries `theme-ocharo`; `vite.config.ts` serves and emits `/theme.js`, the SDK `colorModeScript` for
  `ocharo.catalogue.theme`. The sidebar rail choice is stored under `ocharo.catalogue.sidebar-collapsed`.
- `application/shared/UseOcharoApps.ts` is the config seam. Startup reads `/api/runtime-config` (3 s bound) ahead of
  the `VITE_` build values and the local defaults. Keys: `OCHARO_DESK_URL`, `OCHARO_CATALOGUE_URL`, `OCHARO_STUDIO_URL`,
  `OCHARO_MARKETING_URL`, `OCHARO_EDITORIAL_URL`, `OCHARO_BRAND_URL`, `OCHARO_ATLAS_URL`, `OCHARO_WEAR_URL`,
  `OCHARO_BLOG_URL` (served at runtime) and `OCHARO_BRAND_API_URL` (build-time only, the print sheet's Brand API).
- The account menu holds appearance, interface language and the Catalogue and API versions; with an Ocharo session it
  also names the operator by their Desk name and email from `/api/sso/session` (else "Signed in through Ocharo Desk"),
  links the Desk account in a new tab and offers sign-out.
- The frame glass comes from the SDK `ocharo` theme: `variant="glass"` on the top bars and sidebar, `variant="glass-menu"`
  on the menus, notifications and phone sheet, `surface-glass!` on the phone bar (the SDK `BottomNavMenu` paints
  `bg-background` and takes no glass variant yet). `index.css` keeps no glass copy.
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

Typecheck includes the Vue SFC compiler. Tests cover greeting loading, success, failure, malformed responses,
request cancellation, and an illegal architecture import.

## Delivery

`pnpm build` produces `apps/web/dist/`. `pnpm deploy` rebuilds and replaces the generated API `wwwroot/` artifact.
Run `deploy` only when replacing that generated frontend is intended.
Docker installs from the workspace lockfile and copies the same `apps/web/dist/` artifact into the .NET image.
API calls remain same-origin in development and production.
