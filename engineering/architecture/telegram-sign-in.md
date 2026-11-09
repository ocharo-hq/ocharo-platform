# Telegram bot sign-in

*Last updated: 2026-11-09*

Desk's own Telegram sign-in (a linked bot chat, a six-digit code, Desk cookie sessions) retired on 2026-11-02 with the
Identity cutover. Ocharo people now sign in by phone at Ocharo's WoW2 Identity instance, which drives the bot and sends
the code in Telegram; Desk signs in through the SDK's BFF.

- Bots, tokens and environments: [Telegram bots](telegram-bots.md).
- Sign-in flow, invites and rollout: [identity cutover](../research/identity-cutover.md).
- Migration `013-identity-cutover` dropped Desk's `telegram_*` and `sso_*` tables with their rows (2026-11-09); the
  `011` reserved for it stays a gap, because deployed databases already held `012-files`.
