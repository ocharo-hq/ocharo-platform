# OcharoEvents.Infrastructure

*Last updated: 2026-11-19*

Command and query handlers, the event service, validation, and the events repository over WoW2 Events' brokers
(`EventsServiceRepository`, `EventsServiceMapper`): reads are the organization's tracking list of the catalog, a new
event is a member proposal that Events publishes at once in the organization's private catalog, tracking writes pass, fact edits and deletes answer 410 Gone. `Delivery/` serves the
organization's Telegram delivery from Events' digests of the same catalog (`EventDigestsService`).
