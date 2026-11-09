# Brand deployment

*Last updated: 2026-11-09*

`Dockerfile` builds the single host (SPA in `wwwroot`) from the `engineering/codebase/` context. Brand stores its rows in
schema `brand` of the product database (`ocharo_{env}`, shared with Desk and Identity), its sign-in sessions in schema
`brand_cache` and its Data Protection key ring in `brand.data_protection_keys`, so a restart or deploy keeps everyone
signed in. The `/data` volume (`brand-data`) keeps the blobs stored before WoW2 Files and, until the copy below, the
retired SQLite store. Wheelhouse deploys it as the `brand` service and site of `../deploy.yml`, signed in through
Ocharo's Identity (client `brand`); the image needs `ConnectionStrings:Database`, `Identity:Client:*` and
`Brand:OrganizationId` to boot, so it has no stand-alone local compose file. Details: `../deployment.md` § Brand and
Catalogue pilots.

## PostgreSQL move

The retired store is `/data/ocharo-brand.db` plus the key ring in `/data/keys`, on the `brand-data` volume; the blobs in
`/data/blobs` stay where they are. The image's `migrate` step creates the `brand` set (`001-baseline`, every table the
SQLite store held) and the `distributed_cache` set in `brand_cache`. The image's `import-sqlite [--dry-run]` verb copies
the store once per environment, after the deploy that carries the move: dev first, then prod with the same verb.

1. Run `import-sqlite --dry-run` as an operator job on the target's image with the `brand-data` volume mounted. It
   writes nothing and reports what it would copy.
2. Run `import-sqlite` the same way. It reads `LegacySqlite:Path` (image default `/data/ocharo-brand.db`),
   `LegacySqlite:KeysPath` (`/data/keys`) and the blob folder under `Brand:DataRoot` (`/data/blobs`), then:
   - copies every row PostgreSQL lacks, keeping each id, revision, SHA-256, Files `file_id`, document text and instant
     (stored in UTC), and every key file
   - verifies each table by its row count and a SHA-256 over each row's canonical content, read back from PostgreSQL
   - checks each local blob's bytes against its media row's SHA-256
   - renames the file to `ocharo-brand.db.imported-{UTC stamp}`, with its `-wal` and `-shm` files
3. Read the last stdout line, `import-result {json}`: per-table rows, copies and digests (`sha256` from SQLite,
   `postgresSha256` read back, equal once imported), plus releases, file ids, blob and key counts. It carries no
   document, id or key.

Exit `0` means copied, planned or nothing to copy; `1` means refused: a row that differs or only PostgreSQL holds, a key
missing after the copy, a local blob whose bytes disagree with its SHA-256, or no `brand` schema yet (run `migrate`).
A refusal keeps the SQLite file untouched; `2` means failed. A rerun after the file is retired copies nothing and exits
`0`. Until the import runs, every `/api/brand` and `/api/public/brand` route answers 503 (`brandCode`
`storage_moving`), and the boot neither seeds an empty brand nor fills file ids, so the copy finds PostgreSQL as the
migration left it.

Check after the copy, on the target:

- `GET /api/public/brand` serves release 1 (`"release": 1`), each logo with its `fileId`
- `GET /api/public/brand/releases/1` answers 200
- the editor's draft shows the same revision as before
- a restart keeps a signed-in session

Rehearsed locally on 2026-11-09 against a copy of the ignored `App_Data/ocharo-brand.db` and a throwaway
`postgres:18-alpine`: `migrate`, the dry run, the copy (12 of 12 rows, 3 of 3 blobs verified, 1 of 1 keys, every table's
digests equal) and a rerun (`current`) exited `0`; the host then served release 1 from PostgreSQL.

Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
