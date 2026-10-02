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
| Preferences | At least 44 × 44px below `lg` |

## Header tokens

Current warm-paper tokens remain the source of the header palette. They are defined
in `ocharo-catalogue.frontend-services/apps/web/src/bootstrap/index.css`.

| Semantic token | Light | Dark | SDK / Tailwind mapping |
|---|---|---|---|
| Canvas | `#f5f2eb` | `#201e1b` | `background` |
| Header surface | `#fffdf8` | `#292622` | `card` |
| Main ink | `#272924` | `#f4eee5` | `foreground` |
| Secondary ink | `#76796e` | `#bcb4a8` | `muted-foreground` |
| Accent | `#783e4c` | `#ddb0b9` | `primary` |
| Active surface | `#f3e7e9` | `#473239` | `primary-soft` |
| Divider | `#e6e2d8` | `#4b443c` | `border` |
| Focus | `#783e4c` | `#ddb0b9` | `ring` |

## Composition

`AppShellHeader` contains the SDK `Navbar` and the smaller-screen navigation row.
Below `lg`, the header grows to its contents and stacks those rows. The primary
row keeps its height in either layout. The public reader brand link goes to the catalogue gallery; the operator brand control
opens the app launcher. Existing route selection, Preferences and theme/locale behavior
remain active.

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

## Glass palette review — proposals, 2026-10-02

The warm-paper implementation remains current. The user reopened its color direction
because the colors looked old. The following paired palettes are design proposals,
not accepted tokens or app-wide rollout instructions.

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
