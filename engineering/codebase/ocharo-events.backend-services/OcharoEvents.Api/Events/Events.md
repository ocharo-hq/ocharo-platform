# Events

*Last updated: 2026-11-17*

The organization's showcase events under `api/organizations/{organizationId}/events`, Desk's routes and wire (events-app.md #3):
list, `summary`, propose, read, and save tracking by `PUT {eventId}/tracking` (stage, priority, participation, cost and
notes; it never compares the event's facts, so a source revision after the page read cannot refuse it). The full
`PUT {eventId}` answers 410 for a fact edit, since facts change through WoW2 Events' review, and 409 when the page's
`eventRevision` no longer matches; a delete answers 410.
A title comes back in the language `Accept-Language` names (`HttpUiLocale`, matched by language prefix), with the tag it came
under as `titleLanguage` and the source's `languages`.
Events' event status and attendance ride as `eventStatus` and `attendance`, apart from Ocharo's own stage `status`; a
cancelled event's application deadline is not an upcoming one in the summary.
What the source says attending costs rides as `tickets` and `ticketUrl`; `cost` stays Ocharo's own cost of taking part.
A start announced to the minute rides as `startsAt` (local `HH:mm`) beside `startsOn`, with the source's IANA `timeZone`;
the venue's street address rides as `address` and the organizer's site as `organizerWebsite`.
