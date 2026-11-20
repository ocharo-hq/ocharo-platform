# Events

*Last updated: 2026-11-20*

Ocharo Events is one feed of every event where Ocharo could show its products — national fairs and fashion weeks in
Uzbekistan, weeks across Central Asia, and international fairs and competitions. It answers three questions:
what is coming, what must be decided before a deadline, and where Ocharo stands with each event. WoW2 Events holds the
events and Ocharo's tracking; the app renders them with WoW2 Events' own screens (OE07).
[Contract](../../engineering/architecture/events-contract.md).

| Capability | Acceptance |
|---|---|
| Feed | Events grouped by month; undated events kept visible; past events hidden unless asked for; more load a page at a time, with no cap |
| Filters | Scope chips (National · Central Asia · International, derived from the country: Uzbekistan; Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan; every other country; an event with no country reads "Scope unknown" and no chip selects it), stage with counts, kind and free-text search, all applied by WoW2 Events |
| Deadlines first | The five nearest application deadlines lead the page with days left; skipped, rejected and cancelled events never appear there |
| Honest dates | Exact, month-only, expected-from-earlier-editions and not-announced dates read differently; a start time names its zone only when it differs from the reader's |
| Pipeline | Candidate → shortlisted → applying → applied → confirmed → attended; skipped and rejected kept as record, out of the feed until "Show Skipped, Rejected" is on or the stage filter names them, still counted |
| Close-out | An ended event Ocharo was still pursuing (shortlisted, applying, applied or confirmed) waits under "Past, not closed" until one click records it as Attended or Skipped; an ended event Ocharo never tracked asks nothing |
| Calendar | The calendar place draws each event with a known day as a block, coloured by Ocharo's stage; three scope pills above it (National · Central Asia · International, several at once) show only the events of the chosen scopes, each with the number of events in the loaded window, and none chosen shows every event, those with no country included; the choice stays in the address (`?scope=national,central-asia`) |
| Review | Found events arrive untracked ("New — review"); an editor shortlists or skips each one in one click; an event an editor adds is published at once and shows in the feed with no review step |
| Event | Facts as the source gave them (ticket price apart from Ocharo's own cost, address, organizer site, each source by name with its listing state), the title in the reader's language with a language badge otherwise, a calendar file, and the tracking form with its history |

Unknown cost stays unknown, never zero. Ocharo Events never applies, registers or pays on Ocharo's behalf.
Later slices: linking events to the products or catalogue edition shown, and Marketing campaigns attached to an event.
