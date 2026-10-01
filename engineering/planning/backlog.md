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

The user explicitly queued this feedback for after the active translation/language batch.
Do not treat this list as current implementation or as an approved final visual design.
The screenshots show the retained Catalogue pilot, while canonical product editing and
fixed-page editions belong to Ocharo Desk. Resolve that entrypoint before implementing the shell;
do not introduce another writable canonical product store. The screenshot's HTTP 500 error
also needs a runtime/API diagnosis when this review lane starts.

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
- [ ] Merge Products, Prices and per-product translations into one Products experience and edit form.
      Include product facts, materials, prices, collections and translated copy (`Matnlar`).
- [ ] Add product-image upload, removal and active/inactive controls. Require at least one active
      image when saving an editable product. Public product projection and designer eligibility
      must use the same active-media policy; preserve source media and existing immutable releases.
- [ ] Accept prices in UZS only; every catalogue language shows those same UZS prices.
      This supersedes the current multi-currency editor only when this queued lane is implemented.
- [ ] Replace native dropdown menus with the styled, accessible published WoW2 frontend SDK controls.
- [ ] Open product and collection forms as a focused modal with a blurred backdrop on desktop,
      and a full-screen form on tablet/mobile. Avoid placing the form beside/inside the listing grid.
      Preserve draft edits on validation errors and warn before discarding unsaved changes.
- [ ] Show product collection memberships as restrained pills; cap visible pills and expose overflow
      without overwhelming the card. Validate desktop/mobile cards and keyboard navigation together.
