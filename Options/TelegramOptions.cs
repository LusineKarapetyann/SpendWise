namespace SpendWise.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";
    public string BotToken { get; set; } = "";
    public string Currency { get; set; } = "AMD";
    public int DigestHourUtc { get; set; } = 18;
    public string WebhookSecret { get; set; } = "";
}
