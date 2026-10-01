# Catalogue languages and product ownership

*Last updated: 2026-10-01*

## Decision

The operator interface supports Uzbek, Russian and English. A saved catalogue owns its own
content language, so an Uzbek interface can edit an English catalogue without changing either
language implicitly. The catalogue authoring registry supports 25 languages. Supporting a language
means editors can enter and review its text; it does not mean a translation is finished or published.

| Stage | Languages | Reason for authoring support |
|---|---|---|
| Initial content | Uzbek (`uz`), Russian (`ru`), English (`en`) | Existing brand copy and original catalogue templates |
| Regional | Tajik (`tg`), Kazakh (`kk`), Kyrgyz (`ky`), Turkmen (`tk`), Turkish (`tr`), Azerbaijani (`az`) | Neighboring and connected audiences; validate with enquiries |
| International priority | French (`fr`), Italian (`it`), German (`de`), Arabic (`ar`), Simplified Chinese (`zh-Hans`) | Buyer conversations, fashion/trade audiences and user-requested languages |
| Further authoring | Spanish (`es`), Portuguese (`pt`), Polish (`pl`), Ukrainian (`uk`), Persian (`fa`), Urdu (`ur`), Korean (`ko`), Japanese (`ja`), Indonesian (`id`), Malay (`ms`), Hindi (`hi`) | Make requested translations possible without schema or export changes |

This is a language capability list, not 25 approved market launches. Initially expose only released
language editions to buyers. New product translations start as drafts and require explicit review.
Core template text exists in three languages; additional shared catalogue text starts blank so
English is not silently presented as a completed translation.

## Research and limits

- The [Uzbekistan light-industry agency's 2025–2026 export report](https://gov.uz/en/adli/news/view/136514)
  names Russia, Türkiye, Belarus, Kyrgyzstan, Kazakhstan, China, Germany and the UAE among major
  textile export partners. It also describes newer destinations including Algeria and Angola.
  These are textile-industry signals, not evidence of demand for Ocharo garments. Priorities should
  change with Ocharo's actual enquiries and conversion by language; do not exclude whole continents.
- [W3C guidance on language tags](https://www.w3.org/International/articles/language-tags/index.en?changelang=en)
  supports short BCP 47 tags and script distinctions where useful. `zh-Hans` describes Simplified
  Chinese without claiming a single national market.
- [W3C language declarations](https://www.w3.org/International/questions/qa-html-language-declarations.html)
  distinguish the interface's language from separately authored content. Arabic, Persian and Urdu
  need text direction handling; that does not reverse the operator interface or page geometry.

## Ownership and persistence

- Products own localized names, fabric/material text, descriptions, sizes, prices and media membership.
- The existing `document.locales.uz/ru/en` fields remain authoritative. Additional copy is additive
  `translations[locale]`, preserving old product identities and existing data.
- Products own named media groups in model, product, detail and fabric categories. A group can only
  refer to media belonging to that product. Private references are ineligible for public/design media.
- A design owns per-language shared copy: collection heading, cover note, header/footer, size/quantity
  labels, price-on-request text and material/decoration labels. It also owns price visibility choices.
- Bound product text/prices are copied into the saved design snapshot only through explicit refresh.
  Canonical product changes cannot mutate an already saved or published edition behind the editor.
- `Connect products in a new copy` adopts verified original template relationships. Custom/unresolved
  fields require an explicit product binding. The original freeform design remains available.
- Media selection changes a product's photograph within its eligible group. Randomization never draws
  from another product, preserves page geometry, and uses a single undo step for a whole catalogue.
- Marketing reads an immutable Platform release. Public language navigation uses released edition
  locales from that release, not every language in the authoring registry. Downloads retain the exact
  selected edition and its pinned release identity.

## Verification boundary

Automated checks cover persistence, old-client compatibility, exact language projection, media ownership,
designer state/undo, document validation, and release-bound public downloads. Editorial review of
translations and visual proofing of every target script/PDF remain human checks. Machine-generated
interface translations are implementation copy, not independently certified translations.

The later logo/navigation, product-modal, styled-dropdown, UZS-only and active-image feedback is queued
in [the backlog](../../planning/backlog.md#queued-catalogue-review--2026-10-01), outside this language batch.

## Implementation verification — 2026-10-01

| Area | Executed result |
|---|---|
| Canonical Platform frontend | 213 tests; lint, typecheck and production build passed |
| Canonical Platform unit tests | 63 passed after final publication/brand checks |
| Canonical Platform API | Full 93-test suite passed; final 19 catalogue API cases passed after publication/brand fixes |
| Additional-language publication | French, Arabic and Simplified Chinese accepted; unsupported languages rejected |
| Brand integrity | Document-level catalogue brand must match the edition target; old snapshots keep page-level checks |
| Actual catalogue renderer | Arabic, Chinese and Uzbek/Cyrillic PNG/PDF smoke passed; see [retained evidence](2026-10-01-typography-smoke/typography-smoke.md) |
| Retained Catalogue / Brand sources | 18 / 5 backend tests passed after HTTPS origin changes |

The full API run used disposable PostgreSQL containers. The browser smoke used a temporary
browser profile and in-memory fixture origin. User-owned Rider/WebStorm services were not restarted.
The older retained Catalogue pilot is not the canonical authenticated designer: review this work
through Platform's Commerce/product and Catalogue designer screens. Resolving that duplicated
entrypoint is part of the queued UI work.

Owner review remains open: edit an English catalogue while the interface is Uzbek, refresh a
changed product price explicitly, choose a media group, randomize and undo, and inspect the
downloaded PDF. Additional languages still require authored shared/product text and editorial review.
