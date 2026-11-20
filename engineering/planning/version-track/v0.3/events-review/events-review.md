# Events app review: desktop and phone, dev

*Last updated: 2026-11-21*

> Read-only review of every page of Ocharo's Events app on dev, at desktop (1440 px viewport) and phone (390 px viewport), in dark and light themes, input for the next spec that fixes the whole Events app on desktop and phone (developer, 2026-11-21). Nothing was changed, submitted or signed out; the Add event form was opened and cancelled.

## Summary

- 32 defects: 1 high, 9 medium, 22 low
- High: on a phone the calendar's view switcher is clipped, so Year and Agenda cannot be reached (#1)
- Medium cluster, calendar on phone: wrapped period titles, a 10 px horizontal scroll, event chips cut to 3 characters, tap targets 14-28 px (#2-#4, #6)
- Medium cluster, calendar views: all-day chips spill over the first hour rows, zero-length events draw as slivers (#9, #10)
- Medium cluster, feed on phone: tap targets under 40 px, the Add event form opens below the fold, card dates truncated (#12-#14)
- Not found: no console errors or warnings, no broken images, no failed requests, no non-English interface strings (details under Method and limits)

## Defects

Screenshots sit beside this file. Sources: a `file:line` in `apps/web/src/` of the Events frontend (`ocharo-events.frontend-services`); `kit` is `@wow-two-beta/fe-events` (its compiled `dist/kit`); `fe sdk` is `@wow-two-beta/fe` (its compiled `dist/presentation`); both ship compiled, so a kit or fe sdk row names the component, not a line.

| # | Page | Width | Theme | Defect | Severity | Screenshot | Likely source |
|---|---|---|---|---|---|---|---|
| 1 | Calendar, every view | phone | dark, light | View switcher clipped: 460 px of controls in a 346 px frame with `overflow-hidden`, so only Day, Week and a cut "Month" show; Year and Agenda cannot be reached | high | [08](08-calendar-toolbar-clipped-phone-dark.png) | fe sdk `EventCalendarViewer`, mounted at `CalendarPage.vue:69` |
| 2 | Calendar day, week, agenda | phone | dark | Period title wraps to 3-5 short lines beside the switcher (`Nov / 15 – / Nov / 21, / 2026`) | medium | [10](10-calendar-week-phone-dark.png), [09](09-calendar-day-phone-dark.png), [12](12-calendar-agenda-phone-dark.png) | fe sdk `EventCalendarViewer` |
| 3 | Calendar, every view | phone | dark, light | The page scrolls sideways by about 10 px (`main` 390 px of content in 380 px; 398 px in Year): a grey scrollbar sits above the bottom bar | medium | [08](08-calendar-toolbar-clipped-phone-dark.png) | fe sdk `EventCalendarViewer`; frame `AppLayout.vue` |
| 4 | Calendar month | phone | dark, light | Event chips cut to 3-4 letters (`Sou…`, `GN…`, `Arti…`, `Visa Fashion …`); `+1 more` breaks onto two lines | medium | [08](08-calendar-toolbar-clipped-phone-dark.png), [20](20-calendar-phone-light.png) | fe sdk `EventCalendarViewer` |
| 5 | Calendar | phone | dark, light | Scope pills overflow: `International 2` is cut at the right edge | low | [08](08-calendar-toolbar-clipped-phone-dark.png) | fe sdk `ToggleGroup`, `CalendarPage.vue:53` |
| 6 | Calendar | phone | dark | Tap targets far under 40 px: Today 28 px, Previous and Next 14×28, Day/Week/Month/Year/Agenda 24 px high, chips 19-20 px | medium | [08](08-calendar-toolbar-clipped-phone-dark.png) | fe sdk `EventCalendarViewer` |
| 7 | Calendar year | phone, desktop | dark | 365 day buttons are 39-41×24 px; month names 20 px high | low | [11](11-calendar-year-phone-dark.png) | fe sdk `EventCalendarViewer` |
| 8 | Calendar week | phone | dark | Only 3 of 7 day columns fit; the rest sit behind an inner horizontal scroll, chips read `Zurab Tse` | low | [10](10-calendar-week-phone-dark.png) | fe sdk `EventCalendarViewer` |
| 9 | Calendar day (phone), week (desktop) | phone, desktop | dark | All-day chips overflow the all-day strip into the 00:00 and 01:00 hour rows; the now-line strikes through a chip's text | medium | [09](09-calendar-day-phone-dark.png), [28](28-calendar-week-desktop-dark.png) | fe sdk `EventCalendarViewer` |
| 10 | Calendar day, week | desktop, phone | dark | A timed event with the same start and end (`Park in Mall Market fair`, 10:00) draws as a 12 px sliver with its text cut in half | medium | [29](29-calendar-day-desktop-dark.png) | fe sdk `EventCalendarViewer` |
| 11 | Calendar agenda | desktop | dark | Titled `Upcoming from Nov 21` yet lists 1 Oct and 6 Oct events first; zero-length times read `10:00 AM – 10:00 AM` | low | [31](31-calendar-agenda-desktop-dark.png) | fe sdk `EventCalendarViewer` (`AgendaView`) |
| 12 | Feed | phone | dark | Tap targets under 40 px: 51 card menu buttons 32×32, sort select 36, Propose an event 32, `1 past to close` 30, menu items 32 | medium | [01](01-feed-phone-dark.png) | kit `EventsFeed`, `EventMediaCard`, mounted at `EventsPage.vue:100` |
| 13 | Feed | phone | dark | Propose an event opens its form below the fold (form top at 655 px of a 723 px view) with no scroll or focus move, so the tap looks like it did nothing | medium | [06](06-feed-propose-opened-below-fold-phone-dark.png) | kit `EventsFeed` proposal form (`EventsPage.vue:100`) |
| 14 | Feed cards | phone, desktop | dark, light | Date range truncated while the city keeps its room: `29 May – 14 Au…` beside `Kranj (also Tržič, Škofja Loka, …` (same overflow measured at 1440 px) | medium | [02](02-feed-card-meta-phone-dark.png) | kit `EventMediaCard` |
| 15 | Feed | phone | dark | Toolbar wraps raggedly (Filters and sort on one row, Propose an event and `…` right-aligned on the next); the facet bar takes five rows (about 650 px) before the first card | low | [01](01-feed-phone-dark.png) | kit `EventsFeed` |
| 16 | Feed | desktop | dark, light | Toolbar controls have four heights: search 40, Filters 40, sort 36, Propose an event 32 | low | [25](25-feed-desktop-dark.png) | kit `EventsFeed` |
| 17 | Every page | phone | dark, light | Bottom bar label `Telegram delivery` wraps to two lines, so its icon sits higher than the other three | low | [01](01-feed-phone-dark.png) | `PhoneNav.vue:21` over fe sdk `BottomNavMenu` |
| 18 | Feed | phone | dark, light | The sources-freshness clock beside the title is a 20 px icon whose text is only a tooltip; a tap shows nothing, so on a phone the freshness text is unreachable | low | [01](01-feed-phone-dark.png) | `SourcesFreshness.vue:56`, used at `EventsPage.vue:96` |
| 19 | Event detail | phone | dark | Tap targets: `← All events` 80×20, `Download calendar` 32 high, links 19-20 high | low | [03](03-detail-phone-dark.png) | `EventsPage.vue:139`; kit `EventDetail` |
| 20 | Event detail | phone, desktop | dark, light | `Download calendar` text starts about 13 px right of the labels above it | low | [03](03-detail-phone-dark.png), [23](23-detail-desktop-light.png) | kit `EventDetail` |
| 21 | Event detail | desktop | light | The gallery repeats one photo: first and third images are identical | low | [23](23-detail-desktop-light.png) | kit `EventDetail` or the event's image data: unknown |
| 22 | Event detail | desktop, phone | light | Languages shows the raw tag `ru-Cyrl` | low | [23](23-detail-desktop-light.png) | kit `EventDetail` |
| 23 | Event detail | desktop | light | WHEN and WHERE sit in two columns about 1150 px apart with a wide gap between them | low | [23](23-detail-desktop-light.png) | kit `EventDetail` |
| 24 | Event detail, Telegram delivery | phone | dark | Small pink text under 4.5:1 on the dark page: `← All events` 3.96, `SETTINGS` eyebrow 3.96 | low | [03](03-detail-phone-dark.png), [14](14-telegram-delivery-phone-dark.png) | primary colour token: unknown |
| 25 | Reminders | phone, desktop | dark | The page is only an empty state (`Reminder settings are on their way`), its heading left and its message centred in a blank page | low | [13](13-reminders-phone-dark.png), [32](32-reminders-desktop-dark.png) | `RemindersPage.vue:19` |
| 26 | Telegram delivery | phone | dark | Tap targets: 31 checkboxes 13×13, Refresh and New digest 32 high, inputs 38 | low | [15](15-telegram-delivery-form-phone-dark.png) | kit `DigestsPanel`, `TelegramDeliveryPage.vue:43` |
| 27 | Telegram delivery | phone | dark | `Delete digest` sits on its own row, indented about 34 px from the form's left edge | low | [15](15-telegram-delivery-form-phone-dark.png) | kit `DigestsPanel` |
| 28 | Telegram delivery | phone, desktop | dark | The digest shows twice (a list row and the editor card, same name and next run); raw `role:member` and `not_linked` tokens in prose and run history | low | [14](14-telegram-delivery-phone-dark.png), [33](33-telegram-delivery-desktop-dark.png) | kit `DigestsPanel` |
| 29 | Telegram delivery | desktop | dark | Form rows stagger: `Runs on its schedule` sits at the top beside the Name input, and the `New since the last digest` checkbox is not aligned with the Starting and Deadline inputs | low | [33](33-telegram-delivery-desktop-dark.png) | kit `DigestsPanel` |
| 30 | Add event | desktop | dark | Label sizes differ (Precision, Date, Certainty small and grey, Venue, City, Country bold); Date inputs are empty boxes with no hint while precision is `Not announced`; Kind select is half width | low | [36](36-add-event-desktop-dark.png) | kit `EventsFeed` proposal form |
| 31 | Add event | phone | dark | Form fields are 38-39 px high, just under 40 | low | [04](04-add-event-phone-dark.png) | kit `EventsFeed` proposal form |
| 32 | Page not found | desktop | dark | The heading uses a serif face (`font-editorial`), unlike every other page title | low | [38](38-not-found-desktop-dark.png) | `NotFoundPage.vue:19` |

Counts by severity: high 1 (#1), medium 9 (#2, #3, #4, #6, #9, #10, #12, #13, #14), low 22.

## Overviews, one per page per width

| Page | Phone dark | Phone light | Desktop dark | Desktop light |
|---|---|---|---|---|
| Feed | [01](01-feed-phone-dark.png), [17](17-feed-past-to-close-phone-dark.png) | [18](18-feed-phone-light.png) | [25](25-feed-desktop-dark.png), [26](26-feed-cards-desktop-dark.png) | [22](22-feed-desktop-light.png) |
| Feed, past to close strip | [17](17-feed-past-to-close-phone-dark.png) | | [39](39-feed-past-to-close-desktop-dark.png) | |
| Feed, filters, card menu, empty search | | | [35](35-feed-filters-desktop-dark.png), [40](40-feed-card-menu-desktop-dark.png), [37](37-feed-empty-search-desktop-dark.png) | |
| Event detail | [03](03-detail-phone-dark.png) | [19](19-detail-phone-light.png) | [24](24-detail-desktop-dark.png) | [23](23-detail-desktop-light.png) |
| Add event form | [04](04-add-event-phone-dark.png), [05](05-add-event-form-end-phone-dark.png) | | [36](36-add-event-desktop-dark.png) | |
| Calendar, month | [07](07-calendar-phone-dark.png) | [20](20-calendar-phone-light.png) | [27](27-calendar-desktop-dark.png) | [21](21-calendar-desktop-light.png) |
| Calendar, week | [10](10-calendar-week-phone-dark.png) | | [28](28-calendar-week-desktop-dark.png) | |
| Calendar, day | [09](09-calendar-day-phone-dark.png) | | [29](29-calendar-day-desktop-dark.png) | |
| Calendar, year | [11](11-calendar-year-phone-dark.png) | | [30](30-calendar-year-desktop-dark.png) | |
| Calendar, agenda | [12](12-calendar-agenda-phone-dark.png) | | [31](31-calendar-agenda-desktop-dark.png) | |
| Reminders | [13](13-reminders-phone-dark.png) | | [32](32-reminders-desktop-dark.png) | |
| Telegram delivery | [14](14-telegram-delivery-phone-dark.png), [15](15-telegram-delivery-form-phone-dark.png) | | [33](33-telegram-delivery-desktop-dark.png) | |
| Sidebar (desktop), phone nav | phone nav in every phone shot | | [34](34-sidebar-expanded-desktop-dark.png) (expanded), the rail in every desktop shot | |
| Not found | | | [38](38-not-found-desktop-dark.png) | |

Number 16 is not used: that capture showed the account's phone number and was deleted.

Light and dark were both checked on feed, detail and calendar at both widths; the other pages only in dark.

## Pages and widths checked with no defect

- Feed filters popover, phone and desktop: opens beside its button, fits the viewport, every field reachable
- Feed card menu (Shortlisted, Skipped), phone and desktop: opens on the card, fits; only the 32 px rows counted in #12 (nothing was clicked)
- Feed past-to-close strip, phone and desktop: readable, no overflow (its Attended and Skipped buttons are 32 px, counted under #12's class of tap target; never clicked)
- Feed empty search, desktop: `No events match these filters.` with `Clear all` available
- Feed desktop light and dark at 1440: no overflow, truncation only as in #14
- Calendar month, year and agenda at desktop, light and dark: no overflow, switcher fully visible
- Calendar desktop pills (National, Central Asia, International): fit, counts update per view
- Event detail desktop dark: no overflow; only the low items under #20-#23
- Sidebar at desktop, collapsed rail and expanded: labels, version and freshness line fit; icon rail has no visible labels
- Account menu at phone (System, Light, Dark, Sign out): fits the viewport; rows are 32 px (#12's class of tap target); not clicked beyond the appearance choice
- Telegram delivery, desktop: no overflow at 1440
- Feed load-more: none reachable, all 65 events render (65 of 65), so no load-more control shows
- Deadlines: no separate page; deadlines appear as the feed's attention pill, the detail's Deadlines section and the filter `Deadline within 30 days 0`; the kit's own deadline pill is hidden at zero today, so only `1 past to close` could be checked

## Method and limits

- Phone route: a real Chrome window of the `mft-ven` profile resized so the page viewport is exactly 390×902 (window 630 pt wide plus the 240 pt vertical-tab strip); `resize_window` to 390 gave 386 px, so the window was set 4 pt wider. Not device emulation: no touch, `hover: none` and `pointer: coarse` are off, so touch-only CSS was not exercised
- Desktop route: the window set to 1680 pt wide, a 1440×846 viewport, wider than the 1512 pt screen and captured whole
- Windows were driven through Apple Events page JavaScript in a window opened for the review, because the Claude in Chrome selection was flipped to another profile on most calls; Claude in Chrome was used for the first resize, the console reads and some page loads. The shared window and its size were restored (1512×949) and the review window closed
- Screenshots: shell `screencapture` of the window at 2×, cropped to the page body (phone 780 px wide, desktop 2880 px wide), cursor parked outside the window
- Measured with DOM scripts: horizontal overflow, elements outside the viewport, interactive elements under 40 px, truncated text, broken images, text contrast (approximate: gradients and photos ignored, so text over card photos was not judged), Cyrillic and Uzbek text
- Console: no messages of any level on fresh loads of feed, calendar, reminders, Telegram delivery and event detail; no request answered 4xx or 5xx on any page
- Images: 29 images on the feed and detail loaded, none broken
- Theme: set through the account menu for the review and put back to System; sidebar width restored to the collapsed rail
- Content, not defects: the venue `Галерея изобразительного искусства (NBU)` and address on one event, the digest name `Ocharo tadbirlari` and the digest Language choices `O‘zbekcha`, `Русский`, `English` are authored content or language names; no Uzbek or Russian interface string was found in the app's own chrome
- Not exercised: changing the sort, `Show Skipped, Rejected`, any stage change, submitting a form, sign-out, the boot splash and loading states (the app loaded too fast to catch one)
