using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace BrandWorkspace.Api.Identity.Telegram;

/// <summary>Receives bot updates only when Development polling is explicitly enabled; failures retry after five seconds.</summary>
public sealed class TelegramPollingService(TelegramAuthSettings settings, IHostEnvironment environment,
    IServiceScopeFactory scopes, IServiceProvider services, ILogger<TelegramPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled || !settings.PollingEnabled || !environment.IsDevelopment()) return;
        var bot = services.GetRequiredService<ITelegramBotClient>();
        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdates(offset: offset, timeout: 25, allowedUpdates: [UpdateType.Message], cancellationToken: stoppingToken);
                foreach (var update in updates)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<TelegramBotUpdatesHandler>().HandleAsync(update, stoppingToken);
                    offset = update.Id + 1;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is RequestException or HttpRequestException or TimeoutException or Npgsql.NpgsqlException)
            {
                logger.LogWarning("Telegram polling could not complete; retrying without changing webhook configuration.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
