using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Options;
using Telegram.Bot;

namespace SpendWise.Services;

public class DailyDigestWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly TelegramOptions _telegramOptions;
    private readonly AppOptions _appOptions;
    private readonly ILogger<DailyDigestWorker> _logger;

    public DailyDigestWorker(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient bot,
        IOptions<TelegramOptions> telegramOptions,
        IOptions<AppOptions> appOptions,
        ILogger<DailyDigestWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _telegramOptions = telegramOptions.Value;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(DateTime.UtcNow, _telegramOptions.DigestHourUtc);
            _logger.LogInformation("Daily digest worker sleeping {Delay} until next run.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    internal static TimeSpan TimeUntilNextRun(DateTime nowUtc, int digestHourUtc)
    {
        var todayRun = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, digestHourUtc, 0, 0, DateTimeKind.Utc);
        var nextRun = nowUtc < todayRun ? todayRun : todayRun.AddDays(1);
        return nextRun - nowUtc;
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<StatsService>();

        var nowUtc = DateTime.UtcNow;
        var chats = await stats.GetChatsWithSpendingThisMonthAsync(db, nowUtc, ct);

        _logger.LogInformation("Daily digest: sending to {Count} chat(s).", chats.Count);

        foreach (var chat in chats)
        {
            try
            {
                var monthStats = await stats.GetMonthStatsAsync(db, chat.Id, nowUtc, ct);

                if (monthStats is null)
                {
                    continue; 
                }

                var reportUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/report/{chat.ReportToken}";
                var text = DigestMessageBuilder.Build(monthStats, _telegramOptions.Currency, reportUrl);

                await _bot.SendMessage(chat.TelegramChatId, text, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send daily digest to chat {ChatId}.", chat.TelegramChatId);
            }
        }
    }
}
