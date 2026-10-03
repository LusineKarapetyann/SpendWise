using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;

namespace SpendWise.Services.Commands;

public class TodayCommandHandler : ITelegramCommandHandler
{
    private readonly AppDbContext _db;
    private readonly TelegramOptions _telegramOptions;
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(4);

    public TodayCommandHandler(AppDbContext db, IOptions<TelegramOptions> telegramOptions)
    {
        _db = db;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Chat chat, Message message, string[] args, CancellationToken ct)
    {
        var localNow = DateTime.UtcNow.Add(LocalOffset);
        var localTodayStart = localNow.Date;

        var utcStart = localTodayStart.Subtract(LocalOffset);
        var utcEnd = utcStart.AddDays(1);

        var todaySpendings = await _db.Spendings
            .Where(s => s.ChatId == chat.Id && s.SpentAtUtc >= utcStart && s.SpentAtUtc < utcEnd)
            .OrderBy(s => s.SpentAtUtc)
            .ToListAsync(ct);

        if (todaySpendings.Count == 0)
        {
            await bot.SendMessage(message.Chat.Id, "Nothing logged today yet.", cancellationToken: ct);
            return;
        }

        var total = todaySpendings.Sum(s => s.Amount);

        var lines = todaySpendings.Select(s =>
            $"{s.SpentAtUtc.Add(LocalOffset):HH:mm} · {Fmt(s.Amount)} {_telegramOptions.Currency} · {s.Category}" +
            (string.IsNullOrEmpty(s.Note) ? "" : $" ({s.Note})"));

        var text = $"Today: {Fmt(total)} {_telegramOptions.Currency} ({todaySpendings.Count} entries)\n\n" +
                   string.Join('\n', lines);

        await bot.SendMessage(message.Chat.Id, text, cancellationToken: ct);
    }

    private static string Fmt(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
}