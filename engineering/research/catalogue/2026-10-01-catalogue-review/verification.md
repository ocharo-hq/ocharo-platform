# Catalogue gallery, editors and product review verification

*Last updated: 2026-10-01*


## Historical Desk verification

The user corrected the active target to **Ocharo Catalogue**, frontend `8282` and API HTTPS
`8278`. Native Catalogue now contains the gallery, product review and Web/PDF designers.
Read [native Catalogue verification](native-catalogue-verification.md) for the current review instructions.
The Desk architecture/results below remain historical implementation evidence, not instructions
to redirect Catalogue review into Desk. Existing Catalogue product data was retained. Authentication
and consolidation of the two stores remain explicit integration work; no migration occurred here.


## Status

Implementation and automated checks are complete. Owner review and live publication remain open.
The screenshot supplied in chat was the retained unauthenticated catalogue pilot at port 8282.
Canonical authoring is Ocharo Desk in `brand-workspace.*`, with authenticated organization ownership.

## Review entry

- Rider: `engineering/codebase/brand-workspace.backend-services/BrandWorkspace.Api`, profile `https`, port 8264.
- WebStorm: `engineering/codebase/brand-workspace.frontend-services`, `pnpm dev`, HTTPS port 8266.
- Open `https://localhost:8266/catalogue`, sign in, select an organization, and open Catalogues.
- Each saved catalogue exposes Web designer and/or PDF designer according to its enabled outputs.
- The retained pilot includes a new-tab Desk link, preserving unsaved pilot edits.

At verification time, only the retained pilot frontend 8282 and backend 8279 were listening.
Canonical 8264/8266 were not running. No user-owned Rider/WebStorm service was started, stopped,
or restarted. The retained pilot expects HTTPS 8278; its recurring proxy 500 is not established
as repaired by these changes. Use the canonical pair above for the new workflows.

## Implemented behavior

- One named catalogue record with description, stable URL slug, edited date, active state and independent Web/PDF outputs.
- Gallery creation, settings, inactive duplication, activation/deactivation and archival removal; revisions remain immutable.
- Web pages: ordered product selections, spread/grid/collage layouts, editorial/grid theme and 1440/768/390 device previews.
- PDF pages: original physical dimensions, editor, exact renderer preview, page activation and selected/all/current-page export.
- New catalogues start with an A4 cover containing only operator-entered title/description. Original Ocharo pages remain an explicit template choice.
- Shared copy, media group, selected product images and five price visibility flags travel into the published Web output.
- Public named routes live under `/catalogue/collections/{slug}/`; each catalogue may expose a responsive site, a PDF, or both.
- UZS product price fields cover 1–9 and 10+ pieces. Both amounts must be positive to confirm; incomplete drafts remain private.
- Unchanged legacy prices retain their original currency and confirmation. Editing prices resets confirmation; unrelated edits preserve it.
- Translation confirmation is independent per language. Product-list issue counts include unresolved core/explicitly authored translations.
- Publication eligibility is evaluated in the selected catalogue language. Web excludes individual ineligible products; PDF excludes their whole bound pages.
- Review questions are unresolved product facts, not customer FAQs. Their open issues continue blocking product publication.
- Styled warning dialogs replace ordinary in-app native confirms. Browser-required close/reload protection remains.
- API/page failures use toasts; failed loads retain retry controls. Field validation stays inline. Content loading uses skeletons.
- Local build identity (`dev`) moved from the primary breadcrumb to About.

## Automated evidence

| Scope | Result | Evidence / boundary |
|---|---|---|
| Canonical Vue suite | 247 tests passed across 42 files | Full suite after final neutral-scaffold and validation fixes |
| Canonical frontend | lint, typecheck, production build passed | Vite reports the existing large-bundle advisory |
| Platform unit suite | 83 tests passed | Includes both-positive price confirmation and semantic unchanged-price preservation |
| Platform API E2E | 101 full tests passed; 22 catalogue tests passed after output refinement | Test-host execution, not a live IDE runtime |
| Retained pilot | 19 tests, typecheck, production build and lint passed | SDK 0.0.7 modal/feedback integration; no API service restart |
| Marketing publication | 161 tests passed | Includes named slugs, language gates, independent outputs and frozen presentation settings |
| Public reader/gallery | Six phone/tablet/desktop fixtures passed | 390/820/1440 widths; no horizontal overflow; 44px action targets |
| Built canonical UI | Seven checks passed | [Browser receipt](verification.json); intercepted synthetic API fixture, no live account/data mutations |
| Actual PDF download | One A4 210×297 mm page, 60,283 bytes | [PDF receipt](pdf-inspection.json), [download](catalogue-cover.pdf) |

The exported PDF contains an embedded 1984×2806 raster page. Poppler visual inspection confirmed
“Ocharo autumn” without clipping, overlap or corruption. It does not provide searchable text.
The fixture contains no private product data. Browser screenshots and rendered fixture output
are verification artifacts, not approved Ocharo designs or proof of live publication.

## Browser review artifacts

- [Catalogue gallery](gallery.png)
- [PDF designer](pdf-designer.png)
- [Web mobile preview](web-mobile-preview.png)
- [Warning modal preserving edits on Cancel](discard-warning.png)
- [HTTP 500 toast and stable retry state](failed-load-toast.png)

## Shared convention status

WoW2 feedback, skeleton, data lifecycle and AlertModal conventions were updated locally.
The mft chat's active Git hook rejected `git commit --only` for the isolated convention change.
WoW2 already contained unrelated staged work, which was preserved. None of those unrelated files
was committed or unstaged. WoW2 push was OFF. Therefore these convention changes remain local,
separate from the committed/pushed Ocharo application batches. The task-only patch and test logs
are preserved in `ocharo-ws/.local/catalogue-review-20261001/` for the owning WoW2 chat.

## Owner review queue

Use [v0.1 iteration 5](../../../planning/version-track/v0.1/v0.1.md#iteration-5--named-catalogues-and-product-review).
The earlier navbar/logo, styled dropdown, product modal, active-image and guest-saving requests
remain [queued](../../../planning/backlog.md#queued-catalogue-review--2026-10-01).
No automatic publication, deployment, currency conversion or legacy-pilot data rewrite occurred.

## Implementation commits

| Repository | Commit | Change |
|---|---|---|
| Platform | `91431e2` | Named output contracts, eligibility and publication projection |
| Platform | `b8884c7` | Confirmed-price integrity across product saves |
| Platform | `c07201b` | Gallery, separate designers, product review and shared feedback |
| Platform | `28c11ea` | Retained pilot Desk entry and feedback integration |
| Marketing | `484a8e9` | Named responsive public catalogue readers and static routes |
