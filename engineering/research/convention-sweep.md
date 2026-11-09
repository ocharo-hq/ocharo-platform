# Convention sweep — 2026-11-09

*Last updated: 2026-11-09*

Inventory of every break against the WoW2 conventions found in this repo on 2026-11-09: Desk (`brand-workspace.*`, `ocharo-image.backend-services`, deployment, repo docs, planning), Brand and Catalogue (`ocharo-brand.*`, `ocharo-catalogue.*`). Found by read-only agents sweeping the repo against `wow-two-ws/conventions/conventions.md`; recorded exceptions are excluded. Ids are stable (`D-`, `B-`, `C-`); the backlog rows in [backlog.md](../planning/backlog.md) group them by theme.

Size: S < 1 h, M ≤ 1 day, L > 1 day. `known` = already in `convention-audit.md`, the backlog or `deploy.yml` comments.

## Summary

| Area | Rows | S | M | L | Changes behaviour or data (yes) | No |
|---|---|---|---|---|---|---|
| Desk — repo and docs | 6 | 5 | 1 | 0 | 1 | 5 |
| Desk — planning | 6 | 1 | 4 | 1 | 0 | 6 |
| Desk — deployment | 4 | 2 | 2 | 0 | 0 | 4 |
| Desk — backend | 16 | 6 | 5 | 5 | 3 | 13 |
| Desk — frontend | 8 | 6 | 1 | 1 | 2 | 6 |
| Brand | 28 | 14 | 11 | 3 | 9 | 19 |
| Catalogue | 33 | 16 | 13 | 4 | 9 | 24 |
| **Total** | 101 | 50 | 37 | 14 | 24 | 77 |

Evidence caveats: D-24 counts `NpgsqlCommand|CreateCommand` matches; D-28, D-29 and D-30 come from regex scans and are approximate; D-34 counts files directly in each slice folder.

## Scope and paths

- Desk `where` paths are repo-relative to `ocharo-platform/`; `rule` is relative to `wow-two-ws/` (`conventions/…`) unless it starts with `~`. Backend root `B/` = `engineering/codebase/brand-workspace.backend-services`; frontend root `F/` = `engineering/codebase/brand-workspace.frontend-services/apps/web`.
- Brand and Catalogue `where` paths are relative to `engineering/`; `rule` paths are relative to `wow-two-ws/conventions/`, with `dev/` = `development/`, `be/` = `development/backend/dotnet/`, `fe/` = `development/frontend/`.
- Excluded as recorded exceptions: `BrandWorkspace` namespaces, `brand-workspace.*` slugs, the `ocharo-*` companion slugs and namespaces, Desk's Domain project referencing Brand and Catalogue Domain projects, the HTTPS-only local transport, the WoW2 Files adoption code, lock files.
- Conforming, checked: `.slnx`, `Directory.*` props, central packages, five Clean projects, `SystemController`, five-layer frontend with linted boundaries, `createAppRouter`, `ColorModeProvider`, `lucide-vue-next`, `AppShell`, CI workflow, `.dockerignore`, LFS.
- Not checked: responsive and screen-class rules, the design spec, `scripts/active.sh` ledger rows.
- `known` rows: D-14 (`configCheck` part), D-31, D-32, B-7, B-9, C-7, C-9.

## Desk

### Repo and docs

| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| D-1 | `product/product.json:1`; `engineering/deployment/deploy.yml:14` | `conventions/deployment/descriptor/deploy-descriptor.md:199` | Retired `product.json` still holds identity; `deploy.yml` has no `name`/`description`. | S | no | Add `name`, `description` to `deploy.yml`; delete `product.json`. |
| D-2 | `product/brand/` (no `icons/`); `F/public/` + `F/index.html:3-18` (no favicon, no touch icon) | `conventions/design/identity/product-icons.md:49`; `conventions/development/frontend/shapes/app/platform/document.md:28,31` | No `product/brand/icons/` variants; Desk tab shows no favicon or apple-touch icon. | M | yes | Export short-colored icons; add `favicon.svg`, `apple-touch-icon.png`, head links. |
| D-3 | `product/features/`, `product/brand/`, `engineering/scripts/`, `engineering/deployment/tests/`, `engineering/deployment/image-worker/` | `conventions/development/repo/structure/repo-structure.md:90` | 5 folders hold 2+ docs or files with no `{folder}.md` lead doc. | S | no | Add `features.md`, `brand.md`, `scripts.md` and the other leads. |
| D-4 | `engineering/scripts/README.ocharo-templates.md` | `conventions/development/repo/structure/repo-structure.md:89` | `README.md`-named doc below the repo root. | S | no | Rename to `ocharo-templates.md`; fix links. |
| D-5 | `AGENTS.md:1` | `~/.claude/conventions/tools.md:16` | Codex entry file kept; Codex was dropped 2026-10-23. | S | no | Delete `AGENTS.md`; keep `CLAUDE.md`. |
| D-6 | `B/tests/BrandWorkspace.Tests.E2E/tests-e2e.md`; `B/tests/BrandWorkspace.Tests.Unit/tests-unit.md` | `conventions/development/repo/structure/repo-structure.md:90` (`{folder}.md`) | Test lead docs not named after their project folder (2). | S | no | Rename to `BrandWorkspace.Tests.E2E.md` and `.Unit.md`. |

### Planning

| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| D-7 | `engineering/planning/files-adoption.md` (309 lines); `engineering/planning/identity-cutover.md` (401 lines) | `conventions/planning/version-track/version-track.md:17` | Two extra planning files beside backlog and version track. | M | no | Move into `v0.1` tasks, backlog rows or `engineering/architecture/`. |
| D-8 | `engineering/planning/version-track/v0.1/v0.1.md:5` (Started 2026-10-01, 25 iterations, 428 lines) | `conventions/planning/version-track/version-track.md:66,69` | One Feature version spans 5+ weeks; no `Adoption` version follows. | L | no | Close `v0.1`; split remaining work into `v0.2` Adoption and `v0.3`. |
| D-9 | `engineering/planning/version-track/v0.1/v0.1.md` (86 of 186 task lines) | `conventions/planning/version-track/version-track.md:126` | 86 task lines exceed the 75-character cap. | M | no | Rewrite each as one short verb-first action. |
| D-10 | `engineering/planning/version-track/v0.1/v0.1.md` (about 112 prose lines, e.g. `:43-44`, `:301`, `:346`, `:369`) | `conventions/planning/version-track/version-track.md:149,162` | Prose and re-scoping history left under done and open iterations. | M | no | Strip prose; move lasting facts to architecture or `context.md`. |
| D-11 | `engineering/planning/version-track/v0.1/v0.1.md:28` (`### Iteration 4 — Verification`) | `conventions/planning/version-track/version-track.md:98` | A second `Verification` iteration sits mid-document; the final one is separate. | S | no | Rename it to its subject; keep one last `### Verification`. |
| D-12 | `engineering/planning/backlog.md:42-94` | `conventions/planning/version-track/version-track.md:33,35` | Shipped items listed; groups use dated names and `[ ]` bullets, not `Item · Type · Notes`. | M | no | Delete shipped items; convert groups to tables; drop dates in names. |

### Deployment

| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| D-13 | `B/BrandWorkspace.Api/appsettings.json:1`; `B/BrandWorkspace.Worker/appsettings.json:1`; `engineering/deployment/deploy.yml` (no `dotnetEnvironment`) | `conventions/deployment/hosting/environment-files.md:10-14`; `conventions/development/repo/structure/product-conformance.md:101` | Loopback DB and `AllowedHosts` sit in `appsettings.json`; no `Development.json`; dev runs as Production. | M | no | Move local values to `appsettings.Development.json`; set `dotnetEnvironment: true`. |
| D-14 | `engineering/deployment/deploy.yml:23-26,31-34`; `B/BrandWorkspace.Api/Configurations/HostConfiguration.Extensions.cs:471` | `conventions/deployment/descriptor/deploy-descriptor.md:46,47` | `desk` hosts a migration set but has no `migrate:` item; no `configCheck:` for desk, jobs, image-worker (known, `deploy.yml:27-30`). | M | no | Ship `migrate` and `config check` verbs; add both descriptor items. |
| D-15 | `engineering/deployment/deploy.yml:46`; `engineering/deployment/Dockerfile` (HEALTHCHECK) | `conventions/deployment/descriptor/deploy-descriptor.md:36` | Desk health points at hand-written `/health/ready`, not the boot bundle's `/health`. | S | no | Follow D-18: adopt bundle `/health`; repoint descriptor and Dockerfile. |
| D-16 | `engineering/deployment/deploy.yml:110-113`; `engineering/codebase/ocharo-image.backend-services/Dockerfile` | `conventions/development/repo/structure/repo-structure.md:145,146` | Image-worker Dockerfile lives under `codebase/`, with its own build context. | S | no | Move to `engineering/deployment/`; build from the `codebase` context. |

### Backend

| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| D-17 | `B/BrandWorkspace.Api/Program.cs:3-14`; `Configurations/HostConfiguration.cs:15-21` | `conventions/development/backend/dotnet/shapes/service/platform/startup/host-configuration.md:33,43`; `startup-defaults.md:8` | No `ServiceHost.RunAsync` or `AddApiDefaults`/`UseApiDefaults`; boot and `--migrate`, `--import-*` flags hand-written. | L | no | Boot through the SDK bundle; turn flags into registered verbs. |
| D-18 | `B/BrandWorkspace.Api/Configurations/HostConfiguration.Extensions.cs:375-387` | `conventions/development/backend/dotnet/shapes/service/platform/startup/startup-defaults.md:46` | Hand-written `/health/live` and `/health/ready` routes. | S | no | Use bundle `/health` plus `AddDatabaseReadinessCheck`; delete the routes. |
| D-19 | `B/BrandWorkspace.Api/Configurations/HostConfiguration.Extensions.cs:370,388` | `conventions/development/backend/dotnet/shapes/service/platform/responses/known-endpoints.md:42,48` | Status and version are two minimal routes; status omits the informational version. | S | no | Add `SystemController.Status` returning version; retire `/api/system/version`. |
| D-20 | `B/BrandWorkspace.Api/Configurations/HostConfiguration.Extensions.cs:349,403` | `conventions/deployment/hosting/single-host-serving.md:43` | SPA served by hand-wired `UseStaticFiles` and `MapFallbackToFile`. | S | no | Set `ApiDefaultsOptions.SpaHosting`; delete the hand-wiring. |
| D-21 | `B/BrandWorkspace.Api/Configurations/HostConfiguration.Extensions.cs:315` | `conventions/development/repo/structure/product-conformance.md:59,63` | `AddControllers()` without `AddControllersWithSdkJson()`. | S | yes | Adopt it; retype responses; follow `vue-tsc` (wire change). |
| D-22 | `B/BrandWorkspace.Api/**/*Endpoints.cs`, `*Configuration.cs` (71 `Map*` routes, 1 controller) | `conventions/development/backend/dotnet/shapes/service/architecture/clean/clean.md:61`; `conventions/development/backend/dotnet/core/mla/constructs/behavior/controller.md:35` | Routes are minimal-API lambdas, not `*Controller` classes under `Controllers/`. | L | no | Convert each domain's routes to a sealed controller. |
| D-23 | `B/BrandWorkspace.Infrastructure/**/Services/*.cs`; `B/BrandWorkspace.Application/Preparation/Models/PreparationException.cs` (163 `throw new`) | `conventions/development/backend/dotnet/shapes/service/architecture/clean/clean.md:55`; `…/platform/responses/results.md:10` | No mediator, handlers or `AppResult`; services throw exceptions that filters map. | L | no | Introduce handlers returning `AppResult`; drop `PreparationErrorFilter`. |
| D-24 | `B/BrandWorkspace.Persistence/**/*Repository.cs` (11 repos, ~100 `NpgsqlCommand` sites) | `conventions/development/backend/dotnet/core/mla/domains/persistence/access/dapper/dapper.md:18` | Repositories use raw `NpgsqlDataSource` and commands, not `IDbConnectionFactory` with Dapper. | L | no | Move reads and writes to Dapper over the connection factory. |
| D-25 | `B/BrandWorkspace.Application/Commerce/CommerceRules.cs:11` (+ `PublicProjection`, `CatalogueDefinition`, `Files/DeskFiles.cs:97`) | `conventions/development/backend/dotnet/shapes/service/architecture/clean/clean.md:54` | Application holds validation, projection and fallback logic, not contracts only. | M | no | Move behaviour to Infrastructure; keep interfaces and models. |
| D-26 | `B/BrandWorkspace.{Application,Infrastructure,Persistence}/{Commerce,Access,Files}/` (about 19 files) | `conventions/development/backend/dotnet/shapes/service/architecture/clean/domain-structuring.md:24` | Files sit loose under a domain; no `Services/`, `Repositories/` or `Models/` folder. | S | no | Create role folders; move files; fix usings. |
| D-27 | `B/BrandWorkspace.Domain/Commerce/CommerceModels.cs` (21 types); 15 more files hold 2–9 types | `conventions/development/backend/dotnet/core/mla/mla.md:19` | One type per file broken in 16 files. | M | no | Split each type into its own file. |
| D-28 | `B/BrandWorkspace.Persistence/Commerce/CommerceRepository.cs` (88 long lines) and 62 more files; 811 lines over 120 chars | `conventions/development/backend/dotnet/core/lla/notation/style/style.md:162` | 811 source lines (484 more in tests) exceed the hard 120-character limit. | M | no | Run the formatter; wrap per the signature rule. |
| D-29 | `B/BrandWorkspace.*` (41 of 172 types lack `<summary>`) | `conventions/development/backend/dotnet/core/mla/constructs/behavior/repository.md:20` | 41 public types have no type doc. | M | no | Add type docs in each role's starter form. |
| D-30 | `B/tests/` (52 of 150 test methods) | `conventions/development/backend/dotnet/shapes/service/architecture/clean/testing.md:56` | 52 tests miss `{Unit}_Should{Expectation}_When{Condition}`. | M | no | Rename them. |
| D-31 | `B/BrandWorkspace.Persistence/Migrations/002-access`; `Foundation/…`; `Access/AccessDbContext.cs` (known, backlog "Desk's retired tables") | `conventions/development/backend/dotnet/core/mla/domains/persistence/migrations/migrations.md:59` | Desk's own identity tables, EF store and `access.sessions` linger after the Identity cutover. | L | yes | Write `011-identity-cutover`; drop tables; remove `AddIdentityCore`. |
| D-32 | `B/BrandWorkspace.Api/Foundation`; `BuildIdentity.cs` (known, `convention-audit.md` gap 1, backlog "Migration version adoption") | `conventions/development/backend/dotnet/core/mla/domains/persistence/migrations/migrations.md:31` (`MigrationOptions.Version`) | Migration stamp strips `+commit`; SDK column caps versions at 20 characters. | S | yes | Adopt the published SDK field for full versions; stop truncating. |

### Frontend

| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| D-33 | `F/src/{domain,application,integration,presentation}/shared/`; `F/eslint.config.mjs:34` | `conventions/development/frontend/shapes/app/architecture/architecture.md:62` | Cross-domain slice named `shared/`, not `common/`; lint allows `shared`. | S | no | Rename 4 folders to `common/`; update the lint rule. |
| D-34 | `F/src/presentation/workspace/` (51 flat files); `F/src/domain/workspace/` (21); `F/src/application/workspace/` (12) | `conventions/development/frontend/shapes/app/architecture/architecture.md:49,53,61` | One `workspace` slice holds every domain, with no sub-domains or role folders. | L | no | Slice by domain (sourcing, events, catalogue, design, commerce); add `hooks/`, `models/`. |
| D-35 | `F/src/bootstrap/router.ts:19-27`; `F/src/presentation/workspace/frame/DeskFrame.vue` | `conventions/development/frontend/shapes/app/routing/routing.md:9,22,27,46` | One `*` route; no declared places, not-found or guards; layout sits in presentation. | M | no | Declare places in `bootstrap/router/`; add `AppLayout`, `AppErrorBoundary`. |
| D-36 | `F/src/presentation/workspace/OverviewPage.vue:144-146,177-181` (8 inline `<svg>`) | `conventions/development/frontend/core/mla/domains/icons/lucide/lucide.md:14` | SVG glyphs pasted into a template, not `lucide-vue-next` imports. | S | yes | Import Lucide glyphs; wrap brand marks in adapters. |
| D-37 | `F/src/presentation/workspace/DraftEditorPage.vue:292`; `DraftListPage.vue:63` | `conventions/development/frontend/core/mla/domains/i18n/i18n.md:38` | Dates go through `toLocaleString`/`toLocaleDateString`; Uzbek prints `2026 M09 28`. | S | yes | Use an app formatter with Uzbek month names. |
| D-38 | `F/src/integration/shared/BuildIdentityApi.ts:4`; `F/src/integration/workspace/DesignExport.ts:8-11` (12 `throw` in 3 integration files) | `conventions/development/frontend/core/mla/domains/data/state-and-data.md:13` | Raw `fetch` and thrown `Error` for expected request failures, not `Result`. | S | no | Use SDK http client; return `Result` for expected failures. |
| D-39 | `F/src/presentation/workspace/OverviewPage.vue:69,112` | `conventions/development/frontend/core/mla/domains/data/state-and-data.md:34` (`application/` owns state) | Presentation reads and writes `localStorage` directly. | S | no | Use the SDK storage broker via an application hook. |
| D-40 | `F/vite.config.ts:41,55` | `conventions/development/frontend/shapes/app/platform/dev-server.md:13,24` | `mkcert` always on in serve; no `VITE_HTTPS=false` HTTP fallback or `*-http` launch. | S | no | Gate mkcert on `VITE_HTTPS`; add the `*-http` launch entry. |

## Brand


| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| B-1 | `codebase/ocharo-brand.backend-services/OcharoBrand.Api/Program.cs:4-12` | `be/shapes/service/platform/startup/host-configuration.md:33` | `Program.cs` builds the host by hand; no `ServiceHost.RunAsync`, so no `migrate` or `config check` verb. | M | no | Call `ServiceHost.RunAsync`; register `config check` once Postgres lands. |
| B-2 | `…/OcharoBrand.Api/Configurations/HostConfiguration.Extensions.cs:120-164` | `be/shapes/service/platform/startup/startup-defaults.md:9` | No `AddApiDefaults()`/`UseApiDefaults()`; proxy, problem details and pipeline wired by hand. | M | no | Boot through the bundle; keep only Brand's own middleware. |
| B-3 | `…/HostConfiguration.Extensions.cs:130` | `be/shapes/service/platform/responses/serialization.md:24` | `AddControllers()` instead of `AddControllersWithSdkJson()`. | S | yes | Adopt it; retype frontend models (nulls omitted, enum casing); run `vue-tsc`. |
| B-4 | `…/HostConfiguration.Extensions.cs:144-145,162` | `deployment/hosting/single-host-serving.md:47` | Hand-wired `UseDefaultFiles`, `UseStaticFiles`, `MapFallbackToFile`; no SDK `SpaHosting`. | S | no | Set `ApiDefaultsOptions.SpaHosting`; delete the three calls. |
| B-5 | `…/HostConfiguration.Extensions.cs:154,159` | `be/shapes/service/platform/startup/startup-defaults.md:46` | Hand-written `/health` always answers healthy; no database readiness check. `/api/system/version` duplicates status. | S | no | Use the bundle `/health` plus `AddDatabaseReadinessCheck`; drop the version route. |
| B-6 | `…/OcharoBrand.Api/OcharoBrand.Api.csproj:1-18` | `deployment/hosting/single-host-serving.md:15,60` | No `BuildSpa` MSBuild target; the SPA reaches `wwwroot` only by `pnpm deploy` or the Dockerfile. | S | no | Add the `BuildSpa` target; keep `-p:BuildSpa=false` in the Dockerfile. |
| B-7 | `…/HostConfiguration.Extensions.cs:28-41`; `…/Directory.Packages.props:26,34`; `…/OcharoBrand.Persistence.csproj:9` | `be/core/mla/domains/persistence/database/database.md:15` | Brand stores on SQLite (WAL); SQLite is banned for products. Known: `research/adoption/adoption.md:54`. | L | yes | `AddPostgresDatabase`; move rows, keep IDs and media hashes; BFF sessions then persist. |
| B-8 | `…/HostConfiguration.Extensions.cs:198` | `be/core/mla/domains/persistence/migrations/migrations.md:42` | `EnsureCreated` builds the schema; no bespoke SQL migration set, no version stamp. | L | yes | Write a bespoke SQL set; ship with B-7. |
| B-9 | `deployment/deploy.yml:347-386` | `deployment/descriptor/deploy-descriptor.md:46-47` | `brand` has no `needs: [postgres]`, `migrate:` item or `configCheck:` item. Known: `deploy.yml:30`. | M | no | Add them after B-1 and B-7; drop the `brand-data` file DB. |
| B-10 | `deployment/brand/Dockerfile:14,30` | `dev/repo/versioning/versioning.md:17` | Image takes no `APP_VERSION`/`VCS_REF`; status, About and the bar always say `dev`. | S | yes | Add the two args in the SPA and build stages, as `deployment/Dockerfile:5-14`. |
| B-11 | `codebase/ocharo-brand.backend-services/OcharoBrand.Application/Brand/BrandService.cs:11`; `PublicBrandService.cs:11`; `HostConfiguration.Extensions.cs:59` | `be/shapes/service/architecture/clean/clean.md:54-55` | Application holds the service implementations (369 lines); mediator is registered, no handler exists. | L | no | Move behaviour to Infrastructure handlers; expose commands and queries in Application. |
| B-12 | `…/OcharoBrand.Api/Brand/Controllers/BrandAdminController.cs:10-12`; `PublicBrandController.cs:14` | `be/core/mla/constructs/behavior/controller.md:39` | Both controllers inject `BrandService`, `PublicBrandService`, `IMediaStore`, not `ISender`. | M | no | Dispatch through `ISender`; follows B-11. |
| B-13 | `…/BrandAdminController.cs:7-10`; `…/PublicBrandController.cs:7` | `be/core/mla/constructs/behavior/controller.md:24,52` | Type docs lack `Exposes … over HTTP`; `BrandAdminController` is not a plural resource noun. | S | no | Rewrite the summaries; rename to `BrandsController`-style nouns. |
| B-14 | `…/OcharoBrand.Application/Brand/BrandCore.cs:7`; `…/Api/Brand/BrandExceptionMiddleware.cs:14,18`; `…/Api/Access/BrandAccessMiddleware.cs:36` | `be/shapes/service/platform/responses/problem-details.md:23-25,44` | Failures throw `BrandException`; middleware hand-builds `Results.Problem`; no `AppResult`, no shared factory. | M | yes | Return `AppResult`; build bodies with `AppErrorProblemDetailsFactory` and the mappers. |
| B-15 | `…/OcharoBrand.Application/Abstractions/IClock.cs:4`; `…/Infrastructure/Time/SystemClock.cs:8`; `HostConfiguration.Extensions.cs:47` | `be/core/mla/components/time.md:21,38` | Own `IClock` over `DateTimeOffset.UtcNow`; no `TimeProvider`, no `AddTimeProviders()`. | S | no | Inject `TimeProvider`; delete `IClock` and `SystemClock`; tests use `FakeTimeProvider`. |
| B-16 | `…/OcharoBrand.Api/Configurations/HostConfiguration.Access.cs:28-40` | `be/shapes/service/platform/startup/host-configuration.md:10` | Hand-rolled Data Protection key ring; the be sdk ships `AddPersistentDataProtection`. | S | no | Call it (confirm it exists in pinned `10.0.105-beta`); keep the key path. |
| B-17 | `codebase/ocharo-brand.backend-services/tests/OcharoBrand.Tests/`; `OcharoBrand.BackendServices.slnx:7` | `be/shapes/service/architecture/clean/testing.md:41-44,47` | One bare `OcharoBrand.Tests` project, no tier split; solution folder `/tests/`, not `Tests/`. | M | no | Split into `.Tests.Unit`, `.Integration`, `.E2E`; add a `Tests/` solution folder. |
| B-18 | `…/tests/OcharoBrand.Tests/*.cs` (25 tests) | `be/shapes/service/architecture/clean/testing.md:22,23,56` | 14 of 25 names miss `{Unit}_Should…_When…`; 151 xUnit `Assert.` calls, no AwesomeAssertions or `FakeTimeProvider`. | M | no | Rename tests; switch assertions; add fake time. |
| B-19 | `…/OcharoBrand.Application/Brand/BrandDocuments.cs`, `BrandModels.cs`, `BrandRecords.cs`, `BrandCore.cs`; `…/Infrastructure/Brand/MediaStore.cs:92` | `be/core/lla/constructs/constructs.md:112` | Five files declare 2-12 types each (38 types). | S | no | One type per file. |
| B-20 | all folders of `codebase/ocharo-brand.backend-services` (26) and `…frontend-services` (25) | `dev/repo/structure/repo-structure.md:90` | No `{folder}.md` lead doc in any source folder; tests lack the prerequisites doc `testing.md:49`. | M | no | Add one-line leads; at least `tests/` and layer roots. |
| B-21 | `deployment/brand/deployment.md:1`; `deployment/brand/docker-compose.yml:15` | `dev/repo/structure/repo-structure.md:90`; `dev/repo/structure/product-conformance.md:26` | Lead doc is `deployment.md` not `brand.md`; compose binds `127.0.0.1:8200`, off the ledger and equal to Catalogue's. | S | no | Rename to `brand.md`; move or delete the compose file (it no longer boots). |
| B-22 | `codebase/ocharo-brand.frontend-services/apps/web/index.html:1-17`; `public/` | `fe/shapes/app/platform/document.md:25,28,31` | No `favicon.*` or `apple-touch-icon.png` file or `<link>`. | S | yes | Export both from the Ocharo symbol into `public/`; link them. |
| B-23 | `…/apps/web/index.html:15`; `src/bootstrap/main.ts:19-25` | `fe/shapes/app/platform/document.md:46-48` | No splash twin in `#root`, no `SplashScreen`; page is blank until the session resolves. | S | yes | Add the twin to `index.html`/`boot-splash.css` and mount `SplashScreen`. |
| B-24 | `…/src/integration/common/IdentitySessionClient.ts` (243 lines); `src/application/common/UseIdentitySession.ts`; `src/bootstrap/main.ts:19-22` | `dev/sdk-extraction.md:37`; `fe/core/mla/domains/auth/auth.md:15,72-76` | App-local BFF session client and pre-mount sign-in redirect duplicate SDK `createBffStrategy` (fe 0.0.38+). | M | no | Re-pin `@wow-two-beta/fe` (now 0.0.32); adopt the strategy and a route guard. |
| B-25 | `…/src/bootstrap/index.css:62-90`; 21 raw `<button>` in 8 files, 27 raw `input`/`select`/`textarea` in 4 files | `dev/sdk-extraction.md:37`; `dev/repo/structure/product-conformance.md:89` | Hand-styled `btn`, `field`, `panel`, `chip` utilities replace SDK `Button` and form controls (SDK `Button` used once). | M | yes | Swap to SDK `Button` and form fields; delete the utilities. |
| B-26 | `…/src/bootstrap/BrandRouter.ts:47` | `fe/shapes/app/routing/routing.md:27` | Unknown paths render Overview; no not-found place declared. | S | yes | Add a `NotFoundPage` route, as Catalogue does. |
| B-27 | `…/src/integration/common/BuildIdentityApi.ts:4` | `fe/core/mla/domains/data/state-and-data.md:10,13` | Hand `fetch` swallows every failure into the text `'Unavailable'`; no `createApiClient`, no `Result`. | S | no | Use `createApiClient` and return a `Result`. |
| B-28 | `…/src/application/brand/UseBrand.ts:11-24` | `fe/core/mla/domains/data/state-and-data.md:31,36` | Server data held in module-level refs; no query capability, no session-scoped dispose on identity change. | M | no | Move reads to the Vue query capability; clear on sign-out. |

## Catalogue


| id | where | rule | break | size | changes behaviour or data | fix |
|---|---|---|---|---|---|---|
| C-1 | `codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Api/Program.cs:4-12` | `be/shapes/service/platform/startup/host-configuration.md:33` | `Program.cs` builds the host by hand; no `ServiceHost.RunAsync`, so no `migrate` or `config check` verb. | M | no | Call `ServiceHost.RunAsync`; register `config check`. |
| C-2 | `…/OcharoCatalogue.Api/Configurations/HostConfiguration.cs:32-33`; `HostConfiguration.Extensions.cs:105-118` | `be/shapes/service/platform/startup/startup-defaults.md:9` | No `AddApiDefaults()`/`UseApiDefaults()`; proxy, problem details and pipeline wired by hand. | M | no | Boot through the bundle; keep only Catalogue's own middleware. |
| C-3 | `…/HostConfiguration.Extensions.cs:115` | `be/shapes/service/platform/responses/serialization.md:24` | `AddControllers()` instead of `AddControllersWithSdkJson()`. | S | yes | Adopt it; retype frontend models; run `vue-tsc`. |
| C-4 | `…/HostConfiguration.cs:39-40,60` | `deployment/hosting/single-host-serving.md:47` | Hand-wired `UseDefaultFiles`, `UseStaticFiles`, `MapFallbackToFile`; no `SpaHosting`. | S | no | Set `ApiDefaultsOptions.SpaHosting`; delete the three calls. |
| C-5 | `…/HostConfiguration.cs:53,58` | `be/shapes/service/platform/startup/startup-defaults.md:46` | Hand-written `/health` always answers healthy; no database readiness check. `/api/system/version` duplicates status. | S | no | Use the bundle `/health` plus `AddDatabaseReadinessCheck`; drop the version route. |
| C-6 | `…/OcharoCatalogue.Api/OcharoCatalogue.Api.csproj:1-20` | `deployment/hosting/single-host-serving.md:15,60` | No `BuildSpa` MSBuild target; the SPA reaches `wwwroot` only by `pnpm deploy` or the Dockerfile. | S | no | Add the `BuildSpa` target. |
| C-7 | `…/HostConfiguration.Extensions.cs:27-40`; `…/OcharoCatalogue.Persistence/CatalogueDatabaseInitialization.cs:16-21`; `…/Directory.Packages.props:26,35` | `be/core/mla/domains/persistence/database/database.md:15` | Catalogue stores products, revisions and releases on SQLite (WAL); banned for products. Known: `adoption.md:54`. | L | yes | Move to PostgreSQL; keep product IDs, revisions and media hashes; BFF sessions then persist. |
| C-8 | `…/CatalogueDatabaseInitialization.cs:19`; `…/Persistence/Catalogue/CatalogueDesignSchema.cs:10-24` | `be/core/mla/domains/persistence/migrations/migrations.md:19,42` | `EnsureCreated` plus boot-time `CREATE TABLE IF NOT EXISTS` and `ALTER TABLE`; no migration set. | L | yes | One bespoke SQL set with a baseline; ship with C-7. |
| C-9 | `deployment/deploy.yml:388-430` | `deployment/descriptor/deploy-descriptor.md:46-47` | `catalogue` has no `needs: [postgres]`, `migrate:` item or `configCheck:` item. Known: `deploy.yml:30`. | M | no | Add them after C-1 and C-7; drop the `catalogue-data` file DB. |
| C-10 | `deployment/catalogue/Dockerfile:14,29` | `dev/repo/versioning/versioning.md:17` | Image takes no `APP_VERSION`/`VCS_REF`; status, About and the bar always say `dev`. | S | yes | Add the two args in the SPA and build stages. |
| C-11 | `…/OcharoCatalogue.Application/Catalogue/Services/*.cs` (8 services, e.g. `ProductService.cs:11`, 520 lines) | `be/shapes/service/architecture/clean/clean.md:54-55` | Application holds eight service implementations; mediator registered (`Extensions.cs:81-83`), no handler exists. | L | no | Move behaviour to Infrastructure handlers; expose commands and queries in Application. |
| C-12 | `…/Api/Catalogue/Controllers/*.cs` (7 controllers), e.g. `DesignSourceController.cs:12`, `ProductsController.cs:10` | `be/core/mla/constructs/behavior/controller.md:39` | Seven controllers inject services; `DesignSourceController` injects `ICatalogueRepository` directly. | M | no | Dispatch through `ISender`; follows C-11. |
| C-13 | same seven controllers, type docs at line 7-14 | `be/core/mla/constructs/behavior/controller.md:24,52` | Type docs lack `Exposes … over HTTP`; `DesignSourceController` has none; `CatalogueController` is not a plural noun. | S | no | Rewrite the summaries; rename. |
| C-14 | `…/Application/Catalogue/` (Services, Models, Repositories) | `be/shapes/service/architecture/clean/domain-structuring.md:9-15` | Products, collections, designs, releases and export share one `Catalogue/` bucket grouped by role. | M | no | Split into `Products/`, `Collections/`, `Designs/`, `Publishing/` with a `Core/`. |
| C-15 | `…/Application/Catalogue/CatalogueException.cs:4`; `…/Api/Catalogue/CatalogueExceptionMiddleware.cs:14,18`; `CatalogueHttpBoundaryMiddleware.cs:27`; `HostConfiguration.Access.cs:71` | `be/shapes/service/platform/responses/problem-details.md:23-25,44` | Failures throw `CatalogueException`; middleware hand-builds `Results.Problem`; no `AppResult`. | M | yes | Return `AppResult`; use `AppErrorProblemDetailsFactory` and the mappers. |
| C-16 | `…/Application/Abstractions/IClock.cs:4`; `…/Infrastructure/Time/SystemClock.cs:8`; `HostConfiguration.Extensions.cs:45-49` | `be/core/mla/components/time.md:21,38` | Own `IClock` over `DateTimeOffset.UtcNow`, used by 8 classes; no `TimeProvider`. | S | no | Inject `TimeProvider`; delete `IClock`, `SystemClock`, `AddClock`. |
| C-17 | `…/Api/Configurations/HostConfiguration.Access.cs:50-57` | `be/shapes/service/platform/startup/host-configuration.md:10` | Hand-rolled Data Protection key ring; the be sdk ships `AddPersistentDataProtection`. | S | no | Call it (confirm it exists in pinned `10.0.105-beta`). |
| C-18 | `…/Api/Catalogue/CatalogueHttpBoundaryMiddleware.cs:9,42-43` | `be/shapes/service/platform/startup/host-configuration.md:12-13` | Middleware takes `IConfiguration` and reads `Catalogue:TrustedOrigins` on every write. | S | no | Bind it to `CatalogueOptions` and pass the record. |
| C-19 | `…/OcharoCatalogue.Api/appsettings.json:16-18` | `deployment/hosting/environment-files.md:11,14` | Loopback origin `https://localhost:8282` sits in base `appsettings.json`, so deployed hosts trust it for writes. | S | yes | Move it to `appsettings.Development.json`. |
| C-20 | `codebase/ocharo-catalogue.backend-services/tests/OcharoCatalogue.Tests/`; `OcharoCatalogue.BackendServices.slnx:7` | `be/shapes/service/architecture/clean/testing.md:41-44,47` | One bare `OcharoCatalogue.Tests` project, no tier split; solution folder `/tests/`. | M | no | Split into `.Tests.Unit`, `.Integration`, `.E2E`; add a `Tests/` solution folder. |
| C-21 | `…/tests/OcharoCatalogue.Tests/*.cs` (59 tests) | `be/shapes/service/architecture/clean/testing.md:22,23,56` | 54 of 59 names miss `{Unit}_Should…_When…`; 343 xUnit `Assert.` calls, no AwesomeAssertions or `FakeTimeProvider`. | M | no | Rename tests; switch assertions; add fake time. |
| C-22 | `…/Application/Catalogue/Models/ProductModels.cs` (14 types), `DesignModels.cs` (12), `ProductDocument.cs`, `CatalogueVocabulary.cs`, +13 more | `be/core/lla/constructs/constructs.md:112` | About 17 files declare 2-14 types each (a few come from the Files work). | M | no | One type per file. |
| C-23 | `…/Application/Catalogue/Services/CatalogueDesignValidation.cs:30-40`, `CatalogueReleaseService.cs`, `DesignFingerprint.cs`, `CatalogueDesignService.cs`; `…/Persistence/Catalogue/CatalogueDesignRepository.cs`, `CatalogueDesignSchema.cs`; `…/Infrastructure/Catalogue/DesignArtifactValidator.cs` | `be/core/lla/notation/style/style.md:95-103` | Seven design files use unspaced `if(`, several statements per line and expression bodies that branch or throw. | S | no | Run the formatter; give branching members block bodies. |
| C-24 | all folders of `codebase/ocharo-catalogue.backend-services` (35) and `…frontend-services` (41, one `e2e.md` exists) | `dev/repo/structure/repo-structure.md:90` | No `{folder}.md` lead doc in the source folders; tests lack the prerequisites doc `testing.md:49`. | M | no | Add one-line leads; at least `tests/` and layer roots. |
| C-25 | `deployment/catalogue/deployment.md:1`; `deployment/catalogue/docker-compose.yml:15` | `dev/repo/structure/repo-structure.md:90`; `dev/repo/structure/product-conformance.md:26` | Lead doc is `deployment.md` not `catalogue.md`; compose binds `127.0.0.1:8200`, off the ledger and equal to Brand's. | S | no | Rename to `catalogue.md`; move or delete the compose file (it no longer boots). |
| C-26 | `codebase/ocharo-catalogue.frontend-services/apps/web/index.html:1-17`; `public/` | `fe/shapes/app/platform/document.md:25,28,31` | No `favicon.*` or `apple-touch-icon.png` file or `<link>`. | S | yes | Export both from the Ocharo symbol into `public/`; link them. |
| C-27 | `…/apps/web/index.html:15`; `src/bootstrap/main.ts` | `fe/shapes/app/platform/document.md:46-48` | No splash twin in `#root`, no `SplashScreen` on first load. | S | yes | Add the twin to `index.html`/`boot-splash.css` and mount `SplashScreen`. |
| C-28 | `…/src/integration/common/IdentitySessionClient.ts:74` (253 lines); `src/application/common/UseIdentitySession.ts` | `dev/sdk-extraction.md:37`; `fe/core/mla/domains/auth/auth.md:15,72-76` | App-local BFF session client duplicates SDK `createBffStrategy` (fe 0.0.38+). | M | no | Re-pin `@wow-two-beta/fe` (now 0.0.32); adopt the strategy. |
| C-29 | `…/src/bootstrap/index.css:49-77`; 113 raw `<button>` in 22 files, 77 raw `input`/`select`/`textarea` in 15 files, 87 `btn-*` classes | `dev/sdk-extraction.md:37`; `dev/repo/structure/product-conformance.md:89` | Hand-styled `btn`, `field`, `panel`, `chip` utilities replace SDK `Button` and form controls (SDK `Button` used once). | L | yes | Swap to SDK `Button` and form fields; delete the utilities. |
| C-30 | `…/src/integration/design/DesignApi.ts:85,161-169`; `DesignExport.ts:12-19` | `fe/core/mla/domains/data/state-and-data.md:13,15` | Design API throws `DesignRequestError` and plain `Error`; `DesignExport` fetches by hand; no `Result`. | M | no | Return `Result`; map failures at the integration edge. |
| C-31 | `…/src/integration/common/BuildIdentityApi.ts:4` | `fe/core/mla/domains/data/state-and-data.md:10,13` | Hand `fetch` swallows every failure into the text `'Unavailable'`. | S | no | Use `createApiClient` and return a `Result`. |
| C-32 | `…/apps/web/package.json:20`; `src/application/catalogue/CatalogueStore.ts:10-14` | `fe/core/mla/domains/data/state-and-data.md:31,36` | `@tanstack/vue-query` is declared, never imported; server data sits in module-level refs. | M | no | Use the query capability for reads, or drop the dependency; clear caches on sign-out. |
| C-33 | `…/src/application/shared/UseUiLocale.ts:12,28`; `src/presentation/public/CatalogueEditionPage.vue:31,74`; `src/application/design/UseCatalogueDocument.ts:106-417` | `fe/core/mla/domains/data/state-and-data.md:37` | Direct `localStorage`/`sessionStorage` calls (9 sites); Brand uses the SDK storage broker. | S | no | Use `usePersistentState` / the storage broker. |

## Notes

- Brand and Catalogue share 15 rows in kind: B-1/C-1, B-2/C-2, B-3/C-3, B-4/C-4, B-5/C-5, B-6/C-6, B-7/C-7, B-8/C-8, B-9/C-9, B-10/C-10, B-11/C-11, B-12/C-12, B-14/C-15, B-15/C-16, B-16/C-17. One host-composition fix serves both.
- `deploy.yml:12` runs every service as .NET `Production`; `dotnetEnvironment: true` is only a "should" (`deployment/descriptor/deploy-descriptor.md:50`) and depends on Desk's `appsettings.Development.json` (D-13).
- Both companions pin `@wow-two-beta/fe` 0.0.32 (latest tag `fe-v0.0.41`) and be sdk `10.0.105-beta` (latest tag `v10.0.106-beta`); a re-pin takes B-24/C-28 and the BFF route alignment (`fix!` in fe 0.0.41).

## Status

As of 2026-11-09 every row is fixed in v0.2 or kept as an exception below. D-31 shipped as migration
`013-identity-cutover` (not 011: deployed databases already hold 012), dropping the rows outright after the developer
answered "completely fine" to dropping or archiving them. B-7–B-9 and C-7–C-9 moved Brand and Catalogue to
PostgreSQL schemas `brand` and `catalogue`; each environment's one-time `import-sqlite` copy follows its deploy.

## Exceptions

Rows kept in part while fixing them in v0.2, each with what lifts it.

| id | kept | why | lifts when |
|---|---|---|---|
| C-3 | Catalogue's `/api/public/catalogue` feed keeps the old web defaults (`PublicFeedJson`): nulls written, no envelope | Marketing's `CatalogueSyncService` reads `pricing` and `retail` without checking they exist | Marketing tolerates omitted nulls |
| D-40 | Desk's Vite dev server stays HTTPS-only through mkcert, with no `VITE_HTTPS=false` mode or `*-http` launch entry | Ocharo's local transport override (`ocharo-ws/docs/local-development.md`) removes plaintext Vite modes for every Ocharo app | Ocharo drops its HTTPS-only override |
| D-13 | Both environments run as .NET `Production` (no `dotnetEnvironment`); Desk's loopback defaults stay in `appsettings.json` | the flag also switches the descriptor's `identity` service, whose WoW2 image turns on ephemeral signing keys in its Development file | the identity lane confirms Ocharo's dev Identity overrides both key flags |
| D-14 | `jobs` and `image-worker` have no `configCheck:` item | neither answers `config check`: the be sdk `ServiceHost` covers web hosts only, and the image worker is Python | the be sdk offers a worker host with `config check` |
| D-32 | Desk stamps migration history with the version minus its `+commit` build metadata | the be sdk history `version` column is `varchar(20)` | the be sdk widens the column |
| CI | `publish-docker-image.yml` pins pipelines at commit `e134a47`, not a tag | cross-repository `from` links (pipelines `e0d2bf3`) are in no tag yet; Studio and Marketing (M-6) pin the same commit | the developer tags pipelines `v0.16.0` and every Ocharo repo re-pins to it |

Fixed in the be sdk (`10.0.108-beta`, 2026-11-09): the wire preset keeps dictionary keys as written, and `UseApiDefaults`
maps OpenAPI anonymously. Brand and Catalogue re-pinned and dropped their own OpenAPI mapping (B-2, C-2) and
Catalogue's dictionary-key override (C-3); Desk re-pinned and its commerce and public brand routes left
`CommerceJson`, which stays for stored snapshots only (D-21).
