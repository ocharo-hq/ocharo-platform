using System.Text.RegularExpressions;
using BrandWorkspace.Persistence.Access;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BrandWorkspace.Api.Identity.Telegram;

/// <summary>Links only an authenticated app challenge to the same private Telegram sender's own contact.</summary>
public sealed class TelegramBotUpdatesHandler(TelegramAuthRepository repository, ITelegramBotClient bot)
{
    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Chat.Type: ChatType.Private, From.IsBot: false } message
            || message.From.Id <= 0 || message.Chat.Id != message.From.Id) return;
        var text = message.Text?.Trim();
        if (text is not null && Regex.IsMatch(text, "^/start link_[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant))
        {
            await repository.BeginAsync("telegram-link", ct);
            var attached = await repository.AttachLinkRequestAsync(TelegramAuthEndpoints.Hash(text[12..]), message.From.Id, ct);
            await repository.CommitAsync(ct);
            if (attached)
            {
                await bot.SendMessage(message.Chat.Id, "Share your own contact to link this Telegram account to Ocharo.",
                    replyMarkup: new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { KeyboardButton.WithRequestContact("Share my phone number") } })
                    { ResizeKeyboard = true, OneTimeKeyboard = true }, cancellationToken: ct);
            }
            else await ReplyAsync(message.Chat.Id, "This link expired or was already used. Start linking again from Ocharo settings.", ct);
            return;
        }
        if (message.Contact is { } contact)
        {
            if (contact.UserId != message.From.Id || TelegramAuthEndpoints.NormalizePhone(contact.PhoneNumber) is not { } phone)
            {
                await ReplyAsync(message.Chat.Id, "Use the button to share your own contact. Forwarded contacts cannot link an account.", ct);
                return;
            }
            await repository.BeginAsync("telegram-link", ct);
            var linked = await repository.CompleteLinkAsync(message.From.Id, phone, ct);
            await repository.CommitAsync(ct);
            await ReplyAsync(message.Chat.Id, linked ? "Telegram linked. You can now request an Ocharo sign-in code using this phone number."
                : "Linking could not be completed. Sign in to Ocharo with your password and start linking from settings.", ct);
            return;
        }
        if (text?.StartsWith("/start", StringComparison.Ordinal) == true || text is "/help")
            await ReplyAsync(message.Chat.Id, "Sign in to Ocharo with your password, open settings, and choose Connect Telegram. Existing accounts only.", ct);
    }

    private async Task ReplyAsync(long chatId, string text, CancellationToken ct)
    {
        await bot.SendMessage(chatId, text, replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
    }
}
