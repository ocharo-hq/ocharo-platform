# Ocharo media storage — Sourcing and shared assets

*Last updated: 2026-10-09*

## Status and recommendation

Analysis proposal based on current source and a read-only local storage measurement. No application code, provider configuration, original files or database records were changed by this analysis.

Use one shared private media capability inside Ocharo Platform, with documents and attachments owned in their respective apps. Keep metadata, access rules and relationships in PostgreSQL; store file bytes through the existing WoW2 blob abstraction. Use local filesystem storage for development and private object storage for hosted deployment. Select the hosted provider with the deployment plan; this report does not approve a provider or expenditure.

Sourcing needs its own supplier/material attachments and source history. It does not need a separate storage server or another file-manager app. This extends the accepted September 29 shared-storage direction, rather than changing product ownership.

The immediate model correction is **deduplicate bytes without deduplicating supplier evidence**. Migration to object storage alone would leave the current supplier-association problem intact.

## Verified current implementation

| Area | Current persistence | Consequence |
|---|---|---|
| Desk Sourcing | PostgreSQL originals and thumbnails in `sourcing.media_contents`; separate metadata table | Files already survive application restarts with the database volume |
| Desk preparation and commerce | Media/artifact bytes also occur in PostgreSQL tables | A future shared-media migration must inventory these domains too |
| Catalogue and Brand companions | SQLite metadata; SDK filesystem blobs for originals and previews | Databases and blob directories must be backed up together |
| Catalogue releases | Release-asset bytes stored separately in SQLite | Changing the blob provider alone will not migrate all Catalogue files |
| Marketing and Editorial companions | App-local metadata and blob storage; independent publication/access rules | Shared storage must preserve releases and each app's access checks |
| Studio | SQLite creative state with bundled model/texture assets | Keep independently owned creative documents; register reusable exports deliberately |

### Measured Sourcing footprint

Read-only aggregate SQL against the existing local Desk database on October 9:

- 424 stored files.
- Originals: 259,050,615 bytes.
- Thumbnails: 13,868,010 bytes.
- Combined payload: 272,918,625 bytes, approximately 260.3 MiB.
- Sourcing content table including PostgreSQL storage: 283,893,760 bytes.
- Whole database: 295,589,555 bytes.

This is a modest local dataset, not evidence of a current storage-capacity failure. PostgreSQL supports large values through TOAST; storing files there is a valid initial tradeoff. The reason to introduce object storage is shared lifecycle, delivery, backup separation and future media growth. The existing content table is separate from list queries, so ordinary metadata listing need not read every original. [PostgreSQL TOAST documentation](https://www.postgresql.org/docs/current/storage-toast.html).

### Existing Sourcing behavior to preserve

- Every route checks organization membership; mutation requires an editor or owner.
- Originals and thumbnails are private. Reads use organization-scoped queries.
- Original files are preserved, including embedded metadata. The model records GPS presence.
- Images have a 20 MiB limit; PDFs 25 MiB; MP4 files 64 MiB. Additional image decoding limits exist.
- Original and thumbnail writes currently commit atomically with metadata in PostgreSQL.
- Exact duplicates reuse an existing record; near duplicates produce suggestions.
- Revision checks protect updates and deletion.
- Imports retain supplied message/source/timestamp fields and verify supplied hashes.

Current reads materialize entire originals into memory, even where the HTTP endpoint supports ranges. Moving bytes to a provider without changing this path would preserve that memory cost.

## Correct the file and relationship model

These are proposed concepts, not current tables or SDK APIs.

| Concept | Owns | Identity and lifecycle |
|---|---|---|
| Blob | Durable bytes, checksum, verified size/type, storage locator | Immutable; deduplicate only within an organization |
| Asset | Logical file identity, original blob, creator, technical state, lineage | Stable opaque ID; revisions never overwrite prior originals |
| Rendition | Thumbnail, display image, PDF/video preview or export | References source revision and transformation version |
| Attachment | Use of an asset by a supplier, material offer, product, article or design | Owns contextual caption, order, role and optional alt text |
| Source receipt | Who supplied a file, where, when, message identity and permission evidence | Multiple receipts may reference the same bytes |
| Publication reference | Explicit approved rendition/revision used by a release | Independent from upload, supplier assignment and draft approval |

### Why these must be separate

Today `sourcing.media` has one `supplier_id` and a unique constraint on `(organization_id, sha256)`. Uploading identical bytes for another supplier returns the existing row before preserving the new supplier or source metadata.

Example target behavior:

1. Supplier A sends a fabric photograph.
2. Supplier B later sends the identical photograph with a different quote.
3. Store one original blob, two source receipts and two supplier/material attachments.
4. Keep each quote attached to its own supplier and receipt.
5. Removing A's attachment leaves B's attachment and the original intact.

A matching photograph is not proof of the same fabric, stock, origin, price or permission to publish. Near-duplicate matching remains a review aid.

Keep original filenames and received-message context on receipts. Keep captions and alt text on the relevant usage when they vary by context or language. Do not make another app's use silently rewrite the supplier's original evidence.

## Ownership and access

Implement the common media registry and application contract within the existing Platform API and worker. Other apps use that contract with verified organization/actor identity; they do not open another app's database or mount its private data directory.

- Asset lookup, upload, read, attach and delete all enforce organization membership and applicable domain permission.
- Sharing an asset within an organization is an explicit permitted use. A blob ID, hash or object key grants no access.
- Application-specific records stay in their current owner until a separately verified data migration.
- A central usage ledger records references from independently persisted apps. Registration is idempotent and deletion fails closed while references cannot be reconciled.
- Same-organization deduplication is an internal storage optimization, not a cross-user discovery endpoint.
- Cross-organization reuse, if ever supported, creates an explicit copy/share grant with its own ownership record.
- Single sign-on authenticates the person; it does not replace media authorization.

### Storage zones

| Zone | Contents | Delivery |
|---|---|---|
| Staging | Bounded uploads awaiting inspection/finalization | Private; unique temporary keys with expiry |
| Private originals | Supplier evidence, source photos, documents, creative files | Authorized API or short-lived signed read |
| Private renditions | Thumbnails and work previews | Same authorization boundary as their source |
| Published renditions | Explicitly approved, pinned release outputs | Separate delivery policy and preferably separate bucket/origin |

Keep originals and private previews outside static web roots. Published files should come from explicit release references, not from making a supplier-media folder public.

Separate development, test and production storage. Suggested object-key structure uses organization and opaque blob/upload IDs. Supplier names, phone numbers, filenames and current app assignment do not belong in object keys. An attachment can move without moving bytes.

During migration, give every legacy app its own provider prefix. Catalogue, Brand and Editorial currently use similar original/preview keys; their preview recipes differ. Pointing those adapters at one unprefixed bucket could overwrite a preview with another app's output. Shared rendition keys must include their transformation recipe/version.

Keep stable application URLs based on asset/rendition IDs. Generate delivery URLs at read time; do not persist expiring signed URLs as attachment locations. Presigned URLs are bearer access and remain reusable until expiry, so use short validity. Where immediate revocation matters, serve through the authorized API. [AWS presigned URL behavior](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html).

Original supplier photos can contain GPS and other private metadata. Keep originals intact for evidence; serve orientation-corrected display derivatives without sensitive metadata. Test metadata removal explicitly and preserve intended color rendering when generating fabric previews. Supplier possession or delivery does not establish public marketing rights.

## Upload and processing lifecycle

1. Authorize the intended organization and context; reserve quota and an idempotent upload session.
2. Stream to a unique private staging key while enforcing actual byte limits.
3. Inspect the real file type, checksum, decoding limits and supported content. Apply appropriate document/video inspection before allowing inline delivery.
4. Finalize a verified immutable blob. Never issue a write URL for an existing original.
5. Commit the asset, attachment/receipt and processing outbox in PostgreSQL.
6. Return the persisted identity and state. The frontend changes its confirmed result only after success.
7. A worker creates versioned thumbnails/previews; the UI can show a confirmed “processing” state meanwhile.
8. Reconcile abandoned staging objects and unreferenced finalized objects after a grace period.

Object storage and PostgreSQL do not share a transaction. Handle crashes between steps explicitly: unique session IDs, immutable keys, idempotent finalization, durable outbox jobs, retry and orphan reconciliation. A successful PUT alone must not appear as a completed supplier attachment.

Start with API-proxied streaming uploads. Direct signed uploads can follow when measured file sizes or bandwidth justify them; they still need server-side finalization. Add resumable transfers for large videos/creative exports when required by actual workflows.

Current PDF/MP4 acceptance checks are basic signatures. File validation should consider type, content, size, authorized uploader and isolated delivery; never trust the extension or submitted MIME alone. [OWASP file-upload guidance](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html).

## Sourcing experience and integrations

Keep the workflow inside Suppliers and Materials:

- Add photos/documents to a supplier or material offer; provide an unassigned intake inbox.
- Show thumbnails, receipt/source, received date, supplier, material/offer link, caption and verification state.
- Offer “use existing file” while retaining the new receipt and attachment.
- Support context-specific original download and “used in” references.
- Separate “remove from this supplier” from “delete file everywhere”; show blocking references for the latter.
- Expose user-specific gallery density and metadata visibility in personal settings.
- Expose organization usage, limits and retention to administrators. Keep storage credentials and provider topology in operator configuration.

For Telegram or other supported imports, store provider connection/message/attachment identity as provenance and download authorized bytes into Ocharo storage. External file IDs and message links are not the durable archive. Do not persist token-bearing provider download URLs. Importing a file does not approve redistribution.

MCP should return media metadata, provenance and application references by default. Explicit authorized attachment access can issue bounded delivery access. Keep provider credentials, filesystem paths, bulk original bytes and incidental GPS out of default tool results. Future write tools should consume verified upload IDs and source receipts.

## SDK boundary

Already available in WoW2 backend SDK source `10.0.64-beta`:

- `IBlobRepository`: stream save/read, delete, exists, information and async listing.
- Filesystem, S3-compatible and Azure implementations.
- Provider signed URLs and HMAC transfer endpoints.

Keep generic byte-transfer and integrity mechanisms in the SDK. Keep supplier receipts, product relationships, brand permissions, publication decisions and quotas' business rules in Ocharo.

Before broader adoption, address:

| Generic gap | Required behavior |
|---|---|
| Filesystem writes currently truncate the target directly | Write temporary sibling files and atomically promote completed writes |
| HMAC oversize cleanup deletes the named target | Cleanup only the current upload's staging object; never a prior original |
| No provider-neutral conditional write/checksum/version contract | Create-only finalization, integrity evidence and provider capability reporting |
| No range/resumable abstraction | Add only where delivery needs it; native signed delivery can supply provider ranges |
| Local MIME is not persisted by the blob adapter | Keep authoritative media metadata in the registry |

S3's API supports conditional creation and whole-object write semantics; the current SDK abstraction does not expose all those controls. [AWS PutObject](https://docs.aws.amazon.com/AmazonS3/latest/API/API_PutObject.html). Do not mistake an “S3-compatible” label or emulator test for verification against the chosen live provider.

## Deletion, retention and recovery

Current Sourcing deletion permanently removes metadata and bytes in one transaction. That behavior cannot be carried unchanged into shared storage.

Use reference-aware deletion with a recoverable trash period. Separate detaching, trashing and permanent purging. Pin assets used by historical releases where retention requires them. Purge requires a checked zero-reference state; new attachment creation must be rejected while the asset is being purged. Reconcile the ledger before cleanup rather than trusting an unverified counter.

Retention duration and quotas are organization policies to settle before production. Do not invent unlimited retention or silently delete originals. Released public copies may have been downloaded; withdrawing an origin URL cannot recall those copies.

Back up PostgreSQL, existing companion databases, original blobs, non-reproducible artifacts and necessary encryption/identity keys. Use independent recoverable copies with documented restore evidence. Provider durability/versioning is useful but is not a tested application restore.

Catalogue's current JSON export contains settings, current products and collections. Its workspace ZIP contains product JSON. Neither is a complete backup of images, release assets or history.

A restore drill must recover an attachment with its correct supplier/source, original checksum, thumbnail, membership boundary and historical release reference. Routine backups/restore acceptance remain unverified in the deployment documentation; this analysis performed no restore.

## Proposed delivery sequence

1. Correct source receipts and multi-context attachments while retaining existing storage.
2. Introduce the Platform media contract, immutable upload lifecycle and SDK integrity fixes locally.
3. Route new Sourcing uploads through the provider abstraction.
4. Copy existing originals and thumbnails, verify every checksum, retain existing IDs and add explicit storage locators.
5. Switch reads with a per-record legacy fallback and a reversible cutover. Keep old bytes until verified backup/restore and agreed retention permit cleanup.
6. Adopt Catalogue/Brand/Marketing/Editorial through contracts; inventory database-backed release artifacts separately.
7. Validate hosted object storage, quotas, worker retries, delivery and restores before production.

Implementation, data cutover and SDK publication remain proposed. Coordinate SDK implementation with its active owning lane; retain explicit database and publication boundaries throughout adoption.

### Verification gates for implementation

- Two suppliers sharing identical bytes retain independent receipts and quotes.
- Cross-organization direct reads, signed delivery and attachment mutation are denied.
- Upload retry/concurrency yields one durable blob without losing source occurrences.
- Interrupted/oversize uploads cannot truncate or remove a previous object.
- Invalid documents/videos are rejected or held before inline serving.
- Private derivatives remove sensitive metadata.
- Failed upload/finalization leaves no confirmed UI attachment.
- Removing one usage cannot break another app or a published release.
- Provider outage, worker retry, migration rollback and full restore are exercised.

## Local source evidence

Paths are relative to the Platform repository unless identified otherwise.

- [Sourcing schema](../../codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Migrations/007-sourcing/Apply.sql):65–101 — byte storage, tenant/hash keys and single-supplier metadata.
- [Sourcing service](../../codebase/brand-workspace.backend-services/BrandWorkspace.Infrastructure/Sourcing/Services/SourcingService.cs):98–125 — duplicate upload returns the old record.
- [Sourcing repository](../../codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Sourcing/Repositories/SourcingRepository.cs):297–408 — transaction, hard deletion and whole-file reads.
- [Sourcing endpoints](../../codebase/brand-workspace.backend-services/BrandWorkspace.Api/Sourcing/SourcingEndpoints.cs):29,124 — access and delivery.
- [Catalogue media](../../codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Infrastructure/Catalogue/MediaStore.cs):13–58 — originals/previews through the SDK.
- [Catalogue release assets](../../codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Domain/Catalogue/Entities/DesignRecords.cs):47–51 — release bytes retained in database records.
- [Catalogue exports](../../codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Application/Catalogue/Services/CatalogueExportService.cs):15–29,53–65 — limited export contents.
- [Deployment](../../deployment/deployment.md):150 — unfinished operational prerequisites.
- [Sourcing/Events analysis](2026-10-09-sourcing-events-integrations.md) — intake, supplier experience and MCP boundaries.
- WoW2 backend SDK: `src/Storage/Core/IBlobRepository.cs`, `Core/IBlobUrlIssuer.cs`, `FileSystem/LocalFileBlobRepository.cs:25–33`, `Transfer/BlobTransferServiceCollectionExtensions.cs:72–83`, `S3/S3BlobRepository.cs`.
- Editorial source review found conflicting cache assignments, but its boundary middleware registers an `OnStarting` callback forcing `no-store`. This is policy cleanup, not evidence of a private-media cache leak.

Existing tests were inspected; no application tests were rerun for this documentation-only analysis. Provider selection, hosted durability, live cross-app media integration and restore success remain unverified.
