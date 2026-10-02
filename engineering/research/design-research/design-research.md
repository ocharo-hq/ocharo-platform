# Ocharo Platform — Catalogue design

*Last updated: 2026-10-02*

## Screens

Catalogue supports desktop (primary), tablet and phone. Review sizes are 1440 × 900,
820 × 1180 and 390 × 844; 320px is the narrow-width boundary.

## Catalogue header — selected option B

The user selected the smaller stacked full-wordmark layout on 2026-10-02 for
Catalogue review. The approved wordmark artwork is scaled without reshaping
the needle; the translated app name sits underneath.

| Element | Value |
|---|---|
| Primary row | 56px, SDK `Navbar` |
| Brand link | At least 44px high; keyboard focus ring |
| Full wordmark | 80 × 30px; contain original aspect ratio |
| App label | Translated `Catalogue`, centered underneath the wordmark |
| Label type | UI sans, 10px / 12px, medium weight, uppercase; 0.28em tracking |
| Brand gap | 2px vertically; 44px total stack |
| Desktop navigation | Inline at `lg` (1024px) and above |
| Tablet/phone navigation | Separate horizontal scroll row; at least 44px targets |
| Public Settings button | At least 44 × 44px below `lg`; operator preferences live in Settings |

## Header tokens

The user approved the white **Ink and Raspberry** palette on 2026-10-02.
The user selected **Neutral graphite** for dark mode on 2026-10-02. Catalogue
adopts both approved palettes. The source is
`ocharo-catalogue.frontend-services/apps/web/src/bootstrap/index.css`.

| Semantic token | Approved light | Approved Neutral graphite | SDK / Tailwind mapping |
|---|---|---|---|
| Canvas | `#F5F6FA` | `#141416` | `background` |
| Editing surface | `#FFFFFF` | `#1D1D20` | `card` |
| Header material | White at 84% | `#29292D` at 90% | `chrome` |
| Menu material | White at 92% | `#29292D` at 96% | `menu` |
| Main ink | `#181B25` | `#F4F2F5` | `foreground` |
| Secondary ink | `#555E72` | `#B7B3BD` | `muted-foreground` |
| Action fill | `#8B2459` | `#C24783` | `primary` |
| Action label | `#FFFFFF` | `#FFFFFF` | `primary-foreground` |
| Active surface | `#F8E9F0` | `#392330` | `primary-soft` |
| Active label | `#8B2459` | `#E69ABC` | `primary-soft-foreground` |
| Divider | `#DFE3ED` | `#3D3B42` | `border` |
| Essential control border | `#858EA3` | `#85808C` | `control` |
| Focus | `#9D2E6A` | `#E69ABC` | `ring` |

Light navigation and the app menu use 24px blur and 120% saturation. Dark uses
24px blur and 100% saturation, with 6% raspberry and 8% neutral ambient tint. The header
owns the material; the nested `Navbar` is transparent. The SDK popover is portalled,
so its backdrop does not sit inside the header's blur layer. Content cards and
editing fields stay opaque. Canvas gradients use restrained raspberry and blue
ambient tint, without applying filters to garment photographs or changing authored
Web/PDF catalogue colors. Print removes interface gradients.

Reduced-transparency, forced-colors and unsupported-blur modes use solid surfaces.
Those fallbacks apply to both accepted palettes. This change is
Catalogue-specific; it does not roll out an unreviewed palette to the other apps.

## Composition

`AppShellHeader` contains the SDK `Navbar` and the smaller-screen navigation row.
Below `lg`, the header grows to its contents and stacks those rows. The primary
row keeps its height in either layout. The public reader brand link goes to the catalogue gallery; the operator brand control
opens the app launcher. Route selection and independent interface/content language
behavior remain active.

Use approved `ocharo-wordmark-ink.png` in light mode and `ocharo-wordmark-reverse.png`
in dark mode. These are byte-identical copies of the approved logo-family exports.
Keep the label on one line. Preserve visible keyboard focus and avoid clipping
navigation or compressing the wordmark to fit tablet navigation.

## Review

Implementation starts in Catalogue. Owner review remains open before treating this
trial as the accepted header for the remaining operator apps. Public editorial
mastheads retain their own app-specific design requirements.

## Ocharo app launcher

The approved stacked Catalogue wordmark and app label form the app-launcher button.
A 16px chevron sits to the right and rotates while open; hover and open states show a
subtle surface. This supersedes the separate nine-dot button trial on 2026-10-02.
Use the published SDK `Popover`, anchored bottom-start; show a three-column grid of
app names, without icons or descriptions. The current Catalogue app is highlighted. Keyboard focus enters the
panel; Escape and outside click dismiss it. Keep a 44px trigger and at least 44px tiles.
The panel fits narrow screens and uses the existing light/dark surface tokens.

Workspace, Brand, Catalogue, Marketing, Blog studio, Studio, Atlas, Wear and Blog are
browser destinations. Each destination retains its own access checks. Other apps open
in a new tab, preserving this editor; Catalogue stays within the router and its guards.
`PlatformLinks.ts` owns HTTPS frontend URLs and `VITE_OCHARO_*_URL` overrides.
Public `/c/` readers hide the operator launcher. Catalogue currently remains a loopback
pilot without sign-in; this menu is navigation, not authentication or single sign-on.

## App identity recommendation

Keep the approved Ocharo wordmark plus app label for Catalogue, Workspace, Brand and
operator Marketing. Atlas, Wear and Blog can have different editorial styles while
retaining the Ocharo identity. Studio is a candidate for a distinct logo with a subordinate
`by Ocharo` endorsement if it is marketed independently to public creative-tool users.
Separate app-logo generation remains unapproved. Any endorsement uses the exact approved
Ocharo artwork; tiny launcher icons carry no endorsement text.

## Local launcher verification — 2026-10-02

Catalogue frontend build/typecheck and lint passed. The existing architecture and
destination safety suites passed all 10 tests. An isolated fixture using the actual
header and launcher passed 36 width/locale/theme combinations: 320, 390, 768, 820,
1024 and 1440px; Uzbek, Russian and English; light and dark. No header overlap,
clipping, page overflow or browser errors were observed.

Keyboard checks covered opening, all nine links, Escape focus return and outside
dismissal. Other-app navigation was intercepted to verify HTTPS/new-tab destinations
and preservation of the current route; this does not establish service availability.
Public reader isolation passed all six locale/theme combinations. The final accessible
dialog name was verified after rebuilding the attribute-forwarding change.

These local checks do not close the version-track owner-review boxes.

## Merged logo-menu verification — 2026-10-02

Frontend build/typecheck and lint passed. The actual-component fixture passed all 36
width/locale/theme cases after moving the launcher into the logo. The logo remains
80 × 30px; the trigger is at least 44px high and the primary row remains 56px.
The menu stays within the viewport at 320px; inline navigation has clearance at
1024px in all three languages. Keyboard opening, all nine links, Escape focus return
and outside dismissal passed. Other apps preserve the current route in a separate tab.
All six public-reader locale/theme cases retain the brand link and hide the launcher.
Owner review remains open.

## Initial glass palette review — 2026-10-02

The user reopened the color direction because the colors looked old. The initial
paired proposals below are retained as the review record. Only the white Ink and
Raspberry direction was approved; its navy dark counterpart was not accepted.
The current adopted light tokens are listed above, and the dark refinement follows.

Recommend **Ink and Raspberry**: replace beige/brown surfaces with neutral white and
cool graphite, keeping a restrained raspberry accent for Ocharo's fashion identity.
Porcelain and Cobalt is crisper and more tool-like; Pearl and Jade is calmer and
craft-oriented. Those style assessments are design judgments, not measured user results.

Each cell lists light / dark. Glass opacity is 84% for the navigation material;
use a denser 92% menu layer over varied content and verify the actual background.

| Token | Ink and Raspberry | Porcelain and Cobalt | Pearl and Jade |
|---|---|---|---|
| Canvas | `#F5F6FA` / `#0F1118` | `#F3F6FC` / `#0D1424` | `#F2F7F5` / `#0B1714` |
| Glass surface | `#FFFFFF` / `#1C2030` | `#FFFFFF` / `#19243A` | `#FFFFFF` / `#172A24` |
| Main text | `#181B25` / `#F4F5FA` | `#151E30` / `#F3F6FC` | `#13251F` / `#EEF8F2` |
| Accent | `#8B2459` / `#F2A4CC` | `#2457CE` / `#99BBFF` | `#006C54` / `#7BE2B2` |
| Soft accent | `#F8E9F0` / `#3D2438` | `#E8EEFC` / `#253960` | `#E3F4EC` / `#203E33` |
| Essential control border | `#858EA3` / `#67718A` | `#8493AA` / `#657B9C` | `#789789` / `#5F8775` |
| Focus | `#9D2E6A` / `#F2A4CC` | `#2457CE` / `#99BBFF` | `#00795E` / `#7BE2B2` |

Use glass for navigation, menus and overlay chrome; keep product photography, text
editing fields and exported catalogue pages opaque. Menu/content text remains fully
opaque. Avoid translucent layers stacked over each other, and provide a solid surface
when reduced transparency or forced colors is requested or blur is unsupported.

[Fluent's material guidance](https://fluent2.microsoft.design/material/) recommends
acrylic for transient menus and popovers. [Microsoft's acrylic guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic)
covers layering and solid fallbacks. Target at least 4.5:1 for normal text under
[WCAG text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
and 3:1 for essential control indicators under
[WCAG non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).

Calculated text contrast for 84% glass over the specified neutral canvases is
17.00 / 15.24 (Raspberry), 16.50 / 14.84 (Cobalt), and 15.83 / 14.39 (Jade).
These calculations do not certify arbitrary photographs or gradients beneath glass;
verify composited backgrounds and all interaction states during implementation.


## Dark refinement — selection, 2026-10-02

The initial navy proposal used the same `#1C2030` for glass and content, flattening
the surface hierarchy. Its blue ambient tint intensified under saturation. A single
pastel accent served both filled actions and readable accent text, which made actions
visually dominant. These are design assessments; its calculated text contrast already
passed the stated target.

The user selected **Neutral graphite**: distinct neutral surfaces, denser dark glass
and a restrained raspberry action fill. **Soft charcoal** remains a historical
alternative. The approved white side stays identical. The separate dark selection
authorizes the Catalogue dark implementation.

| Token | Neutral graphite | Soft charcoal |
|---|---|---|
| Canvas | `#141416` | `#181617` |
| Glass | `#29292D` | `#302B2D` |
| Content | `#1D1D20` | `#211E20` |
| Elevated content | `#262629` | `#2D282B` |
| Main text | `#F4F2F5` | `#F7F2F4` |
| Secondary text | `#B7B3BD` | `#C0B5BB` |
| Action fill / label | `#C24783` / `#FFFFFF` | `#C24783` / `#FFFFFF` |
| Accent text / focus | `#E69ABC` | `#E79BBD` |
| Soft accent | `#392330` | `#432834` |
| Divider | `#3D3B42` | `#494046` |
| Essential control | `#85808C` | `#90838A` |

Dark proposals use 90% header opacity, 96% menu opacity, 24px blur and 100%
saturation. Reduce ambient tint to 8% and raspberry glow to 6%. Keep primary actions
on opaque content, and garment photographs unfiltered.

Calculated normal body contrast on opaque content is 15.11:1 for Neutral graphite
and 14.92:1 for Soft charcoal. Action-label contrast is 4.64:1 in both. Secondary text
on a 96% menu over its own canvas is 7.12:1 and 7.09:1 respectively. Essential control
borders on a 96% menu over white are 3.34:1 and 3.40:1. These are defined-composite
calculations, not certification of arbitrary underlying content.


## Approved light implementation verification — 2026-10-02

Catalogue frontend build/typecheck, lint, targeted formatting and `git diff --check`
passed. An isolated browser fixture used the actual `AppRoot`, app menu, published
SDK and compiled app stylesheet, with its settings request mocked. The editing
sample used the actual opaque panel and field utilities; no user services were
started, restarted or contacted.

All six width/theme cases at 390, 1024 and 1440px passed: the header is 84% white,
the menu 92% white, both with 24px blur; the nested navbar is transparent. The
primary row remains 56px and the approved wordmark 80 × 30px, with no image filter.
Editing panels and inputs are opaque white, essential field borders use `#858EA3`,
and no page overflow occurred. Escape dismissal returns focus to the logo trigger.

Browser emulation verified reduced-transparency and forced-colors fallbacks: solid
surfaces, no blur and no ambient gradient. Print uses white document/canvas/main
backgrounds and hides navigation. Unsupported-blur fallback is defined in CSS;
the test browser supports blur, so that capability branch was not executed.
All 23 pre-existing dark color tokens match the previous compiled stylesheet.
Public readers in both modes hide the launcher and request no operator settings.
Authored Web/PDF catalogue styling and original image assets were not edited.

The revised dark comparison passed 24 responsive and interaction cases across its
three proposals. The approved white tokens, layout and embedded artwork match the
initial review. At that light-only stage both dark variants remained proposals. The subsequent
Neutral graphite selection is recorded above; local checks do not close owner-review
boxes or establish production deployment.


## Catalogue navigation and settings — 2026-10-02

The operator navigation follows the selected work order: **Catalogues → Collections
→ Products → Stock → Settings**. Products and their existing root route remain
unchanged; catalogues and collections lead the menu because they change more often.

Operator Settings is the single entry for browser interface language and appearance,
Catalogue defaults, and Catalogue import/download tools. The duplicate operator
Preferences button is removed. Global Ocharo brand identity remains owned by Brand;
Catalogue defaults only configure this app's output and product metadata.
Public `/c/` readers retain a Settings modal with interface language, appearance and
About only. They request no operator settings and expose no import/default editing.

Saved interface language hydration was working. Hardcoded Uzbek page text caused
the English-selected interface mismatch. Settings, Stock, Collections, product-editor
validation and legacy routed controls now use English source keys with complete
Uzbek/Russian messages. Known SDK control labels receive the same locale through
its provider. Authored product/collection/copy and stored history text stay untouched;
server-provided error details and import warnings retain their original language.

Changing interface settings persists in browser storage and does not write backend
Catalogue defaults or change a design's content language. Neutral graphite applies
to interface chrome and editing surfaces; authored web themes and exported PDF pages
retain their own colors and media.


## Graphite and consolidated settings verification — 2026-10-02

An isolated browser fixture compiled the actual `AppRoot`, `SettingsPage`, shared
interface controls, published SDK and app stylesheet. It mocked settings reads and
forbade writes; it did not use or restart the developer's Rider/WebStorm services.

Four desktop/phone cases at 1440px and 390px covered both themes. The primary row
stayed 56px, the wordmark 80 × 30px, and the single operator Settings entry followed
the selected menu order. Both palettes had no page overflow. Narrow garment-type
fields use a contained horizontal table scroll rather than compressing their values.

Real SDK language controls changed English → Uzbek → Russian and restored Russian
on reload. Authored defaults and unsaved form values stayed intact, with zero backend
writes. Public Settings passed in both themes: two interface controls, no operator
navigation, no Catalogue defaults/import controls and no operator settings request.
Launcher keyboard dismissal restored focus to the logo trigger.

Neutral graphite header/menu materials use 90%/96% opacity, 24px blur and 100%
saturation. Editing cards and fields remain opaque. Primary fills and light accent
text use separate semantic tokens. Reduced-transparency and forced-colors emulation
verified solid surfaces, no blur and no gradient in both themes. Print uses a white
interface canvas and hides navigation. Unsupported-blur fallback remains defined in
CSS but cannot execute in this browser because it supports blur.

These checks verify interface behavior with mocked data, not live authentication,
production deployment, catalogue publication or completed owner review.


Final Catalogue frontend gates passed: lint, formatting, typecheck/component compile,
production build and all 74 tests across 14 files. Four preferences regression tests
cover navigation, live locale changes, browser persistence and public isolation.
API/screen checks cover localized safe SDK failures and numeric designer failures;
raw backend ProblemDetails remain unchanged. The browser receipt matched the final
interface source hashes and recorded no errors, failed checks or page overflow.
