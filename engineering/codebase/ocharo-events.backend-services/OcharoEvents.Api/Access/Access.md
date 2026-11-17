# Access

*Last updated: 2026-11-17*

Organization access (events-app.md #4): the session's claims, the filter that admits a route's organization only to a
member holding `events:read` there (anything else answers 404, unauthenticated 401), and the problem writer middleware
uses. The resolved access also says whether the person holds `events:track` (propose and track) and `events:admin`
(the organization's Telegram delivery).
