# Catalogue deployment

*Last updated: 2026-11-09*

`Dockerfile` builds the single host (SPA in `wwwroot`) from the `engineering/codebase/` context. Wheelhouse deploys it as the `catalogue` service and site of `../deploy.yml`, signed in through Ocharo's Identity (client `catalogue`); the image needs `Identity:Client:*`, `Catalogue:OrganizationId` and `ConnectionStrings:Database` to boot, so it has no stand-alone local compose file. Details: `../deployment.md` § Brand and Catalogue pilots.

Catalogue keeps its state in schema `catalogue` of the product database (`ocharo_{env}`, shared with Desk, Identity and Studio): products and their revisions, collections, media rows with their Files ids, settings, named catalogues, releases and their artifacts, publications, and the Data Protection key ring (`catalogue.data_protection_keys`). BFF sessions live in schema `catalogue_cache`, so a restart or deploy keeps everyone signed in. The deploy's `migrate` item applies the `catalogue` set and the cache's `distributed_cache` set before Catalogue restarts; a deployed boot only verifies their levels. The `catalogue-data` volume stays mounted at `/data`: it holds the blobs stored before WoW2 Files and, until the move below, the retired SQLite store.

Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md

## PostgreSQL move

The retired store is `/data/ocharo-catalogue.db` plus the file key ring in `/data/keys`, on the `catalogue-data` volume. The image's `import-sqlite [--dry-run]` verb copies it once per environment, after the deploy that carries the move. Until it runs, every `/api/catalogue` and `/api/public` route answers 503 (`storage_moving`), the published reader included; sign-in, `/health`, status and runtime config stay up, and the boot neither seeds nor fills Files ids.

Dev first, then prod, the same steps on each:

1. Deploy the release that carries the move. Its `migrate` item creates schemas `catalogue` and `catalogue_cache`; Catalogue boots and answers 503 on its routes.
2. Run `dotnet OcharoCatalogue.Api.dll import-sqlite --dry-run` as an operator job on the target's image, with the target's settings and the `catalogue-data` volume at `/data`. It writes nothing; its `import-result` line counts the rows it would copy.
3. Run `dotnet OcharoCatalogue.Api.dll import-sqlite` the same way. It reads `LegacySqlite:Path` (default `{Catalogue:DataRoot}/ocharo-catalogue.db`, `/data/ocharo-catalogue.db` in the image) and `LegacySqlite:KeysPath` (default `DataProtection:KeysPath`, else `{Catalogue:DataRoot}/keys`), copies every row and key PostgreSQL lacks in one transaction with its id, revision, hashes, Files ids and instants, verifies every table by row count and a SHA-256 over each row's canonical content (bytes by their SHA-256), then renames the file to `ocharo-catalogue.db.imported-{UTC stamp}`. The key folder and the blobs stay where they are.
4. Read the last stdout line, `import-result {json}`: per table `rows`, `copied`, `diverged` and the two digests, `sqliteSha256` equal to `postgresSha256`; then `keys`, `fileIds` and `publications`. Counts and digests only, never rows, ids or keys.
5. Check the move: `/c/wave1-check` and its PDF open; Catalogue's product list matches; restart Catalogue and the session stays signed in.

Exit `0` means copied, planned or nothing to copy (a rerun after the retire finds nothing and copies nothing); `1` means refused — a row PostgreSQL holds with other content, a row breaking a key, an unknown argument or no schema yet — with nothing written and the SQLite file untouched; `2` means failed. After prod's copy, a later release drops the volume once its blobs live in Files (`v0.2` Iteration 13).
