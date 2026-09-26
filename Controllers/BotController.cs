using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SpendWise.Options;
using System.Security.Cryptography;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramFinanceBot.Services;

namespace TelegramFinanceBot.Controllers;

[ApiController]
[AllowAnonymous]
[Route("bot")]
public class BotController : ControllerBase
{
    private readonly ITelegramBotClient _bot;
    private readonly TelegramUpdateHandler _handler;
    private readonly TelegramOptions _options;
    private readonly ILogger<BotController> _logger;

    public BotController(
        ITelegramBotClient bot,
        TelegramUpdateHandler handler,
        IOptions<TelegramOptions> options,
        ILogger<BotController> logger)
    {
        _bot = bot;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    
    [HttpPost("{secret}")]
    public async Task<IActionResult> Post(string secret, [FromBody] Update update, CancellationToken ct)
    {
        if (!FixedTimeEquals(secret, _options.WebhookSecret))
        {
            return NotFound();
        }

        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var headerToken)
            || !FixedTimeEquals(headerToken.ToString(), _options.WebhookSecret))
        {
            return NotFound();
        }

        try
        {
            await _handler.HandleAsync(_bot, update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook update {UpdateId}.", update?.Id);
        }

        return Ok();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        var byteA = Encoding.UTF8.GetBytes(a);
        var byteB = Encoding.UTF8.GetBytes(b);

        if (byteA.Length != byteB.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(byteA, byteB);
    }
}
