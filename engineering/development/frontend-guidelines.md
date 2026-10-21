# Frontend development

*Last updated: 2026-10-21*

Follow the [frontend standard](../../../../../../../../../conventions/development/repo/repo-conventions.md#frontend-standard):
Vue 3, strict TypeScript, Vite, Tailwind v4, and reusable capabilities from `@wow-two-beta/fe`.
No React exception is requested or recorded.

Use the SDK's public exports and a verified published package or explicitly reproducible local pack.
Do not bind the application to a sibling repository's mutable source path. The template's React
starter must be replaced before reporting the scaffold complete.

The API serves the built frontend from generated `wwwroot/`; development uses the shared proxy
and port conventions. Preserve draft conflicts and display delivery uncertainty explicitly.

The Ocharo frame's glass is the SDK `ocharo` theme's: `variant="glass"` on the top bar and sidebar,
`variant="glass-menu"` on the account, apps and phone menus, `surface-glass!` on the phone bar (the SDK
`BottomNavMenu` paints `bg-background` and takes no glass variant yet). Desk keeps
no glass copy. App names translate through `ShellUiMessages.ts`; Editorial has its own
`Apps menu: Editorial` key (with an `en` entry), apart from the designer's Editorial style.
