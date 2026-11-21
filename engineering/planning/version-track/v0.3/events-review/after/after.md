# Events app after-fix check: desktop and phone, dev

*Last updated: 2026-11-21*

Read-only re-check of the 32 defect rows of [events-review.md](../events-review.md) on dev, after fe `0.0.60`, fe-events `0.0.20`, Ocharo `6b57cf3` (the app menu reads Events `0.3.40`, API `0.3.40 · 6b57cf3`), under the Samarkand Turquoise theme. Result: 29 pass, 3 fail (#6, #10, #21).

## Method

- Chrome profile `wow2`, one tab of the session group; phone = window 630 pt wide so the page viewport is exactly 390 px (`innerWidth` 390), desktop = window 1680 pt wide, viewport 1440; window restored to 1512×949 after; theme put back to System
- Measured by DOM script: `scrollWidth <= innerWidth`, control heights, title line count, text contrast, element positions; screenshots are shell `screencapture` of the window at 2×, cropped to the page body (the extension's "Claude is working here" badge sits over the page top)
- Nothing submitted, no stage changed, no sign-out; the Add event form was opened and cancelled; the Telegram page's account-id line was hidden in the DOM before capture; the account menu was never open in a capture
- One screenshot may serve several rows that share a view (same file content under each row's name)

## Rows

| # | Result | Measured | Screenshot |
|---|---|---|---|
| 1 | pass | phone dark and light: Day, Week, Month, Year, Agenda all visible, each 62×40, rightmost ends at 349 px of 390 | [dark](01-calendar-phone-dark.png), [light](01-calendar-phone-light.png) |
| 2 | pass | phone titles on one line (24 px high): day `Sat, Nov 21`, week `Nov 15 – 21`, month `November 2026`, year `2026`, agenda `From Nov 21` | [week](02-calendar-week-phone-dark.png), [agenda](02-calendar-agenda-phone-dark.png) |
| 3 | pass | phone: `scrollWidth` 390 = `innerWidth` 390 in day, week, month, year, agenda, dark and light; no inner horizontal scroller | [dark](03-calendar-phone-dark.png), [light](03-calendar-phone-light.png) |
| 4 | pass | phone month: cells show thin text-free bars and up to 3 dots; tapping the day number lists that day's events under the grid | [dark](04-calendar-phone-dark.png), [light](04-calendar-phone-light.png) |
| 5 | pass | phone: pills wrap to a second row (National 103, Central Asia 126, International 130 px wide, each 40 high), none cut | [dark](05-calendar-phone-dark.png), [light](05-calendar-phone-light.png) |
| 6 | fail | phone toolbar controls all 40 px (Today, Previous, Next, five views) and week list rows pass; but day-view all-day chips are 20 px high (3 chips), under the 28 px the spec asks; timed chips are 28 | [dark](06-calendar-phone-dark.png) (month); chips measured in the day view, see [09](09-calendar-day-phone-dark.png) |
| 7 | pass | phone year: 2 columns of 12 mini months, 0 day buttons; desktop keeps its day buttons (33×24) as decided | [phone](07-calendar-year-phone-dark.png), [desktop](07-calendar-year-desktop-dark.png) |
| 8 | pass | phone week: list of 7 days (`SUNDAY, NOV 15` ... ), 0 time labels, 0 inner scrollers | [dark](08-calendar-week-phone-dark.png) |
| 9 | pass | phone day: 3 all-day rows then `+1 more`, hour grid starts below the strip (strip ends 452, grid after), now-line at 06:00 inside the grid; desktop week: 3 all-day rows, `+1 more`, grid below | [phone](09-calendar-day-phone-dark.png), [desktop](09-calendar-week-desktop-dark.png) |
| 10 | fail | desktop day: `Park in Mall Market fair` (10:00, zero length) is now 24 px tall (30 min at 48 px/h) and shows its start time only, but the box is 22 px of content room for 34 px of content, so the `10:00 AM` line is cut in half at the bottom | [desktop](10-calendar-day-desktop-dark.png) |
| 11 | pass | desktop agenda: starts at Sat Nov 21; 4 running events marked `ONGOING`; no 1 Oct or 6 Oct rows; zero-length times show only `10:00 AM` | [desktop](11-calendar-agenda-desktop-dark.png) |
| 12 | pass | phone feed dark: no control under 40 px except the 1×1 skip link and the card title links (20 px, not in the review row); sort 40, Propose an event 40, `1 past to close` 40, card menu buttons 40×40, opened menu items 40 | [phone](12-feed-phone-dark.png) |
| 13 | pass | phone: after tapping Propose an event the form top is at 189 px of 797, `scrollY` 0, the Title input has focus | [phone](13-feed-phone-dark.png) |
| 14 | pass | phone and desktop, dark and light: `29 May – 14 Aug 2027` is not truncated; only the city (`Kranj (also Tržič, ...`) ellipsizes | [phone dark](14-feed-phone-dark.png), [phone light](14-feed-phone-light.png), [desktop dark](14-feed-desktop-dark.png), [desktop light](14-feed-desktop-light.png) |
| 15 | pass | phone: facet pills sit in two rows that scroll sideways (kit override, stays until the `ToggleGroup` wrap ships); toolbar is two full-width rows (Filters + sort, Propose + more); first card section at about 480 px instead of five facet rows | [phone](15-feed-phone-dark.png) |
| 16 | pass | desktop dark and light: search, Filters, sort, Propose an event and More actions are all 40 px high on one row | [dark](16-feed-desktop-dark.png), [light](16-feed-desktop-light.png) |
| 17 | pass | phone dark and light: the four bottom labels read `Feed`, `Calendar`, `Reminders`, `Telegram`, each one line (20 px), all items 56 px high | [dark](17-feed-phone-dark.png), [light](17-feed-phone-light.png) |
| 18 | pass | phone: `Sources updated 5 hours ago` shows inline under the page title, no icon-only control | [phone](18-feed-phone-dark.png) |
| 19 | pass | phone detail: `← All events` 80×40, `Download calendar` 126×40; the source link `afisha` is still 38×20 (not part of the spec, see new defects) | [phone](19-detail-phone-dark.png) |
| 20 | pass | phone dark and desktop light: `Download calendar` text starts at the same x as `WHEN` (16 px phone, 280 px desktop) | [phone](20-detail-phone-dark.png), [desktop](20-detail-desktop-light.png) |
| 21 | fail | desktop light: the gallery still shows 3 images where the first and third are the same photo (same file id `e1de191d...` at sizes `_l` and `_m`), visible in the capture | [desktop](21-detail-desktop-light.png) |
| 22 | pass | Languages reads `Russian`, no `ru-Cyrl`, phone and desktop | [desktop](22-detail-desktop-light.png) |
| 23 | pass | desktop light: WHEN at x 280, WHERE at x 684 (404 px apart instead of about 1150) | [desktop](23-detail-desktop-light.png) |
| 24 | pass | contrast: dark `← All events` 9.12, `SETTINGS` eyebrow 9.12, `WHEN` eyebrow 8.09; light 5.45, 5.32 (gradients ignored) | [detail](19-detail-phone-dark.png) / [telegram](24-telegram-phone-dark.png) |
| 25 | pass | phone and desktop: page title at the left edge (x 16 / 280), the empty state in a card under it at the same x, text left-aligned | [phone](25-reminders-phone-dark.png), [desktop](25-reminders-desktop-dark.png) |
| 26 | pass | phone: the 31 checkbox labels are 40 px high (inputs themselves 20×20 inside), Refresh, New digest, Save, Preview, Send now, Delete 40 px, text inputs 40 | [phone](26-telegram-phone-dark.png) |
| 27 | pass | phone: `Delete digest` left edge 33 = input left edge 33 (no 34 px indent); it still sits on its own row | [phone](27-telegram-phone-dark.png) |
| 28 | pass | prose reads "Recipients default to every member of the organization; a member who has not linked Telegram shows as not linked in the run history"; run history says `No Telegram link`; no `not_linked`; the digest shows once (1 heading); `role:member` remains only in the Recipients hint and box | [phone](28-telegram-phone-dark.png), [desktop](28-telegram-desktop-dark.png) |
| 29 | pass | desktop: `Runs on its schedule` top 445 = Name input top 445; `New since the last digest` row top 733 = the Starting and Deadline inputs top 733 | [desktop](29-telegram-desktop-dark.png) |
| 30 | pass | desktop add event: all labels 14 px weight 500, Date inputs carry the hint `Pick a precision first`, Kind select full width (1084 px) | [desktop](30-add-event-desktop-dark.png) |
| 31 | pass | phone add event: all 17 form controls 40 px high (min 40) | [phone](31-add-event-phone-dark.png) |
| 32 | pass | desktop not found: heading class `page-title`, sans face at 36 px, same as the other titles | [desktop](32-not-found-desktop-dark.png) |

## New defects seen

- Calendar month on a phone: the day-number button that selects a day is 22×16 px and the cell body is not tappable (a tap at a cell's empty area did nothing); low
- Calendar year on a phone: month-name buttons are 20 px high; low
- Event detail on a phone: source link `afisha` is 38×20 px; low (was in #19's "links 19-20 high", left out of the fix spec)
- Feed on a phone: the facet pills scroll sideways and `International` is cut at the right edge (kit override, expected to go with the `ToggleGroup` wrap); low
- Calendar desktop: toolbar and view buttons are 24 px high (not a phone issue); info
- Feed desktop: `Stage` buttons on cards and `1 past to close` are 32 px high; info
- Load: on the first fresh load the app sat on its splash for about 10 s once and a toast read the app list "could not be loaded", then it loaded; one earlier load ended on a `Bad Gateway` from the sign-in route; not reproduced on the next load; low

## T07 and T08 dev check

Read-only check on dev after fe `0.0.61`, fe-events `0.0.21`, Ocharo `53197f3`. Chrome profile `wow2`; DOM measurements by `getBoundingClientRect`. Limit: the phone window could not go below 626 pt, so the page viewport read 570 px, not 390 (the `wow2` profile's window minimum; an iframe at 390 px is refused cross-origin); heights are measured at 570 px in the phone layout (bottom bar, toolbar, list chips). Window restored to 1512×949.

| Item | Result | Measured | Screenshot |
|---|---|---|---|
| T08 1: dated tile, photo-less card | pass | `Artigiano in Fiera 2026`: day `5` 60 px, month `Dec`, tile 76×110, no icon (0 `svg`/`img` in all 43 tiles); `tile-year` slot is empty for 2026 and reads `2027` on 2027 events | [T08-01](T08-01-date-tile-dec.png) |
| T08 2: month-only tile | pass | `TWIG Christmas Bazaar 2026`: day `?` at opacity 0.5, month `Dec`, tile 76×110, same height and month position as the dated tile, no icon | [T08-02](T08-02-date-tile-month-only.png) |
| T07 3: day view all-day chips | pass | 4 all-day chips, each 28 px high (222-225 wide, two rows); timed chips 28 px; toolbar buttons 40 px | [T07-01](T07-01-calendar-day-all-day-chips.png) |
| T07 4: month view day cells | pass | 42 day cells, each one `button` 75×96 px (min 96); `scrollWidth` 570 = `innerWidth` 570 | [T07-02](T07-02-calendar-month-cells.png) |

- Undated or year-only cards: no `TBA` text anywhere in the feed; the 43 tiles all carry a month, many show `?` (month-only), none shows `?` with no month
- Seen, not in the items: in the month view the event bars sit over the top of the day numbers (numbers half covered in the capture)
