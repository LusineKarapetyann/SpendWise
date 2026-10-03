using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;

namespace SpendWise.Services.Commands;

public class MonthCommandHandler : ITelegramCommandHandler
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly TelegramOptions _telegramOptions;
    private readonly AppOptions _appOptions;

    public MonthCommandHandler(
        AppDbContext db,
        StatsService stats,
        IOptions<TelegramOptions> telegramOptions,
        IOptions<AppOptions> appOptions)
    {
        _db = db;
        _stats = stats;
        _telegramOptions = telegramOptions.Value;
        _appOptions = appOptions.Value;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Chat chat, Message message, string[] args, CancellationToken ct)
    {
        var monthStats = await _stats.GetMonthStatsAsync(_db, chat.Id, DateTime.UtcNow, ct);

        if (monthStats is null)
        {
            await bot.SendMessage(message.Chat.Id, "Nothing logged this month yet.", cancellationToken: ct);
            return;
        }

        var reportUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/report/{chat.ReportToken}";
        var text = DigestMessageBuilder.Build(monthStats, _telegramOptions.Currency, reportUrl);

        await bot.SendMessage(message.Chat.Id, text, cancellationToken: ct);
    }
}