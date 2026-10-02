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
row keeps its height in either layout. The brand link goes to the catalogue gallery;
existing route selection, Preferences and theme/locale behavior remain active.

Use approved `ocharo-wordmark-ink.png` in light mode and `ocharo-wordmark-reverse.png`
in dark mode. These are byte-identical copies of the approved logo-family exports.
Keep the label on one line. Preserve visible keyboard focus and avoid clipping
navigation or compressing the wordmark to fit tablet navigation.

## Review

Implementation starts in Catalogue. Owner review remains open before treating this
trial as the accepted header for the remaining operator apps. Public editorial
mastheads retain their own app-specific design requirements.

## Ocharo app launcher

The approved stacked Catalogue header gains a nine-dot app button beside Preferences.
Use the published SDK `Popover`; show a three-column grid of app names, without icons
or descriptions. The current Catalogue app is highlighted. Keyboard focus enters the
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
