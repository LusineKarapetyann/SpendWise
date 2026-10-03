using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Entities;
using SpendWise.Options;
using SpendWise.Services;
using SpendWise.Services.Commands;
using System.Globalization;
using System.Security.Cryptography;
using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;

namespace TelegramFinanceBot.Services;

public class TelegramUpdateHandler
{
    private readonly AppDbContext _db;
    private readonly TelegramOptions _telegramOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        AppDbContext db,
        IOptions<TelegramOptions> telegramOptions,
        IServiceProvider serviceProvider,
        ILogger<TelegramUpdateHandler> logger)
    {
        _db = db;
        _telegramOptions = telegramOptions.Value;
        _serviceProvider = serviceProvider;
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

        var chat = await GetOrCreateChatAsync(_db, message.Chat, ct);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;

        var commandHandler = _serviceProvider.GetKeyedService<ITelegramCommandHandler>(command);

        if (commandHandler is not null)
        {
            var args = parts.Skip(1).ToArray();
            await commandHandler.HandleAsync(bot, chat, message, args, ct);
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

    private static async Task<Chat> GetOrCreateChatAsync(AppDbContext db, Telegram.Bot.Types.Chat tgChat, CancellationToken ct)
    {
        var chat = await db.Chats.FirstOrDefaultAsync(c => c.TelegramChatId == tgChat.Id, ct);

        if (chat is not null)
        {
            return chat;
        }

        chat = new Chat
        {
            TelegramChatId = tgChat.Id,
            ReportToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            StartedAtUtc = DateTime.UtcNow
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync(ct);

        return chat;
    }

    private static string Fmt(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
}