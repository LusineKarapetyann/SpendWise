using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Options;
using SpendWise.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramFinanceBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName));

builder.Services.Configure<AppOptions>(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default is empty.");
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.BotToken))
    {
        throw new InvalidOperationException(
            "Telegram:BotToken is empty. Set it in appsettings.Development.json " +
            "or via `dotnet user-secrets set Telegram:BotToken <token>`.");
    }

    return new TelegramBotClient(options.BotToken);
});

builder.Services.AddSingleton<StatsService>();


builder.Services.AddScoped<TelegramUpdateHandler>();

builder.Services.AddHostedService<DailyDigestWorker>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

using (var webhookScope = app.Services.CreateScope())
{
    var bot = webhookScope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    var telegramOptions = webhookScope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    var appOptions = webhookScope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

    if (string.IsNullOrWhiteSpace(telegramOptions.WebhookSecret))
    {
        throw new InvalidOperationException(
            "Telegram:WebhookSecret is empty. Set a random string (e.g. `openssl rand -hex 16`) " +
            "in appsettings.Development.json.");
    }

    if (string.IsNullOrWhiteSpace(appOptions.PublicBaseUrl))
    {
        throw new InvalidOperationException("PublicBaseUrl is empty.");
    }

    var webhookUrl = $"{appOptions.PublicBaseUrl.TrimEnd('/')}/bot/{telegramOptions.WebhookSecret}";

    await bot.SetWebhook(
        url: webhookUrl,
        secretToken: telegramOptions.WebhookSecret,
        allowedUpdates: new[] { UpdateType.Message });

    app.Logger.LogInformation("Telegram webhook set to {WebhookUrl}", webhookUrl);
}

app.MapControllers();

app.Run();