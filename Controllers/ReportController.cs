using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendWise.Data;
using SpendWise.Models;

namespace SpendWise.Controllers;

[AllowAnonymous]
[Route("report")]
public class ReportController : Controller
{
    private readonly AppDbContext _db;

    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(4);

    public ReportController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.ErrorMessage = "The report link is missing a token.";
            return View("Error");
        }

        var chat = await _db.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ReportToken == token, ct);

        if (chat == null)
        {
            ViewBag.ErrorMessage = "This report link is invalid or has expired.";
            return View("Error");
        }

        var rawSpendings = await _db.Spendings
            .AsNoTracking()
            .Where(s => s.ChatId == chat.Id)
            .OrderByDescending(s => s.SpentAtUtc)
            .ToListAsync(ct);

        var spendings = rawSpendings.Select(s => new
        {
            Entity = s,
            LocalDate = s.SpentAtUtc.Add(LocalOffset).Date
        }).ToList();

        var todayLocal = DateTime.UtcNow.Add(LocalOffset).Date;
        var startOfMonthLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1);

        var last7DaysCutoff = todayLocal.AddDays(-6);
        var prev7DaysCutoff = todayLocal.AddDays(-13);

        var currentMonthTotal = spendings
            .Where(s => s.LocalDate >= startOfMonthLocal)
            .Sum(s => s.Entity.Amount);

        var current7DaysTotal = spendings
            .Where(s => s.LocalDate >= last7DaysCutoff)
            .Sum(s => s.Entity.Amount);

        var previous7DaysTotal = spendings
            .Where(s => s.LocalDate >= prev7DaysCutoff && s.LocalDate < last7DaysCutoff)
            .Sum(s => s.Entity.Amount);

        var viewModel = new ReportViewModel
        {
            ChatTitle = "Spending report",
            Token = token,

            CurrentMonthTotal = currentMonthTotal,
            Current7DaysTotal = current7DaysTotal,
            Previous7DaysTotal = previous7DaysTotal,

            CategoryBreakdown = rawSpendings
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Uncategorized" : s.Category)
                .Select(g => new CategorySpending
                {
                    Category = g.Key,
                    TotalAmount = g.Sum(s => s.Amount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList(),

            Spendings = rawSpendings.Select(s => new SpendingView
            {
                Amount = s.Amount,
                Category = string.IsNullOrWhiteSpace(s.Category) ? "Uncategorized" : s.Category,
                Description = s.Note ?? string.Empty,
                SpentAtUtc = s.SpentAtUtc
            }).ToList()
        };

        return View(viewModel);
    }
}