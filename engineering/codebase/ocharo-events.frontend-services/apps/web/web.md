# Web

*Last updated: 2026-11-22*

Events' Vue 3 operator app on the shared `AppFrame`, with its own side navigation like Brand's: the feed (`fe-events` 0.0.14's v2: facet bar with server counts, sort, cards that load on scroll, attention pills), the calendar, and Settings with the reminders and, for an admin, the Telegram delivery (a bottom bar of the same places on a phone). Sources in `src/`, unit tests in `tests/`, stable-name files (icons, splash, first-paint styles) in `public/`, and development-only helpers in `dev/` (`pnpm dev:replay` serves `/api` from a stored snapshot, no backend: see [dev](dev/dev.md)).
