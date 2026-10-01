# Ocharo Platform — Catalogue design

*Last updated: 2026-10-02*

## Screens

Catalogue supports desktop (primary), tablet and phone. Review sizes are 1440 × 900,
820 × 1180 and 390 × 844; 320px is the narrow-width boundary.

## Catalogue header — selected option B

The user selected the compact-symbol layout on 2026-10-02 for Catalogue review.
This trial supersedes option C. The approved compact artwork is scaled without
reshaping the needle; ink and reverse assets match the reviewed option B bytes.

| Element | Value |
|---|---|
| Primary row | 56px, SDK `Navbar` |
| Brand link | At least 44px high; keyboard focus ring |
| Compact symbol | 32 × 32px; contain original aspect ratio |
| App label | Translated `Catalogue`, beside the compact symbol |
| Label type | UI sans, 14px / 20px, semibold weight |
| Brand gap | 10px |
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

Use approved `ocharo-symbol-ink.png` in light mode and `ocharo-micro-reverse.png`
in dark mode. These are byte-identical copies of the approved logo-family exports.
Keep the label on one line. Preserve visible keyboard focus and avoid clipping
navigation or shrinking the compact symbol to fit tablet navigation.

## Review

Implementation starts in Catalogue. Owner review remains open before treating this
trial as the accepted header for the remaining operator apps. Public editorial
mastheads retain their own app-specific design requirements.
