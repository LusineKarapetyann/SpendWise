using Telegram.Bot;
using Telegram.Bot.Types;
using Chat = SpendWise.Entities.Chat;

namespace SpendWise.Services.Commands;

public class StartCommandHandler : ITelegramCommandHandler
{
    public async Task HandleAsync(ITelegramBotClient bot, Chat chat, Message message, string[] args, CancellationToken ct)
    {
        await bot.SendMessage(
             message.Chat.Id,
             "Hi! I'm your finance consultant.\n\n" +
             "Send one spending per message, one line:\n" +
             $"{SpendingParser.FormatHelp}\n\n" +
             "Commands:\n" +
             "/today — sum and list for today\n" +
             "/month — short month recap",
             cancellationToken: ct);
    }
}