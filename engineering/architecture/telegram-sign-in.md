# Telegram bot sign-in

*Last updated: 2026-10-09*

## Decision

Ocharo Desk uses the existing Telegram Bot API pattern from Haven: the user links a private bot chat, then requests a six-digit code by phone number. Email/password remains available. Telegram Gateway and Telegram OIDC are not part of this implementation.

Haven's `CodeSendCommandHandler`, `SignInCommandHandler`, and `TelegramMessageCommandHandler` were inspected as the product precedent. Desk reuses the published SDK's OTP generation, verification, and Telegram delivery, while retaining Desk's durable cookie sessions and organization membership checks.

The bot cannot discover a chat from an arbitrary phone number. A user must first open the bot and explicitly share their own contact. Desk also requires existing-account ownership before accepting that contact; a matching phone alone never claims an operator account.

## First link

1. Sign in to the existing Ocharo account with email/password.
2. Choose Link Telegram and re-enter the current password.
3. Open the returned bot URL in the intended Telegram account.
4. Press the bot's Share my phone number button in the private chat.
5. Refresh the account settings; the linked number appears after the server confirms it.

The link URL contains a random 256-bit secret. PostgreSQL stores only its SHA-256 hash, the initiating user/session, a ten-minute expiry, and the eventual private-chat ID. Completion rechecks that initiating session and security stamp. The sender, private chat, and contact owner must have the same Telegram ID. Each link can complete once; one operator, one Telegram ID, and one phone are unique independently.

Forwarded contact cards, groups, expired/revoked app sessions, conflicting links, and unsolicited contact messages cannot link an account. New users still require the existing provisioning/invitation process; bot interaction creates no operator or membership.

## Sign-in

- Request a code using the linked international phone number.
- The bot sends the code to the stored private chat, never a chat supplied by the browser.
- Enter the code in Ocharo. Verification creates the existing eight-hour Desk session.
- The existing session cookie, session-bound CSRF token, revocation checks, and organization authorization remain authoritative.
- Unknown, unlinked, inactive, locked, and unsupported two-factor accounts do not receive a code. The request returns the same accepted response shape to avoid account enumeration.
- Code delivery failure consumes the pending code; the browser receives no provider exception or account details.

Codes expire in five minutes, allow five failed attempts, and can be requested at most once per thirty seconds per phone. Authentication routes additionally allow twelve requests per minute per source address. Reverse proxies must preserve a trustworthy client address before relying on this limiter.

OTP records are protected by ASP.NET Data Protection, never logged or returned to the browser. The database repository holds a transaction-scoped advisory lock across SDK verification and durable session insertion. Concurrent valid submissions produce one session; a consumed code cannot replay. The code table retains no expired records beyond the following successful code creation's one-day cleanup window.

## Change or remove the linked identity

Unlink requires the current password and CSRF token. It removes the link, cancels pending link/code requests, rotates the user security stamp, and revokes every Desk session for that user. Link a replacement only after signing in again. Phone reassignment and Telegram account changes never automatically transfer the link.

## API

All routes are under `/api/auth/telegram`. Application requests use the existing `X-CSRF-TOKEN` header for mutations. Every failure is Problem Details with a stable `code`.

| Method and route | Request | Success |
|---|---|---|
| `GET /api/auth/telegram` | None; anonymous allowed | `{data:{enabled,botUsername,linked,phone}}`; linked phone only for the authenticated operator |
| `POST /link` | `{password}`; authenticated | `{data:{botUrl,expiresAt}}` |
| `DELETE /link` | `{password}`; authenticated | `204`; current cookie removed and all operator sessions revoked |
| `POST /code` | `{phone}`; anonymous allowed | `202 {data:{expiresAt}}`; generic accepted response |
| `POST /login` | `{phone,code}`; anonymous allowed | `{data:{authenticated:true}}` plus normal Desk session cookie |
| `POST /updates` | Telegram update; secret header | `200`; no browser-CSRF exemption elsewhere |

Error codes: `telegram_unavailable` (404), `invalid_phone` (400), `invalid_credentials` (401), `telegram_already_linked` (409), `telegram_code_invalid` (401), `telegram_rate_limited` (429), `telegram_webhook_unauthorized` (401), `telegram_update_invalid` (400), `telegram_update_too_large` (413). Expired, consumed, incorrect, and exhausted codes share `telegram_code_invalid`.

## Configuration and local activation

The feature is disabled by default. No bot client is contacted until the feature is explicitly enabled and an authorized operation or Development polling runs. No code prints the bot token or code. Startup never registers or deletes a Telegram webhook.

| Explicit environment alias | Host key | Value |
|---|---|---|
| `TELEGRAM_AUTH_ENABLED` | `Identity:Telegram:Enabled` | `true` to activate |
| `TELEGRAM_BOT_USERNAME` | `Identity:Telegram:BotUsername` | `ocharo_bot`, without `@` |
| `TELEGRAM_BOT_TOKEN` | `Identity:Telegram:BotToken` | Server-only BotFather token |
| `TELEGRAM_WEBHOOK_SECRET` | `Identity:Telegram:WebhookSecret` | Independent random 32–256 character ASCII secret: letters, numbers, `_`, `-` |
| `TELEGRAM_POLLING_ENABLED` | `Identity:Telegram:PollingEnabled` | `true` only for explicit local Development polling |

Store secrets through the established private server configuration, never chat, source, committed settings, browser storage, or frontend environment. Apply migration `009-telegram-access` through the existing migration command before enabling the feature. Back up and preserve PostgreSQL and Data Protection keys.

For localhost review, explicitly enable Development polling. It requires no public tunnel and does not register a webhook. The API webhook route rejects requests in polling mode. Telegram itself rejects `getUpdates` when that bot already has a webhook; resolve that configuration deliberately instead of silently deleting it. Run one polling consumer per bot.

For deployment, turn polling off and explicitly register the public HTTPS URL ending `/api/auth/telegram/updates` with Telegram, including the independent `secret_token`. Localhost cannot receive Telegram's internet webhook. The handler accepts private human messages only and enforces a 64 KiB body limit. Live registration and bot-token activation remain operator actions, separate from local implementation verification.

## Verification

The initial focused run passed 25 tests: 15 Telegram cases plus 10 existing password/session contracts, using a disposable PostgreSQL database and fake Telegram HTTP transport. Coverage includes disabled behavior, CSRF/password linking, private/own contact, challenge expiry/revocation, takeover prevention, encrypted code storage, attempts/expiry/throttling, failed delivery, concurrent consumption, lockout/inactive/two-factor restrictions, unlink revocation, and retained password login.

Live bot credentials, real delivery, and manual account linking have not been exercised by this implementation run. Cross-app SSO uses the Desk authority contract documented separately; Telegram authentication alone does not authenticate the preserved loopback Catalogue/Brand APIs.

## Sources

- [Telegram bot deep links](https://core.telegram.org/bots/features#deep-linking): private-chat start parameters and token length/character constraints.
- [Telegram contact-request buttons](https://core.telegram.org/bots/api#keyboardbutton): deliberate contact sharing in private chats.
- [Telegram webhook](https://core.telegram.org/bots/api#setwebhook) and [long polling](https://core.telegram.org/bots/api#getupdates): authentication and mutually exclusive update delivery.
- [Telegram Bots FAQ](https://core.telegram.org/bots/faq): platform messaging behavior and limits.

OTP delivery is bounded to ten seconds; timeout invalidates the pending code and keeps the generic response. Database serialization covers delivery to prevent resend/verification races. The bot supports `/start` and `/help`, both guiding existing users to **Connect Telegram**.

## Bot profile prepared for BotFather

- Name: **Ocharo**; username: **@ocharo_bot**.
- About: `Sign in to Ocharo apps with Telegram.`
- Description: `Connect your Telegram account to Ocharo and receive sign-in codes here. Start from your Ocharo account settings to connect. Never share your codes.`
- Profile image: the existing compact Ocharo mark; leave description media blank.
- Commands: `start - Connect your Ocharo account`; `help - Get sign-in help`.
- Privacy-policy URL must point to a real published policy; that publication remains pending.

These are prepared profile values, not evidence that BotFather settings were changed. Linking starts after password sign-in: open settings, choose **Connect Telegram**, confirm the password, follow the one-time bot link, and share your own contact. Keep bot secrets in server-local configuration only.
