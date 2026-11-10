# Shared shell adoption

*Last updated: 2026-11-10*

Ocharo's seven operator apps (Desk, Brand, Catalogue, Marketing, Editorial, Studio, Assets) move onto the one shared
app shell the fe sdk lane builds in `@wow-two-beta/fe` (`events-integration #59`, `#60`). This is the platform lane's
track of that adoption: what each app's local shell does beyond drawing the frame, what the shared shell has to accept
for it, and what stays Ocharo's own. Measurements (bar height, widths, fonts, colours) are in the orchestrator's sweep
(`events-integration #57`, Ocharo shell facts) and are not repeated here.

Source: three read-only agents on 2026-11-10, reading each app's `bootstrap/AppLayout.vue`, `bootstrap/AppRoot.vue`,
`presentation/frame/`, sections, app-switcher and locale files at `ocharo-platform` `c633538`, `ocharo-marketing`
`f70f68e`, `ocharo-studio` `873dc0e` and `ocharo-assets` `098d344`. SDK internals were not read.

## Status

- ✅ Inventory written 2026-11-10; the gaps in #1 went to the orchestrator for the fe sdk lane.
- ✅ The shell shipped as `AppFrame` in `@wow-two-beta/fe` `0.0.45`; every need in #1 is answered except #12, declined
  (Studio takes the frame's drawer on a phone, or keeps its menu in the bar).
- ✅ Brand pilot 2026-11-10 (`b91f7c8`): the pattern in #6, read locally against fixture data.
- 🔄 Catalogue, Desk, Marketing, Editorial, Studio and Assets follow the pattern; each repo's version track records its
  release and the sizes read on dev.

## #1 What the shared shell must accept

The shell's contract names a top bar, a sidebar with persisted collapse, an account menu, a breadcrumb, a content
width, a version badge, and from the consumer a brand mark, nav groups, app-switcher items, a top-bar slot, a language
picker and the theme. Ocharo's apps need these beyond that list.

| # | Need | Who needs it |
|---|---|---|
| 1 | No sidebar at all, and a sidebar that can be absent at runtime | Assets (none); Desk hides it until an organization is open; Catalogue's public reader |
| 2 | A bare public mode: bar with a brand mark and one slot, no app switcher, no account menu | Catalogue's public reader (`PublicReaderLayout.vue`) |
| 3 | Collapse persistence under a consumer storage key, a consumer default (Studio defaults to the rail), a synchronous first-render seed, the rail forced below `lg`, hidden below `md` | all six sidebar apps; a new key or default resets every user's choice |
| 4 | Bar height and sidebar widths exported as CSS variables | Studio reads `--frame-bar-height` in 3 places (toast offset, a list's max height) and its container queries answer the width the sidebar leaves |
| 5 | Content width as a consumer value, and a full-bleed main: no gutter, no max width, a flex column, the only scroller | widths 1400 (Brand), 1536 (Desk), 1600 (Marketing, Editorial), 1800 (Catalogue); full-bleed in Studio and Assets |
| 6 | The active nav item set by the consumer, never by path matching | all six: each resolves it from route meta, and several routes share one section |
| 7 | Nav items rendered as router links | Desk, Marketing, Editorial, Catalogue and Studio guard unsaved work in the router; a raw anchor skips it |
| 8 | A trailing badge per nav item, hidden in the rail | Marketing's draft count on Posts |
| 9 | An external nav item that opens in a new tab | Editorial's "Open site" |
| 10 | Ungrouped items, a headingless first group, and a sidebar footer slot | Editorial (flat), Desk and Brand (headingless Home group), Studio ("Studio home" above the collapse button) |
| 11 | Phone navigation: a bottom bar taking a per-item flag and a short label, plus a "More" sheet with a consumer title | Desk, Brand, Catalogue, Marketing, Editorial |
| 12 | Phone navigation as a top-bar menu instead, with no bottom bar | Studio |
| 13 | Account menu without sign-out, and an appearance-only anonymous menu with its own trigger label | Studio signed out; Assets always |
| 14 | An account-menu items slot | Desk ("Account settings"), Studio ("Studio settings", "Sign in") |
| 15 | Organizations in the account menu with an `update:organization` event | Desk |
| 16 | Sign-out as the consumer's async handler, which may refuse; the shell never navigates itself | Desk confirms on unsaved edits; Marketing, Editorial and Studio hold sign-out while work is unsaved or saving |
| 17 | The account menu's open state as a model or event | Brand, Catalogue, Marketing, Editorial and Studio fetch the API version only when it opens |
| 18 | Versions as consumer rows (`label`, `version`, `commit`), shown beside the app button or in the menu only | each app has a build version and an API version from its own `/api/system/status`; Desk shows them in the menu only |
| 19 | Three top-bar regions: after the app button, a flexible middle that can replace the breadcrumb, and actions before the account menu | Studio's phone menu; Studio's teleport target `#studio-frame-bar`, which must be mounted before the page teleports into it; Refresh, "New post", "New story", Catalogue's notifications bell, Brand's release status |
| 20 | Breadcrumb as translated strings with no links, optional and hideable | all five that show one; Studio shows none |
| 21 | A slot above the page content, and a default slot the consumer fills with its own skeleton, error boundary and disabled-fieldset wrapper | the signed-out, expired and read-only connection card in Brand, Catalogue, Marketing and Editorial; dismissible notices; Desk's account settings panel |
| 22 | The main scroll container exposed, or a scroll-to-top method | Desk scrolls to top on a section change |
| 23 | Print: bar, sidebar and phone bar hidden, the frame back in normal flow | Brand, Catalogue, Editorial |
| 24 | Every shell string as a prop or message map, and no language picker required | uz, ru and en in Desk, Catalogue, Marketing, Editorial; Brand fixed to uz with no picker; Studio and Assets English only |
| 25 | App switcher optional; its subtitle a consumer value | Assets and the public reader have none; Desk shows the organization name |
| 26 | The colour-mode storage key and pre-paint script left with the consumer | seven keys exist (`ocharo.desk.theme` … `ocharo-studio.theme`), and Studio's script migrates a legacy value |
| 27 | Safe-area top inset on the bar, and a class passed through on every region | all apps set the inset by hand today; print and Studio's CSS hang off region classes |

## #2 What stays Ocharo's own

The SDK shell is generic, so these stay outside it. Today they are copied in five places: Desk, Brand, Catalogue,
Studio, and Marketing's `@ocharomarketing/connect` package, which only Marketing and Editorial can import.

- The app list: nine keys (six workspaces, three public sites), labels, summaries and icons.
- Address resolution: `OCHARO_*_URL` from `/api/runtime-config`, then build-time values; a missing or non-https
  address drops that app from the menu.
- The "O" brand tile (`.oc-mark`, `.oc-app-icon`).
- The session connection card and each app's session logic.
- The unsaved-work registry that holds sign-out.
- The locale state, the key `ocharo.operator.locale`, and the shell's message dictionaries in uz, ru and en.
- The build-identity reader over `/api/system/status`.

Open: one home for these across four repositories. No package is shared across Ocharo's repositories except the SDK.

## #3 Per-origin state

Theme, language and sidebar collapse live in each app's own `localStorage`, and each app is its own origin. A person
who picks dark mode or Russian in Desk still gets the other app's own setting after switching. The shared shell makes
the frame identical, not these choices; carrying them across apps needs a store all origins can read.

## #4 Drift the adoption settles

Differences between apps that read as copies drifting, not intent.

- Sign-out wiring: Desk listens for `sign-out`; the others pass an `onSignOut` prop through a spread object.
- API version: Desk fetches it on mount, the others when the account menu opens.
- App version beside the app button: shown in five apps, absent in Desk.
- Unsaved-work guard: app-wide in Desk; page-level in Catalogue's designer, Marketing, Editorial and Studio; none in
  Brand, so edits on its Identity page are lost on any navigation.
- Sign-in return path: Brand keeps the query string, Catalogue only the path.
- Route announcer: mounted in six apps, absent in Desk. The SDK's skip-link string is translated in Brand and
  Catalogue only.
- Refresh control: an icon button in Desk, a labelled button in Brand and Marketing, absent elsewhere.
- Mount element: `#app` in Desk, `#root` elsewhere.
- Default language: `en` in Desk, `uz` in Catalogue; Brand has no picker; Brand's loading placeholder is hard-coded Uzbek.
- Editorial's leave-page confirm is a hard-coded Uzbek string in a native dialog.

## #5 Adoption order

1. Brand and Catalogue: near-identical layouts, the smallest change.
2. Marketing and Editorial: one change through the `connect` package.
3. Desk: the organization switch, the absent-sidebar state and the app-wide guard.
4. Studio: the top-bar teleport, the rail default, the CSS variables and the full-bleed main.
5. Assets: the bare bar.

## #6 The adoption pattern

Fixed on Brand and repeated in every app. An app's `bootstrap/AppLayout.vue` renders `AppFrame` and keeps only what
is its own.

| Piece | Where it lives now |
|---|---|
| Bar, sidebar, rail, page column, their sizes, glass, print rules, safe-area inset, skip link | the frame |
| Collapse toggle, its persistence, the tablet drawer | the frame; the app passes its existing storage key as `collapse-storage-key`, so nobody's choice resets |
| App switcher and brand mark | the frame's brand cell; the app passes `apps`, `current-app`, `app-label`, `app-subtitle`, the switcher's phrases and the `O` mark in `#brand` |
| Version beside the app button | the frame's badge: `version` is the build's, `running-version` follows the API's status through `useRunningVersion`; a `dev` build compares nothing |
| Sections | the app's nav component in `#nav`, without a collapse control; router links, the active item chosen by the app |
| Phone bar | the app's component in `#phone-nav`, without placement classes |
| Trail | `crumbs` as translated strings, `trail-label` |
| Bar controls | `#bar-end` |
| Account menu | `account` (identity, versions, busy state), `v-model:account-open`; languages and organizations where the app has them |
| Connection card, notices | `#notice` |
| Page, error boundary | the default slot |
| The frame's own phrases | the app's `provideLocale` map: `AppFrame.collapseLabel`, `.expandLabel`, `.openNavigationLabel`, `.navigationLabel`, `VersionBadge.label`, `.updateLabel` |

Picks:

- Page column: the frame's 1536px in Brand, Desk, Marketing and Editorial (Brand was 1400px, Marketing and Editorial
  1600px); Catalogue keeps 1800px for its designers; Studio and Assets are full-bleed.
- The API version is read when the frame mounts and every five minutes after, for the badge; the account menu's
  version rows still read when the menu opens.
- Local check without a database or sign-in: the built app served beside fixture answers for its `/api` routes over
  the machine's mkcert certificate; sizes read with `getBoundingClientRect()` on `header[role="banner"]` and `aside`.
