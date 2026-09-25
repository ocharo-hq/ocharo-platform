# Brand Workspace — product definition

*Last updated: 2026-09-26*

## Purpose

Help a brand turn verified product facts and appropriate media into approved content, reliable delivery,
and understandable evidence of audience response. Content & Publishing is the first module of a wider
brand-operations platform. The product serves Ocharo first and models organizations and brands explicitly.

Brand Workspace is a working name, not a registered brand, domain, or validated market position.

## Users and responsibilities

An operator prepares content and handles delivery. A reviewer approves exact content and media revisions.
A publisher schedules approved work. A connection administrator authorizes external accounts.
One person may hold several roles; role separation does not require a large team.

An authenticated human belongs to an organization; a brand belongs to that organization. Neither an
editable organization identifier nor an external social account ID grants access by itself.

## Initial value

The first useful workflow is product/media selection → draft → review → approved publication package.
It supports explicit manual handoff before automated provider access is proven. The next workflow adds
one-time scheduling, confirmed publication, reconciliation, and honest response observations.

Repeated use should reduce preparation or recovery effort and improve consistency. Record qualified
enquiries when evidence supports them. Likes, reach, and attributed orders are different observations;
the platform does not equate engagement with incremental sales.

## Ownership

| Concern | Owner |
|---|---|
| Generic execution, persistence helpers, credentials, media primitives, UI | Shared wow-two libraries |
| Approvals, publication state, connection capabilities, observations | Reusable brand-operation modules and libraries |
| Garment facts, sizes, materials, apparel production rules | Optional apparel modules |
| Ocharo name, templates, languages, products, prices, editorial policy | Ocharo organization configuration and records |
| Garment editor, creative saved looks, texture placement, creative exports | Independent Ocharo Studio product |

## Boundaries

The initial scope excludes a complete order ledger, inventory system, production suite, public signup,
subscription billing, universal inbox, and every social channel. These remain expansion directions.
A module does not imply a separate repository or service.

Studio imports are optional. Genuine catalogue media can support the first workflow without Studio
running. Creative renders remain labelled creative studies; they do not establish available stock,
exact materials, physical fit, or workmanship.

Canonical business records remain in Maftuna's workspace. Importing an approved snapshot does not
silently transfer ownership or establish two-way synchronization.

## Evidence and uncertainty

Ocharo's existing product records and requested scheduling workflow support the first product case.
Another organization's fixtures demonstrate generality and isolation, not demand from another customer.
Actual provider eligibility, operator adoption, and sales effects remain evidence to collect.

Full rationale: [platform analysis][analysis] and [build design][design].

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md
[design]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform-content-publishing-build.md
