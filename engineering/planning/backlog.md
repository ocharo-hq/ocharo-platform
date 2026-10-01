# Ocharo Platform — Backlog

*Last updated: 2026-10-01*

## Features

| Feature | State | Spec |
|---|---|---|
| Workspace and approvals | implemented; owner review open | [preparation.md](../../product/features/preparation.md) |
| Catalogue editions | implemented; owner review open | [catalogue.md](../../product/features/catalogue.md) |
| Image processing | implemented; owner review open | [image-processing.md](../../product/features/image-processing.md) |

## Product and delivery

| Item | Type | Notes |
|---|---|---|
| Provider delivery | feature | Connect one authorized provider with durable send receipts. |
| Publishing recovery | engineering | Verify restart, lease expiry and crash-after-send reconciliation. |
| Metrics observations | feature | Distinguish unknown, stale, unsupported and measured zero. |
| Campaign recurrence | feature | Explicit schedules, channel formats and review collaboration. |
| Brand vector masters | feature | Sanitized source SVG and exported brand guidelines. |
| Merchant evidence | feature | Validate order evidence and real workflow attribution. |
| Companion consolidation | engineering | Retained Brand/Catalogue pilots stay loopback-only until absorption. |
| Hosted rollout | engineering | Verify backups, keys, provider configuration and target approval. |
| Migration version adoption | engineering | Adopt a published SDK migration field supporting full prerelease versions beyond 20 characters; never truncate identity. |
| Container source identity | engineering | Adopt a shared publisher that passes source revision to Docker; current v0.1.0 passes APP_VERSION only. |
| SDK password visibility labels | engineering | Published `@wow-two-beta/ui-vue` 0.0.6 `PasswordInput` keeps its internal Show password / Hide password labels in English and exposes no label override. Add resolver-backed messages in the SDK, verify uz/ru/en accessibility labels, publish through the SDK release process, then adopt that version here. Keep the working visibility toggle; do not patch installed packages. |

## Queued catalogue review — 2026-10-01

The initial feedback was queued during the translation batch. The user subsequently activated
canonical product review and named catalogue outputs. Their implementation and unchecked owner-review
scenarios now live in [v0.1, iteration 5](version-track/v0.1/v0.1.md#iteration-5--named-catalogues-and-product-review),
with [architecture and evidence](../architecture/catalogue-outputs.md).

Promoted work includes combined product facts/translations/prices, per-language confirmation and
review indicators, fixed UZS 1–9/10+ prices, the catalogue gallery, separate web/PDF designers and
explicit retained-pilot entry into Desk. These are no longer future queue items. Existing legacy
prices and immutable releases are preserved; no automatic currency conversion was introduced.

The remaining list below is still queued and is not approved final visual design. The screenshots
originated in the retained pilot. Continue from canonical Desk for the active owner review; do not
create another writable product source. Any recurring pilot HTTP 500 requires its own runtime/API
diagnosis; the new Desk entry link does not establish that the old runtime error is resolved.

- [ ] Replace the text placeholder with the approved Ocharo logo; add a considered `Catalogue` subtitle.
      Reuse this brand/application lockup in fitting apps, including Studio.
- [ ] Use a top navbar only. This supersedes the earlier collapsible-sidebar request, including
      its horizontally compressed/elliptical toggle and icon-only collapsed state.
- [ ] Make the logo open an Ocharo workspace/app-launcher dialog only after sign-in.
      Guests see a public catalogue and top navigation; admins/managers gain editing tools.
      Guests and ordinary users can save products and download/export PDFs. Define local guest
      saving and authenticated persistence explicitly before building account synchronization.
- [ ] Evaluate account/settings placement consistently for guests and signed-in users. Proposed
      direction: one account button with sign-in/join and language/theme for guests, profile and
      language/theme after sign-in. This is an analysis recommendation, not reviewed UI approval.
- [ ] Add product-image upload, removal and active/inactive controls. Require at least one active
      image when saving an editable product. Public product projection and designer eligibility
      must use the same active-media policy; preserve source media and existing immutable releases.
- [ ] Replace native dropdown menus with the styled, accessible published WoW2 frontend SDK controls.
- [ ] Open product and collection forms as a focused modal with a blurred backdrop on desktop,
      and a full-screen form on tablet/mobile. Avoid placing the form beside/inside the listing grid.
      Preserve draft edits on validation errors and warn before discarding unsaved changes.
- [ ] Show product collection memberships as restrained pills; cap visible pills and expose overflow
      without overwhelming the card. Validate desktop/mobile cards and keyboard navigation together.
