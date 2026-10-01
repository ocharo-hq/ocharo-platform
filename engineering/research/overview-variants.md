# Business overview variants

*Last updated: 2026-09-27*

## Purpose

Show the real business workspace as a connected whole without inventing sales, engagement, automatic
integrations or stock. The editorial ivory/charcoal/tan treatment keeps the business feeling related to
Ocharo's printed material while the content remains organization-specific.

## Three views

| View | Primary use | Composition |
|---|---|---|
| Work map | Understand where each piece belongs | A central saved-design collection, linked catalogue/cards/media/content/brand nodes, one attention item and an informational Studio card |
| Attention board | Decide what to pick up | Actual content status totals, review/unfinished-draft signals and missing catalogue/card starting points |
| Compact dashboard | Move quickly between tools | A restrained inventory list, saved counts and one contextual next action |

The selected view is the only persisted client preference, under `brand-workspace.overview-view.v1`.
No organization, account, document, media, token or authorization state is written to localStorage.
Storage failures leave the selected view usable for the current visit. Buttons expose `aria-pressed`;
the small layout retains the same destinations and replaces graph positioning with an ordered card list.

## Evidence and boundaries

- Content, brands and uploaded-media counts come from the passed workspace controls.
- Catalogue and business-card totals come from authorized `getDesigns` reads and exclude archived designs.
- Failed/loading design reads render unknown totals instead of zero; missing-design prompts require a successful zero.
- Requests are cancelled on scope disposal or organization/session changes; late responses cannot replace new counts.
- Review and approved counts reflect draft status; approval is not presented as publication.
- The map describes how work relates; its connections do not claim automatic cross-app synchronization.
- Studio is explicitly a separate app. No deployed Studio URL or platform connection is invented.
- Marketing and scheduling are planned; the standalone journal is not connected to this platform.
- All active destinations use the existing access navigation guard. This view writes no business data.

## Implementation and verification

`OverviewPage.vue` and its namespaced `OverviewPage.css` are the complete presentation scope.
The coordinating application lane owns routing and `WorkspacePage` integration. Visual browser checks
and screenshots follow that integration; source/type checks alone do not establish layout acceptance.

Validation on 2026-09-27: frontend `pnpm typecheck` passed (Vue TypeScript and SFC checks),
and targeted ESLint for `OverviewPage.vue` passed. Final trusted-HTTPS browser checks exercised all three
views, remembered selection after reload and 390px mobile layout without overflow or page errors.
The [review guide](previous-work/rough-track/r0.3/r0.3-review.md) preserves the rendered alternatives;
developer preference remains open.
