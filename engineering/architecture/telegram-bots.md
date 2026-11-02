# Telegram bots

*Last updated: 2026-11-02*

Ocharo runs 3 Telegram bots, one per WoW2 Identity instance. Identity's Telegram module drives each bot: it links accounts, delivers access codes and sends platform messages. Ocharo apps never talk to a bot directly.

## Bots

| Environment | Username | Name | Updates | Token |
|---|---|---|---|---|
| prod | `@ocharo_bot` | Ocharo | webhook on the prod Identity | `Identity:Telegram:BotToken` secret on the prod Identity |
| dev | `@ocharo_dev_bot` | Ocharo Dev | webhook on the hosted dev Identity | `Identity:Telegram:BotToken` secret on the dev Identity |
| local | `@ocharo_local_bot` | Ocharo Local | polling while a developer's machine runs Identity | `Identity__Telegram__BotToken` in the ignored `.local/identity/telegram.env` |

- `Identity:Telegram:BotUserName` carries the username per environment; the username is public, the token never enters a repository.
- The owner keeps every token in the NordPass family vault.
- Identity has no Telegram test mode; the local bot replaces it.
- A webhook bot answers polling with `409`, so a local run never borrows the dev or prod bot.
- Identity keys each Telegram link by bot username, so replacing a bot starts with no linked accounts.

## BotFather settings

- Group privacy stays on for all 3 bots.
- Allow groups stays off for dev and local.
- Prod turns Allow groups on only to join the workspace group, then off again; inside that group the bot is an admin with every right off except Manage topics.
- Commands stay empty: Identity answers `/start` only.
- Profile pictures are pending; the compact Ocharo mark is the planned image, with no description media.

## Languages

- Profile text is English only, set without a `language_code`, so every Telegram app language sees the same text.
- The bot's own replies follow each sender's Telegram `language_code` in English, Russian or Uzbek; `Identity:Telegram:Messages:{culture}:{key}` overrides them.
- A translated profile later needs per-language text through the Bot API's `setMyName`, `setMyShortDescription` and `setMyDescription`.

## Profile copy

### Prod

About:

```text
The official bot of Ocharo, a contemporary Uzbek clothing brand.
```

Description:

```text
The official Telegram bot of Ocharo, a contemporary Uzbek clothing brand.

What it can do:
• Manage your access to the Ocharo platform
• Deliver platform updates and news
• Help you take actions on the Ocharo platform

Never share your access codes. Ocharo never asks for them.
```

### Dev

About:

```text
Ocharo's test bot for the dev environment. Customers use @ocharo_bot.
```

Description:

```text
Test bot for Ocharo's dev environment, for the Ocharo team only.

What it can do:
• Manage your access to the dev platform
• Deliver dev platform updates
• Help you take actions on the dev platform

The real Ocharo bot: @ocharo_bot
```

### Local

About:

```text
Ocharo's test bot for local development. Customers use @ocharo_bot.
```

Description:

```text
Test bot for Ocharo running on a developer's machine. It answers only while that machine runs Ocharo.

What it can do:
• Manage your access to the local platform
• Deliver local platform updates
• Help you take actions on the local platform

The real Ocharo bot: @ocharo_bot
```

## Sources

- [Telegram bot features](https://core.telegram.org/bots/features#botfather): BotFather profile fields and privacy mode.
- [`setMyDescription`](https://core.telegram.org/bots/api#setmydescription) and [`setMyShortDescription`](https://core.telegram.org/bots/api#setmyshortdescription): per-language profile text and the default text.
