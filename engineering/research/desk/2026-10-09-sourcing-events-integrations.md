# Desk material sourcing and events: UI and integration direction

*Last updated: 2026-10-09*

## Status

- [x] Inspected the current Desk source, contracts, importers and authentication boundary.
- [x] Researched official discovery sources and provider limitations.
- [x] Implemented matching loading states, calendar downloads and read-only MCP access.
- [x] Added a reviewed public source directory and explicit assistant connection controls.
- [ ] Owner reviews the proposed material workbench and event decision view.
- [ ] Verify populated live screens after operator sign-in; this browser remained at the login screen.
- [ ] Activate MCP in the intended server configuration and connect the chosen trusted client.

## Product identity

These are existing Ocharo Desk modules: `/organizations/{organizationId}/sourcing` and
`/organizations/{organizationId}/events`. Their engineering roots remain
`brand-workspace.frontend-services` and `brand-workspace.backend-services`.
The standalone Catalogue app is a different surface and was not changed in this iteration.

Current Sourcing organizes suppliers, people, contact routes and immutable private media.
Current Events records opportunities, dates with precision, application deadlines, participation requirements,
cost descriptions and a decision pipeline. Existing imports preserve Desk edits and reject invalid bundles.

## Recommended UI

### Sourcing: support materials and suppliers as equal starting points

The main working question is “which material can satisfy this design at this quantity?”
A supplier directory alone cannot answer it. Keep suppliers as reusable records while adding materials and offers.

| Surface | Main content | Primary action |
|---|---|---|
| Materials | Searchable reference/swatches, composition, use, review stage | Capture reference |
| Material detail | Specification, supplier offers, samples, evidence, linked product designs | Attach quote |
| Compare | Equivalent offers by quantity unit, minimum order, price break, delivery and validity | Choose sample/order candidate |
| Suppliers | Contacts, routes, qualification evidence, catalogue links, material coverage | Record supplier |
| Inbox | Unassigned photos, PDFs, selected replies | Assign evidence |

Use a gallery for visual references and a table for offers. Desktop uses a main results area with a detail pane;
mobile opens detail as a separate full-width view with preserved filters. Keep Refresh, capture and filters in
stable toolbars. Empty results are centered text within reserved results space. Request failures are toasts.

An offer needs: original amount and currency; unit (piece/metre/kg); quantity range and MOQ; composition,
width/weight/dimensions where relevant; stock/lead time; sample cost; quote date and expiry; delivery basis;
freight/taxes as known, estimated or unknown; source evidence and supplier confirmation.
Do not compare a metre price with a piece price without an explicit conversion basis.
Do not label a landed total complete while freight or required taxes remain unknown.
FX rates need provider, rate date and a saved snapshot; changing rates must not rewrite accepted quotations.

Decision on 2026-10-09: the owner wants both starting points. Materials answers “what can satisfy this design?”;
Suppliers answers “what has this supplier shared, and how do I reach them?”. Keep both visible and allow a
personal default starting view. The current media records support a shared materials/files gallery; they do
not yet constitute structured fabrics or comparable supplier offers. Offer comparison and purchase approval
remain a domain proposal. See [supplier information and personal views](2026-10-09-supplier-information-and-personal-views.md).

### Events: prioritize decisions and deadlines

The main question is “what should Ocharo pursue, and what must happen before its deadline?”
Use an upcoming agenda with a compact deadline strip, followed by opportunities grouped by date.
Calendar is a secondary spatial view; a pipeline board is useful only when enough active applications exist.

An event row should expose title, city, exact/estimated date, format, decision status and source freshness.
The detail pane should answer: fit, participation route, required assets, application deadline, fees, travel
considerations, source evidence and next action. Unknown deadlines remain unknown; event start is not a substitute.
Separate attending as a buyer from applying as an exhibitor or designer.

Add explicit edition/season, source observations and review history before automated updates.
Deduplicate by organizer + edition + place + dates, with a human merge decision for uncertain matches.
Do not infer a new edition from a stale announcement or reopen an expired application automatically.

### Shared intake and freshness

Capture → extract candidate facts → compare against existing facts → review → apply.
Each observation needs canonical URL or file hash, source title, observed time, extracted excerpt,
edition/validity, proposed fields and confidence/unknown values. Store operator corrections separately.
Background discovery produces candidates; it never overwrites a confirmed record silently.

Source states: not connected, manually imported, checked, changed, needs review, unavailable.
“Checked” means evidence was inspected at a particular time, not that a supplier was qualified or application accepted.
Suggested checks: weekly for opportunities, daily near a known deadline, monthly for supplier catalogues.
These are product recommendations, not existing scheduler commitments or provider guarantees.

## Sources and integration methods

Research inspected official pages on 2026-10-09. The event pages below did not advertise a documented public
API, RSS or ICS feed. Their directory entries are explicitly reviewed links, not live connections.

| Priority | Source | What to capture | Integration |
|---|---|---|---|
| P0 | Existing private Ocharo supplier register | Selected catalogues, quote attachments, swatches, replies | Existing reviewed bundle/file import; private evidence stays private |
| P0 | [P L Industries / KRITI](https://www.plshawlstoles.com/ladies-stole.html) | Composition, dimensions, MOQ, indicative price/unit | Reviewed product page; require dated quote before purchase comparison |
| P0 | [Lucky Enterprises](https://www.shawlsandcarpets.com/cashmilon-kashmiri-shawls-and-stoles.html) | Product and quotation leads | Reviewed catalogue capture |
| P0 | [Texworld / Apparel Sourcing Tashkent](https://texworld-apparel.uz/eng) | Event and exhibitor/material leads | Official page capture; current edition 12–14 October 2026 |
| P0 | [Hunarmand](https://craftfair.uz/?lang=en) | Regional craft and designer opportunities | Official announcement/participation evidence |
| P0 | [Uzexpocentre](https://www.uzexpocentre.uz/en/menu/krgazmalar-rezhasi) | Venue dates and calendar PDFs | Date cross-check with attribution; not application-deadline evidence |
| P1 | [Kazakhstan Fashion Week](https://kfw.kz/page/o-nedele) | SS27 application requirements | Reviewed application checklist; fee supplied after review |
| P1 | [Tashkent Fashion & Textile Expo](https://tftexpo.com/en) | Sourcing/B2B event | Official page; 25–27 January 2027 displayed |
| P1 | [Central Asia Fashion](https://fashionexpo.kz/en/) | Regional wholesale and exhibition leads | Official page; 14–16 March 2027 displayed |
| P1 | [TextileExpo Uzbekistan](https://textileexpo.uz/) | Event and future exhibitor list | Current homepage 27–29 April 2027; exhibitor list unavailable when inspected |
| P1 | [Visa Next Designer Award](https://www.visa.com.kz/ru_KZ/pay-with-visa/promotions/next-designer-award.html) | Recurring competition watch | Current 2026 offer expired; no invented next deadline |
| P2 | [Uzbekistan Travel](https://uzbekistan.travel/en/o/bukhara-host-international-silk-and-spices-festival/) | Regional festival discovery | Historical 2025 announcement; corroborate a current edition |
| P2 | [Première Vision digital tools](https://landing.premierevision.com/?fq%5Bmirakl_shop_ids%5D=B.O.B.+Textile) | International material/supplier leads | Old Marketplace replaced October 2024; avoid obsolete indexed API assumptions |

Khiva's 17–18 October 2026 carpet exhibition is corroborated by the
[Tourism Committee](https://gov.uz/en/uzbektourism/news/view/224119); the programme includes designer collections,
but exhibitor acceptance is not established. [Art Station Samarkand](https://artstation.uz/) needs manual programme evidence.

### Provider roadmap

| Provider/capability | Start with | Later, after access and workflow are agreed |
|---|---|---|
| Calendar | Download `.ics` for one saved event and its application deadline | OAuth calendar selection and incremental synchronization |
| Gmail / WhatsApp | User-selected reply/attachment evidence | Narrow authorized account connector and review inbox |
| Telegram | Forwarded announcement or saved URL | Bot in explicitly permitted channels; arbitrary channel monitoring is unsupported |
| Instagram | Saved official URL/screenshot | Supported professional-account API access, after account/permissions review |
| Currency | Record original quote currency | Official CBU rates with date/snapshot and manual-rate override evidence |
| RSS/Atom/ICS | Adapter only for a verified feed URL | Conditional requests, update/cancellation matching, per-provider rate controls |
| Webpage extraction | Reviewed link and evidence upload | Bounded fetcher with redirect/DNS/private-network protection and extracted draft review |

Provider references: [Google incremental sync](https://developers.google.com/workspace/calendar/api/guides/sync),
[Telegram bot access](https://core.telegram.org/bots/faq),
[Meta account access](https://www.postman.com/meta/instagram/folder/u4g5a2a/instagram-api-with-facebook-login),
[CBU official data](https://cbu.uz/en/arkhiv-kursov-valyut/veb-masteram/).

## Implemented in this iteration

1. Events, deadlines, suppliers and media use shape-matched first-load skeletons, persistent toolbar Refresh,
   safe error toasts and plain empty labels. Failed refreshes retain confirmed content.
2. Event detail exports `.ics` using the existing SDK download helper. Exact event dates and application
   deadlines become separate stable entries. Approximate/unknown event dates are omitted; internal notes stay private.
3. Integrations lists nine reviewed public sources, filterable by Events/Sourcing. This does not fetch or synchronize them.
4. Desk exposes six read-only MCP tools through the published backend SDK's stateless Streamable HTTP support.
5. Integrations includes an explicit MCP connection panel, endpoint, temporary token creation/copy, expiry and hide.
   Tokens remain in component memory, outside query caches and storage; identity changes cancel late results.
6. Development proxy routes both `/api` and `/mcp` to the certificate-verified backend.

The installed frontend SDK `0.0.6` lacks `useRefresh` and `SkeletonStateGroup`. Explicit refresh currently shows
button loading while retaining content. Adoption of the shared 400 ms refresh/skeleton lifecycle remains a separate
SDK upgrade; this iteration did not duplicate that infrastructure or modify SDK work owned by another chat.

## MCP boundary and setup

Endpoint: `/mcp`. Tools: `list_suppliers`, `get_supplier`, `list_supplier_media`, `list_events`, `get_event`,
`get_event_summary`. Media exposes metadata, not original bytes or GPS coordinates. Results may include private
supplier notes and contacts, so connect only an intended trusted client.

MCP is disabled by default. Enable `Mcp:Enabled=true` in the existing local configuration or `MCP_ENABLED=true`,
and use the existing `Integrations:SigningKey` configuration. Do not commit a signing key.
Capabilities are available to an organization member at
`GET /api/organizations/{organizationId}/integrations/mcp` and report actual usable state without exposing keys.

After a controlled server restart, sign in to Desk → Connected apps / Integrations → Assistant access.
Create an access token explicitly. Configure a client supporting Streamable HTTP and bearer authentication with
the displayed endpoint and token in its Authorization header, never a URL query string.
Access expires after five minutes. This is a short integration session, not an unattended OAuth connector.
A client that requires OAuth discovery/refresh cannot use this manual bearer flow; proper client authorization
and refresh are a later integration slice. No Claude/Codex connector was silently configured.

Every request checks live session and membership; organization comes from the audience-bound ticket, never
a tool argument. Cookies alone cannot grant MCP access; MCP tokens cannot authorize normal cookie APIs.
HTTPS and Origin checks run before protocol execution. Sign-out/session revocation or membership removal
invalidates access. Hiding a token clears the display and pending issuance, but does not revoke a previously copied token.

Existing local CLI importers accept filesystem paths and are deliberately not exposed as MCP tools.
Future draft import tools must use uploaded evidence IDs and existing authorization/revision/audit boundaries.
Provider credentials must remain separate from MCP client tokens, following
[MCP security guidance](https://modelcontextprotocol.io/specification/latest/basic/security_best_practices).

## Verification and remaining gates

- Backend build: zero warnings/errors. MCP, API, access and integration-ticket suites: 36/36.
- MCP tests use real protocol requests and disposable PostgreSQL: expiry, revocation, audience, tenant isolation,
  disabled endpoint, HTTPS and Origin checks.
- Calendar tests: 17/17; exact/approximate dates, exclusive end dates, escaping, folding and stable IDs.
- Frontend: Node 24 production build/typecheck/SFC, full ESLint and 336/336 tests passed.
- MCP frontend lifecycle: 8/8; expiry, tenant/session changes, late responses, hide during renewal, cache/storage isolation.
- Interactive proposal: material/event tabs, offer comparison, deadline state and evidence intake browser-checked.
- The current Desk runtime belongs to another session. Its configuration and operator data were not changed.
- Populated live browser verification remains pending operator sign-in; source and component tests are not live acceptance.

## Implementation sequence

1. Deliver the decided dual Materials/Suppliers entry and personal supplier views; retain owner UI review.
2. Add `MaterialRequest`, `SupplierOffer`, sample review and immutable quote evidence.
3. Add source observations and reviewed import candidates, including event edition and cancellation handling.
4. Add one verified provider/feed adapter and a controllable discovery run with receipts.
5. Add durable MCP client authorization when unattended use is needed; keep writes proposed/reviewed initially.
6. Add offers comparison/FX snapshots and user-authorized calendar synchronization.

The interactive proposal illustrates this direction. Its material examples are fixtures, not supplier quotations.
The implementation above does not claim that the proposed complete workbenches or automatic providers already exist.
