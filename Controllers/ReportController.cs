namespace SpendWise.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Data;
using SpendWise.Entities;
using SpendWise.Options;
using SpendWise.Services;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

[ApiController]
[AllowAnonymous]
[Route("report")]
public class ReportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly TelegramOptions _telegramOptions;

    public ReportController(AppDbContext db, StatsService stats, IOptions<TelegramOptions> telegramOptions)
    {
        _db = db;
        _stats = stats;
        _telegramOptions = telegramOptions.Value;
    }

    
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        var chat = await _db.Chats.FirstOrDefaultAsync(c => c.ReportToken == token, ct);

        if (chat is null)
        {
            return NotFound();
        }

        var nowUtc = DateTime.UtcNow;

        var monthStats = await _stats.GetMonthStatsAsync(_db, chat.Id, nowUtc, ct);
        var last14 = await _stats.GetLast14DaysAsync(_db, chat.Id, nowUtc, ct);
        var last20 = await _stats.GetLast20SpendingsAsync(_db, chat.Id, ct);

        var html = RenderHtml(monthStats, last14, last20, _telegramOptions.Currency);

        return Content(html, "text/html");
    }

    private static string RenderHtml(MonthStats? month, List<DayTotal> last14, List<Spending> last20, string currency)
    {
        var enc = HtmlEncoder.Default;
        var cur = enc.Encode(currency);
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Spending report</title>");
        sb.Append("<style>" +
                   "body{font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:640px;margin:2rem auto;padding:0 1rem;color:#1a1a1a;}" +
                   "table{width:100%;border-collapse:collapse;margin:.5rem 0 1.5rem;}" +
                   "th,td{text-align:left;padding:.4rem .5rem;border-bottom:1px solid #e2e2e2;}" +
                   "th{color:#666;font-weight:600;font-size:.85rem;text-transform:uppercase;}" +
                   "h1{margin-bottom:.25rem;} h2{margin-top:2rem;font-size:1.05rem;color:#333;}" +
                   ".stat{font-size:1.1rem;margin:.3rem 0;} .muted{color:#666;}" +
                   "</style></head><body>");

        sb.Append("<h1>Spending report</h1>");

        if (month is null)
        {
            sb.Append("<p class=\"muted\">No spendings recorded this month.</p>");
        }
        else
        {
            sb.Append($"<p class=\"stat\"><strong>This month:</strong> {Fmt(month.MonthTotal)} {cur} " +
                      $"<span class=\"muted\">({month.MonthCount} entries)</span></p>");
            sb.Append($"<p class=\"stat\"><strong>Last 7 days:</strong> {Fmt(month.Last7Total)} {cur}</p>");
            sb.Append($"<p class=\"stat\"><strong>Previous 7 days:</strong> {Fmt(month.Previous7Total)} {cur}</p>");
            sb.Append($"<p class=\"stat\"><strong>Typical day this month:</strong> {Fmt(month.TypicalDay)} {cur}</p>");

            sb.Append("<h2>Category breakdown</h2><table><tr><th>Category</th><th>Total</th></tr>");
            foreach (var c in month.AllCategories)
            {
                sb.Append($"<tr><td>{enc.Encode(c.Category)}</td><td>{Fmt(c.Total)} {cur}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<h2>Last 14 days</h2><table><tr><th>Date</th><th>Total</th></tr>");
        foreach (var d in last14)
        {
            sb.Append($"<tr><td>{d.Date:yyyy-MM-dd}</td><td>{Fmt(d.Total)} {cur}</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Last 20 spendings</h2><table><tr><th>Date (UTC)</th><th>Amount</th><th>Category</th><th>Note</th></tr>");
        foreach (var s in last20)
        {
            sb.Append("<tr>" +
                      $"<td>{s.SpentAtUtc:yyyy-MM-dd HH:mm}</td>" +
                      $"<td>{Fmt(s.Amount)} {cur}</td>" +
                      $"<td>{enc.Encode(s.Category)}</td>" +
                      $"<td>{enc.Encode(s.Note ?? "")}</td>" +
                      "</tr>");
        }
        sb.Append("</table>");

        sb.Append("</body></html>");

        return sb.ToString();
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
