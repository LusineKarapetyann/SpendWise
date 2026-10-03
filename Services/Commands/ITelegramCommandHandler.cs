using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;

namespace SpendWise.Services.Commands;

public interface ITelegramCommandHandler
{
    Task HandleAsync(ITelegramBotClient bot, Chat chat, Message message, string[] args, CancellationToken ct);
}