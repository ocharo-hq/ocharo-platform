# Application

_Last updated: 2026-11-22_

Orchestration hooks: the Events page's `fe-events` client (the workflow with Ocharo's labels, and each event the page reads with
returned as Events sent it), its feed-or-event navigation (held while the session signs out, keeping the address's query across the move so the feed's filters survive an event; the page has no form, so no unsaved-edit guard), the
person's current day, and the workflow read the event's tracking form gets (the stored one with Ocharo's labels filling
what it lacks; a visit never writes it; the detail shows no tracking form for now), the past events still to close, their count behind the feed's "past to close" pill and the one
tracking write that closes each as Attended or Skipped (`events/UsePastToClose`, OE12), the calendar's
state (its view and focused day, the organization's events for the days shown, read through the feed's client and paged
by cursor up to 1000, and the filters (region, kind, stage, city, source and a search text) narrowing the events in the browser, with the filter card's facet counts over the window read and its "n of m"; `events/UseEventsCalendar`) kept in the address under the kit's keys (`?regions=…&kinds=…&stages=…&cities=…&sources=…&q=…`, a repeated key for a list) by `events/UseAddressFilters`, so one address means the same filters on both pages; `bindAddressFilters` keeps any page's filters in its address, which both pages use (the feed's are the kit's `useEventsFilters` state, written with its `toRouteQuery` and read with its `fromRouteQuery`): they open on the query, follow it when it changes (the back button, a link), and a choice is written after it, replacing the entry in place so the back button takes no step per click and leaving the query's other keys; it names no page; and the check that an address's organization is the session's (`events/`); the sections the session is
offered, addressed for its organization (`events/UseEventsSections`), the Feed's and the Calendar's links carrying the filter keys of the open address (`q`, `stages`, `kinds`, `cities`, `sources`, `regions`) and no other key, so a filter set on one page is there on the other; the Ocharo session, feedback, the interface language (English only, whatever is stored or the browser prefers, until the SDKs'
translation layer improves; `shared/UseUiLocale`), the app menu's addresses and what the Telegram delivery page hands the digest editor (`delivery/`).
