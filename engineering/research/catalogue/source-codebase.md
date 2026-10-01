# codebase/

The code lives in `ocharo-catalogue.backend-services/` (.NET) and `ocharo-catalogue.frontend-services/` (Vue).

The frontend is a private pnpm workspace. Its product app is `apps/web/`; optional shared code uses `packages/`.
Installation and root `dev`, `typecheck`, `test`, `build`, and `deploy` commands run from that workspace root.
The app owns its dependencies, Vite and TypeScript configuration, five-layer `src/`, tests, and `dist/` output.
`scripts/deploy.mjs` copies `apps/web/dist/` to `ocharo-catalogue.backend-services/OcharoCatalogue.Api/wwwroot/`.

Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
