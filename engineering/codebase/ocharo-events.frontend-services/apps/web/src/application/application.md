# Application

_Last updated: 2026-11-20_

Orchestration hooks: the Events page's `fe-events` client (the workflow with Ocharo's labels, and each event the page reads with
returned as Events sent it), its feed-or-event navigation (held while the session signs out; the page has no form, so no unsaved-edit guard), the
person's current day, and the workflow read the event's tracking form gets (the stored one with Ocharo's labels filling
what it lacks; a visit never writes it; the detail shows no tracking form for now), the past events still to close, their count behind the feed's "past to close" pill and the one
tracking write that closes each as Attended or Skipped (`events/UsePastToClose`, OE12), the calendar's
state (its view and focused day, the organization's events for the days shown, read through the feed's client and paged
by cursor up to 1000, and the chosen scopes: kept in the address's `?scope=` query, followed when it changes, narrowing the events in the browser, with a count per scope for the pills) and the check that an address's organization is the session's (`events/`); the sections the session is
offered, addressed for its organization (`events/UseEventsSections`); the Ocharo session, feedback, the interface language (English only, whatever is stored or the browser prefers, until the SDKs'
translation layer improves; `shared/UseUiLocale`), the app menu's addresses and what the Telegram delivery page hands the digest editor (`delivery/`).
