# Supplier information and personal sourcing views

*Last updated: 2026-10-09*

## Decision and scope

The owner chose both material-first and supplier-first entry. A visual material reference can lead to a
supplier; a supplier record can lead to the fabrics, catalogues, quotes and replies they shared.
The Sourcing section remains inside Ocharo Desk. No new app, second supplier store or public contact directory
is required. This iteration adds personal presentation choices around the existing records.

## What belongs on a supplier profile

| Section | What the user needs | Current evidence and scope |
|---|---|---|
| Identity | Name, country/city, relationship stage, website | Stored supplier facts; identity and stage stay visible |
| Contacts | Primary contact route, channel, destination, linked person, verification, source and notes | Existing routes; safe links can open a contact app, but Desk does not send messages |
| People | Name, role, notes and the routes associated with the person | Existing people and route references; a person may have multiple channels |
| Shared materials and files | Fabric photos, reference images, catalogue PDFs, videos, captions, provenance and received date | Existing private media; do not label all photos as confirmed fabrics or every PDF as a quote |
| Overview and notes | Website, recorded counts and operator context | Existing facts; avoid copying contacts into notes when structured routes exist |
| Commercial terms | Material specification, unit, original price/currency, minimum order, quantity breaks, mix-and-match, lead time, sample terms and quote validity | Future structured offers; do not infer from supplier stage or media filenames |
| Follow-up | Last contact evidence, next action/date, sample requested/received/reviewed | Future activity/sample model; existing free-text notes are not a complete activity history |
| Qualification evidence | Who confirmed the terms, when, source attachment and any unresolved condition | Existing route verification and evidence are partial; qualified stage is an explicit decision, not proof of every condition |

### Useful default arrangement

Always show the supplier identity and relationship stage. Let the user place either shared media or contacts
first. Keep the four optional sections independently visible: Overview, Contacts, People, Shared materials
and files. A compact supplier list remains available alongside the detail on desktop; mobile opens detail
without squeezing the gallery. The Materials starting view shows media across suppliers and links each
assigned item back to its supplier. Unassigned items lead to the inbox.

Keep refresh and settings controls stable. First-load placeholders follow the detail or gallery shape;
request errors go to notifications. A failed read must not become a successful empty state. Preserve
confirmed data on refresh errors and existing unsaved-edit guards when changing view.

## Personal settings

Entry: Sourcing → View settings. These settings change presentation for the current signed-in account and
organization in this browser. They do not change organization facts or what another member sees.

| Setting | Initial delivery | Rule |
|---|---|---|
| Default starting view | Suppliers or Materials | Both remain reachable; an explicit supplier/inbox link wins over the default |
| Supplier section visibility | Overview, Contacts, People, Shared materials and files | Identity/stage remain visible; hiding a section never deletes its data |
| Supplier section order | Contacts first or Materials first | Predictable presets before arbitrary drag-and-drop layout editing |
| Save / reset | Explicit Save and restore-default controls | Validate the versioned value; confirm persistence before reporting success |

Preferences contain only identifiers and display flags. Use the published frontend SDK storage broker,
namespace by user and organization, validate decoded values and discard malformed/future versions.
Do not persist contacts, attachments, credentials, authentication tokens or supplier snapshots in this
preference record. A storage failure must preserve the previous applied settings and show a notification.
This initial implementation is browser-local, not a claim of account synchronization across devices.
A server-backed user-preference resource can follow when cross-device sync becomes necessary.

Do not put shared field definitions in personal display settings. If extensible supplier fields are added,
use an organization-owned schema with typed values, permissions and revision history. Price, quantity/unit,
quote validity and evidence should remain structured offer fields, because comparison depends on them.

## Follow-on domain work

1. Material/reference records: composition, dimensions, use, colour and reference images.
2. Supplier offers: dated evidence, currency/unit, quantity rules, availability and quote validity.
3. Samples and follow-up: requested, received, reviewed and linked to the exact offer/reference.
4. Compare equivalent offers without silently converting units or treating unknown freight/taxes as zero.

These are documented scope gaps, not populated product facts or an automatic supplier approval workflow.

## Runtime and verification

- The existing Desk backend at `https://localhost:8264` passed its readiness check on 2026-10-09.
- Port `8266` is occupied by an unrelated Home Atlas development process; preserve its runtime.
- Desk serves the updated frontend from its own `wwwroot` at port `8264`; served HTML and JavaScript were verified against the new build.
- The current in-app browser requires operator sign-in before populated supplier screens can be inspected.
- Implementation verification and owner acceptance are recorded separately in the version track.

Executed verification: 343/343 frontend tests, 26/26 focused Sourcing tests, typecheck/SFC, ESLint and
production build passed. Tests cover separate all/inbox/supplier requests, scoped/validated preference
storage, blocked writes, authenticated preference changes, dirty forms and Materials routing/history.
All supplier facts and authorization remain server-owned; this slice changes presentation and navigation.
