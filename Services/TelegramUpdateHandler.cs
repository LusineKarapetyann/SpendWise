using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Entities;
using SpendWise.Options;
using SpendWise.Services;
using System.Globalization;
using System.Security.Cryptography;
using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;


namespace TelegramFinanceBot.Services;

public class TelegramUpdateHandler
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly TelegramOptions _telegramOptions;
    private readonly AppOptions _appOptions;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        AppDbContext db,
        StatsService stats,
        IOptions<TelegramOptions> telegramOptions,
        IOptions<AppOptions> appOptions,
        ILogger<TelegramUpdateHandler> logger)
    {
        _db = db;
        _stats = stats;
        _telegramOptions = telegramOptions.Value;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message || message.Text is not { } rawText)
        {
            return;
        }

        var telegramChatId = message.Chat.Id;
        var text = rawText.Trim();

        _logger.LogInformation("Chat {ChatId}: {Text}", telegramChatId, text);

        var chat = await GetOrCreateChatAsync(_db, telegramChatId, ct);

        if (text == "/start")
        {
            await bot.SendMessage(
                telegramChatId,
                "Hi! I'm your finance consultant.\n\n" +
                "Send one spending per message, one line:\n" +
                $"{SpendingParser.FormatHelp}\n\n" +
                "Commands:\n" +
                "/today — sum and list for today\n" +
                "/month — short month recap",
                cancellationToken: ct);
            return;
        }

        if (text == "/today")
        {
            await HandleTodayAsync(bot, chat, telegramChatId, ct);
            return;
        }

        if (text == "/month")
        {
            await HandleMonthAsync(bot, chat, telegramChatId, ct);
            return;
        }

        if (SpendingParser.TryParse(text, out var parsed, out var parseError))
        {
            _db.Spendings.Add(new Spending
            {
                ChatId = chat.Id,
                Amount = parsed!.Amount,
                Category = parsed.Category,
                Note = parsed.Note,
                SpentAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            var noteSuffix = string.IsNullOrEmpty(parsed.Note) ? "" : $" ({parsed.Note})";
            await bot.SendMessage(
                telegramChatId,
                $"Saved {Fmt(parsed.Amount)} {_telegramOptions.Currency} · {parsed.Category}{noteSuffix}",
                cancellationToken: ct);
            return;
        }

        await bot.SendMessage(telegramChatId, parseError ?? SpendingParser.FormatHelp, cancellationToken: ct);
    }

    private static async Task<Chat> GetOrCreateChatAsync(AppDbContext db, long telegramChatId, CancellationToken ct)
    {
        var chat = await db.Chats.FirstOrDefaultAsync(c => c.TelegramChatId == telegramChatId, ct);

        if (chat is not null)
        {
            return chat;
        }

        chat = new Chat
        {
            TelegramChatId = telegramChatId,
            ReportToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            StartedAtUtc = DateTime.UtcNow
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync(ct);

        return chat;
    }

    private async Task HandleTodayAsync(ITelegramBotClient bot, Chat chat, long telegramChatId, CancellationToken ct)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var todaySpendings = await _db.Spendings
            .Where(s => s.ChatId == chat.Id && s.SpentAtUtc >= todayStart && s.SpentAtUtc < tomorrowStart)
            .OrderBy(s => s.SpentAtUtc)
            .ToListAsync(ct);

        if (todaySpendings.Count == 0)
        {
            await bot.SendMessage(telegramChatId, "Nothing logged today yet.", cancellationToken: ct);
            return;
        }

        var total = todaySpendings.Sum(s => s.Amount);

        var lines = todaySpendings.Select(s =>
            $"{s.SpentAtUtc:HH:mm} · {Fmt(s.Amount)} {_telegramOptions.Currency} · {s.Category}" +
            (string.IsNullOrEmpty(s.Note) ? "" : $" ({s.Note})"));

        var text = $"Today: {Fmt(total)} {_telegramOptions.Currency} ({todaySpendings.Count} entries)\n\n" +
                    string.Join('\n', lines);

        await bot.SendMessage(telegramChatId, text, cancellationToken: ct);
    }

    private async Task HandleMonthAsync(ITelegramBotClient bot, Chat chat, long telegramChatId, CancellationToken ct)
    {
        var monthStats = await _stats.GetMonthStatsAsync(_db, chat.Id, DateTime.UtcNow, ct);

        if (monthStats is null)
        {
            await bot.SendMessage(telegramChatId, "Nothing logged this month yet.", cancellationToken: ct);
            return;
        }

        var reportUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/report/{chat.ReportToken}";
        var text = DigestMessageBuilder.Build(monthStats, _telegramOptions.Currency, reportUrl);

        await bot.SendMessage(telegramChatId, text, cancellationToken: ct);
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
