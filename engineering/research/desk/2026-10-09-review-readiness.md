# Events and Sourcing review readiness

*Last updated: 2026-10-09*

## Scope and outcome

Events and Sourcing run inside the Ocharo Desk single host at `https://localhost:8264/`. This sweep checked both domains, the Desk API and worker, and the Catalogue, Brand, Studio, Marketing and Editorial companions. Local verification is the outcome; this report does not claim production deployment, remote CI completion, or live provider activation.

## Changes

- Events editors retain their opening revision during refresh. Failed conflict reloads preserve drafts. Date precision changes use the currently visible value, and deadline labels refresh across midnight or browser wakeup.
- Event imports reject missing arrays and null entries before writing. Date filters reject reversed ranges. Failed reads do not masquerade as a newly confirmed empty result.
- Wrapped event title links have a continuous click target and a keyboard focus outline.
- Sourcing suppliers can load beyond the first 200 records. Supplier and media results deduplicate identities while preserving raw pagination offsets.
- Supplier, person, contact and media editors retain their opening revisions. Remote removal preserves an open draft for recovery within the existing session; failed reloads and failed saves do not discard it.
- Media selection follows the record identity through refresh. Dirty captions require explicit discard when closing. A successful own-delete closes the viewer; a failed delete preserves it.
- Architecture tests load ESLint configuration once under Node, outside individual assertion deadlines.
- Marketing and Editorial expose GET/HEAD technical health and status while retaining their business API integration gates.
- Studio's sign-in test formatting now passes its format gate.

## Local verification

| Area | Result |
|---|---|
| Desk backend | Release solution build; 133 unit and 168 E2E tests passed, none skipped |
| Desk frontend | 55 files, 387 tests passed; lint, typecheck, Vue SFC checks and production build passed |
| Events title adjustment | Existing Events component suite 23/23 and scoped lint passed; rebuilt and verified by an ordinary browser click opening the expected event detail |
| Catalogue | Backend 59; frontend 169; lint, typecheck and build passed with installed local UI SDK |
| Brand | Backend 10; frontend 22; lint, typecheck and build passed |
| Studio | Backend 31; frontend 120; lint, typecheck, format and build passed |
| Marketing | Backend 264; frontend 108; additional status-boundary run 47/47 passed |
| Editorial | Backend 183; frontend 44; additional status-boundary run 30/30 passed |
| Shared single sign-on | 15 backend tests and three companion snapshot gates passed |
| Image worker | 26 Python tests passed; live model worker not started |
| Desk review launcher | 12 Python tests passed |
| Public outputs | Atlas, Wear, Blog and Catalogue fixture packaging passed |

Backend E2E tests used disposable databases. Existing review data was backed up before additive Desk migrations. Browser checks used the existing local review operator; no supplier or event records were saved or deleted.

### Browser checks

- Existing sign-in survives the Desk restart and frontend refresh.
- Events feed, deadline links, search, empty-filter state, reset, detail, edit form, custom dropdown, date precision, validation and cancel were exercised.
- Sourcing loads the existing supplier list. Search opens the expected supplier; shared images, contacts, people and metadata render.
- Media preview loads the original image. Closing an unsaved caption prompts for discard; cancellation keeps the editor open, explicit discard closes it.
- Final UI evidence is kept outside Git in `/tmp/ocharo-events-review.png` and `/tmp/ocharo-sourcing-review.png`.

## Running endpoints

| Service | API / single host | Development frontend |
|---|---|---|
| Desk: Events and Sourcing | https://localhost:8264/ | Served by the same host |
| Catalogue | https://localhost:8278/ | https://localhost:8282/ |
| Brand | https://localhost:8286/ | https://localhost:8288/ |
| Studio | https://localhost:8260/ | https://localhost:8262/ |
| Marketing | https://localhost:8276/ | https://localhost:8358/ |
| Editorial | https://localhost:8280/ | https://localhost:8284/studio/ |

All six APIs and all five frontend proxies returned successful technical status/version responses. Desk's database-backed worker heartbeat is healthy. Processes are supervised locally for this review. The existing PostgreSQL container remains owned by the local environment.

Frontend proxies use verified HTTPS with the existing .NET public certificate through `VITE_API_CA_PATH`; certificate validation stays enabled. The public certificate export is `/tmp/ocharo-review-api-ca.pem`. Node 24.11.0 is used for frontend tools. Port 8266 belongs to another app and was not disturbed.

## Remaining boundaries

- Catalogue's earlier notification work depends on installed `@wow-two-beta/ui-vue` version `0.0.12-local.084820e625c7`. A clean install of its pinned published `0.0.12` fails typechecking because retained-notification APIs are absent. SDK publication and adoption remain separate, coordinated work; no SDK was published here.
- Marketing and Editorial business APIs require the missing platform organization binding. Their technical health endpoints work; business routes remain closed until properly configured. Authentication was not disabled to make a demo work.
- Telegram is disabled without bot configuration. No bot credentials, external notifications, provider calls, or live identity flows were activated.
- The image worker requires its existing private configuration before live use. Its software tests passed; model execution was not claimed.
- Events currently supports recorded entries, manual editing, JSON import and calendar export. Automated external event ingestion is not implemented by this sweep.
- Vite reports a Desk chunk above its size advisory. No bundle threshold was raised to hide it; route-level splitting remains a future measured performance task.
- Two early macOS Kestrel TLS connection failures were logged during local reconnects. Later validated API probes and browser workflows succeeded without console errors. The underlying platform failure was not diagnosed by this application sweep.
- Container image builds, production deployment and remote CI were not executed.

## Evidence and Git

Machine-local logs: `/tmp/ocharo-desk-tests.log`, `/tmp/ocharo-desk-verified-fe-tests.log`, `/tmp/ocharo-desk-final-verified-build.log`, `/tmp/ocharo-companion-gates/`.

Cohesive local commits: Events `aff61e9`, Sourcing `9d0ac87`, architecture tests `fe88ef8`, Marketing/Editorial status `2a62fa7`, Studio formatting `12bcbc9`. The final title target and this report are committed separately. Fetches found no incoming changes in the three edited repositories. Existing Catalogue notification edits were preserved; no SDK or remote publication was performed.
