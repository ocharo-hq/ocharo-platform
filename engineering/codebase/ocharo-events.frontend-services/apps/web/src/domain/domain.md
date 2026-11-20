# Domain

_Last updated: 2026-11-20_

Framework-free models and copy: Ocharo's configuration of the `fe-events` kit (`events/Events`: the catalog, the eight
stages with their labels and classes, the workflow's custom fields (the spec `deployment/events/ocharo-workflow.json`
mirrors), the labels shown for a stored workflow, kinds, scopes, the fields shown, how a new event is proposed) and the
page's addresses, how Events' intake freshness reads: stale past 24 hours, a warning when stale or a source fails
(`events/IntakeFreshness`), and the calendar's model (`events/EventsCalendar`: the days one visible range needs, and how an
event with a known day becomes an all-day or timed block, coloured by stage; an event known only to its month or year has
no day and is left out); Events' sections (`shared/EventsSections`: the feed, the calendar, the reminders and the admins'
Telegram delivery, their groups, addresses and route metadata); the Telegram delivery's default digest (it leaves out
skipped and rejected events) and event links (`delivery/`), the apps menu, and the uz, ru and en interface messages.
