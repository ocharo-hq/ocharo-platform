# Domain

_Last updated: 2026-11-20_

Framework-free models and copy: Ocharo's configuration of the `fe-events` kit (`events/Events`: the catalog, the eight
stages with their labels and classes, the workflow's custom fields (the spec `deployment/events/ocharo-workflow.json`
mirrors), the labels shown for a stored workflow, kinds, scopes, how a new event is proposed; the scopes are National (UZ), Central Asia (KZ, KG, TJ, TM) and International (every other country), shared by the feed's chips and the calendar's pills) and the
page's addresses, how Events' intake freshness reads: stale past 24 hours, a warning when stale or a source fails
(`events/IntakeFreshness`), and the calendar's model (`events/EventsCalendar`: the days one visible range needs, and how an
event with a known day becomes an all-day or timed block, coloured by stage from theme variables, an event not yet tracked a visible quiet tint; an event known only to its month or year has
no day and is left out; and the scope pills' model: an event's scope from its country by the feed's own rule (none for an event with no country), the events kept by the chosen scopes, the count the window draws per scope, and the `?scope=` query's reading and writing); Events' sections (`shared/EventsSections`: the feed, the calendar, the reminders and the admins'
Telegram delivery, their groups, addresses and route metadata); the Telegram delivery's default digest (it leaves out
skipped and rejected events) and event links (`delivery/`), the apps menu, and the interface messages (English shown; the uz and ru texts stay in the tables for when the language choice returns).
