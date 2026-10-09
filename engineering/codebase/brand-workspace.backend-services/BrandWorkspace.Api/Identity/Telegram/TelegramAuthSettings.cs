namespace BrandWorkspace.Api.Identity.Telegram;

/// <summary>Controls the explicitly enabled Telegram bot sign-in integration.</summary>
public sealed record TelegramAuthSettings
{
    public const string SectionName = "Identity:Telegram";
    public bool Enabled { get; init; }
    public string BotUsername { get; init; } = "";
    public string BotToken { get; init; } = "";
    public string WebhookSecret { get; init; } = "";
    public bool PollingEnabled { get; init; }
}
