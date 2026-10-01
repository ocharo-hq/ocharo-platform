# ventures.ocharo-brand — Frontend Development Guidelines

Defer to `wow-two-ws/conventions/development/frontend/`.

## Workspace

- Frontend root: `engineering/codebase/ocharo-brand.frontend-services/`.
- Product app: private `@ocharobrand/web` under `apps/web/`.
- Runtime: Node `24.11.0` from `.nvmrc`; package manager: `pnpm@10.33.2`.
- Shared packages use `packages/*` only when there is shared code; no runtime federation is configured.
- App source uses `bootstrap`, `presentation`, `application`, `domain`, and `integration` layers.
- The greeting endpoint returns plain `{ message }`; the SDK client uses `rawEnvelope` and validates that wire shape.

## Commands

Run from the frontend root:

```sh
nvm use
pnpm install --frozen-lockfile
pnpm dev
```

The Vite app uses HTTPS on `8224` and proxies `/api` to the backend HTTPS profile on `8220`.
`pnpm dev:http` or `VITE_HTTPS=false pnpm dev` skips local certificate setup for headless checks.
`VITE_API_TARGET` can select a running local backend for those checks; the default is `https://localhost:8220`.

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
