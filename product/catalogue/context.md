> Adopted source reference: Ocharo Platform owns this companion. Shared runtime integration is deferred.

# Ocharo Catalogue — Product Context

*Last updated: 2026-09-28*

> Current state — changes often. Durable definition: `product.md`.

## Current state

v0.1 built 2026-09-28 in one continuous session: backend + Vue operator app + tests. The real workspace import produced 35 products (29 active, 159 images). Browser-checked locally: grid, editor save → revision 2, price sheet, print sheet. Owner review has not happened.

## Open questions

- Should "active" require confirmed prices, or is listing without a confirmed price acceptable?
- The 2–9 piece price band is unconfirmed on every product — show "on request" in print, or hide?
- Looks (look-v01…v06) have photos but no names or prices — keep as drafts, or model them as outfits of products?
- Who approves ru/en copy?

## Decisions

- 2026-09-28 — Separate platform repo (user: "different platforms"); Marketing and Blog consume the public feed instead of copying product data.
- 2026-09-28 — Document-per-product storage with immutable revisions (schema churn stays low while the model settles).
