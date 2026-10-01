# Platform build verification

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-29*

## Implemented scope

Ocharo Desk owns the canonical organization-scoped Commerce draft. Its existing brand IDs remain
stable. Every save creates an immutable revision with optimistic concurrency; owner publication creates
an immutable release and a public slug. Withdrawal removes the active public pointer and makes a pinned
withdrawn release unavailable. Preserved Brand/Catalogue sources remain read-only adoption inputs.

The operator UI provides product status and localized copy, sizes, retail pricing, collections, brand
identity, official destinations, logo assignment, image provenance and publication history. Draft edits
participate in the existing unsaved-navigation guard. Brand switching and withdrawal are disabled while
unsaved changes exist. Failed saves preserve the local buffer. Product and source identifiers remain
visible while approval controls are limited to owners.

Private catalogue media is served through organization membership authorization. Public media requires
an approved hash in an active or explicitly pinned non-withdrawn release. Both media routes disable
caching. Commerce storage and response serialization preserve dictionary key casing, including `M` and
`XL` stock labels; property names retain the existing camel-case contract.

## Canonical catalogue editions

The [catalogue builder](catalogue-builder-verification.md) adds canonical product/image selection,
product/continuation/collage layouts, editable price tiers and source revision metadata to the existing
revisioned designer. Complete editions save, reopen and export to PDF. Frontend verification now
passes 110 tests, plus the actual authenticated four-page browser/export/conflict workflow.

## Studio operator intake

Connected apps accepts the two files exported by Studio: its JSON manifest and the matching PNG/JPEG.
The operator chooses a Platform brand and an optional product. Intake validates the manifest, bounds
file sizes and checks the image SHA-256 before uploading anything. It stores the image in the existing
organization Media library and records the Studio source identity, asset releases, material references,
image identity and source submission note. A source revision of zero is legitimate browser-local
evidence and remains zero. Negative revisions are rejected.

The resulting creative record is always `unreviewed`. The operator can preview it in Connected apps,
attach its image from Media to a product and approve provenance separately in Products. Studio's
`approved-for-review` source submission does not approve business accuracy, media rights or publication.
Failed manifest persistence reports that the successfully uploaded image remains in Media.

## Contracts

- `GET/PUT /api/organizations/{organizationId}/brands/{brandId}/commerce`: private revisioned draft.
- `POST .../commerce/adopt-preserved`: owner-only adoption into an empty canonical draft, restricted to the explicitly bound organization.
- `POST .../commerce/media`: copies an existing organization upload with explicit provenance.
- `GET .../commerce/media/{sha256}`: authenticated private preview.
- `GET/POST .../commerce/releases`: private history and owner publication.
- `POST .../commerce/releases/{releaseId}/withdraw`: owner withdrawal with a reason.
- `GET /api/public/v1/brands/{slug}`: approved identity projection with release identity.
- `GET /api/public/v1/brands/{slug}/catalogue?locale=en`: approved buyer product projection.
- Public reads accept `release={guid}` for a pinned immutable release.
- `POST /api/organizations/{organizationId}/integration-tickets`: CSRF-protected five-minute ticket.
- `POST /api/integrations/introspect`: HTTPS-only validation against live session and membership.
- `POST /api/organizations/{organizationId}/creative-imports`: records verified uploaded derivative provenance.
- `GET /api/organizations/{organizationId}/brands/{brandId}/creative-imports`: private creative intake history.

Tickets accept only `ocharo-studio` and `ocharo-marketing` audiences. They require
`Integrations__SigningKey` with at least 32 UTF-8 bytes. Responses use `Cache-Control: no-store`.
Expired tickets, wrong audiences, missing claims, removed memberships and revoked sessions fail closed.
A role change immediately changes introspected permissions. No browser workspace ID grants authority.

### Preserved source binding

`Commerce__AdoptionOrganizationId` must be an explicit nonempty organization UUID matching the
authenticated destination organization. Missing, malformed, empty or mismatched bindings fail with
403 before accessing donor files. The organization is never inferred from an operator, slug or
first request. `Commerce__SourceRoot` selects the offline preserved codebase; Development retains
its existing default root, but still requires the explicit organization binding. Configure this
only for the intended source owner, then remove the binding after adoption.

## Connected app destinations

The built-in UI accepts only these exact local origins and destinations. It accepts no
user-configurable recipient URL. The destination is resolved again by application ID when constructing
a ticket link. Ticket material occurs only in the fragment; Platform does not persist it.

| Application | Normal local destination | Isolated review destination |
|---|---|---|
| Desk Vite origin | `https://localhost:8266` | `https://localhost:8342` |
| Platform issuer | `https://localhost:8264` | `https://localhost:8340` |
| Studio | `https://localhost:8262/studio` | `https://localhost:8356/studio` |
| Marketing | `https://localhost:8358/` | `https://localhost:8344/` |
| Editorial | `https://localhost:8284/studio/` | `https://localhost:8352/studio/` |

Desk origins 8264/8266 resolve to issuer 8264; 8340/8342 resolve to issuer 8340.
Studio receives `ocharo-ticket`, `audience=ocharo-studio` and `issuer` fragment fields. Marketing and
Editorial receive `platformToken`. Their receiver implementations remove fragments and validate the
ticket against their configured Platform; those repositories own receiver verification. Connection
requires an intentional operator button click and current organization membership.

## Executed verification

| Check | Result |
|---|---|
| Vue strict typecheck and SFC compilation | Passed |
| Frontend production build with Node 24.11.0 | Passed; existing large-chunk notice remains |
| Frontend suite | 99 passed, zero failures |
| Frontend ESLint and architecture boundaries | Passed |
| Authenticated adoption binding regressions against synthetic donor databases | 5 passed, zero failures or skips |
| Focused backend ticket and Commerce serialization regressions | 8 passed, zero failures or skips |
| Backend compilation | Passed, zero warnings |
| Backend unit suite, executed by the coordinating lane | 37 passed, zero failures or skips |
| Complete backend E2E suite against isolated PostgreSQL containers | 51 passed, zero failures or skips |

Frontend regressions cover initial loaded-brand selection, dirty conflict preservation, brand-change
blocking, saved-revision publication and strict local app destinations. Backend regressions cover ticket
expiration, audience isolation, revoked sessions, membership removal, role downgrade, malformed tokens,
missing claims, acceptance of literal Studio revision zero, rejection of negative source revisions and
uppercase size key round-tripping through stored and returned Commerce data. Frontend intake tests
check manifest bounds and matching or mismatched image fingerprints before upload. Adoption regressions
exercise missing, empty, malformed and zero UUID bindings, an unrelated owner, a bound editor, successful
import by the bound owner, preserved source revision evidence and repeat-adoption conflict. The complete 51-test
E2E suite passed after these five adoption cases and the binding guard were added.

## Browser verification

Trusted Chromium verification completed on 2026-09-29 against isolated `https://localhost:8340`, with
normal certificate validation and a synthetic brand. The browser was closed when the account slot was
released to the Marketing verification lane. Existing user runtimes and data were outside this run.

- Sign-in through the real operator form succeeded.
- Products loaded its initial brand and saved a new draft and product.
- Cancelling navigation retained unsaved edits.
- A concurrent server save produced a conflict without replacing the local edit buffer.
- Real Studio JSON and PNG files imported successfully, preserving SHA-256 and source revision zero.
- The imported image rendered as unreviewed; successful intake reset both file inputs.
- Desktop `1440×1050` and mobile `390×844` layouts had no horizontal overflow.
- Clicking Connect to Ocharo Studio reached the actual receiver and returned HTTP 200 session validation.
- The Studio session identified the current Platform organization and permitted editing.
- Studio removed the ticket fragment and retained its ticket in session storage only.
- No browser page errors occurred.

Local evidence is retained under the enclosing workspace's ignored review directory:
[result](../../../../../.local/review-ui/desk-browser-result.json),
[desktop import](../../../../../.local/review-ui/desk-creative-import-desktop.png),
[mobile import](../../../../../.local/review-ui/desk-creative-import-mobile.png),
[desktop conflict](../../../../../.local/review-ui/desk-products-conflict-desktop.png),
[mobile conflict](../../../../../.local/review-ui/desk-products-conflict-mobile.png), and
[connected Studio](../../../../../.local/review-ui/desk-to-studio-connected.png).
These artifacts contain synthetic review content and are not publication evidence.

## Boundaries

Migration `005-commerce` adds separate canonical Commerce tables. It does not replace existing
preparation or companion records. Original data, source databases, media hashes, staged work and Git
history are preserved. The existing operator runtime on `8264/8265` and PostgreSQL `8270` remain outside
this verification lane. Cross-app live verification runs against the separately managed review runtime
and database, recorded in the enclosing Ocharo workspace build record.

No production deployment, provider API call, paid generation, real campaign delivery, commit or push is
established by this record. Local code and tests do not establish business approval of imported content.

Final parent verification: after the donor-binding change, the complete current E2E suite passed **51/51**
with no skipped tests. Together with the separately executed **37/37** unit tests, Platform backend
verification totals **88 passing tests**. The refreshed review API also returned 403 for an unbound
owner adoption request before source access.
