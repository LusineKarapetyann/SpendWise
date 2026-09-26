using Microsoft.EntityFrameworkCore;
using SpendWise.Data;
using SpendWise.Entities;

namespace SpendWise.Services;

public record CategoryTotal(string Category, decimal Total);

public class MonthStats
{
    public decimal MonthTotal { get; init; }
    public int MonthCount { get; init; }
    public decimal Last7Total { get; init; }
    public decimal Previous7Total { get; init; }
    public decimal TypicalDay { get; init; }
    public IReadOnlyList<CategoryTotal> TopCategories { get; init; } = Array.Empty<CategoryTotal>();
    public IReadOnlyList<CategoryTotal> AllCategories { get; init; } = Array.Empty<CategoryTotal>();
}

public class DayTotal
{
    public DateOnly Date { get; init; }
    public decimal Total { get; init; }
}

public class StatsService
{
    public async Task<MonthStats?> GetMonthStatsAsync(
        AppDbContext db, int chatId, DateTime nowUtc, CancellationToken ct = default)
    {
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var todayStart = nowUtc.Date;
        var last7Start = todayStart.AddDays(-7);
        var previous7Start = todayStart.AddDays(-14);

        var monthQuery = db.Spendings
            .Where(s => s.ChatId == chatId && s.SpentAtUtc >= monthStart && s.SpentAtUtc <= nowUtc);

        var monthCount = await monthQuery.CountAsync(ct);

        if (monthCount == 0)
        {
            return null;
        }

        var monthTotal = await monthQuery.SumAsync(s => s.Amount, ct);

        var last7Total = await db.Spendings
            .Where(s => s.ChatId == chatId && s.SpentAtUtc >= last7Start && s.SpentAtUtc < todayStart)
            .SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

        var previous7Total = await db.Spendings
            .Where(s => s.ChatId == chatId && s.SpentAtUtc >= previous7Start && s.SpentAtUtc < last7Start)
            .SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

        var daysElapsed = nowUtc.Day; 
        var typicalDay = monthTotal / daysElapsed;

        var rawCategoryTotals = await monthQuery
            .GroupBy(s => s.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(s => s.Amount) })
            .ToListAsync(ct);

        var allCategories = rawCategoryTotals
            .OrderByDescending(c => c.Total)
            .Select(c => new CategoryTotal(c.Category, c.Total))
            .ToList();

        return new MonthStats
        {
            MonthTotal = monthTotal,
            MonthCount = monthCount,
            Last7Total = last7Total,
            Previous7Total = previous7Total,
            TypicalDay = typicalDay,
            TopCategories = allCategories.Take(3).ToList(),
            AllCategories = allCategories
        };
    }

    public async Task<List<DayTotal>> GetLast14DaysAsync(
        AppDbContext db, int chatId, DateTime nowUtc, CancellationToken ct = default)
    {
        var todayStart = nowUtc.Date;
        var windowStart = todayStart.AddDays(-13);

        var raw = await db.Spendings
            .Where(s => s.ChatId == chatId && s.SpentAtUtc >= windowStart)
            .ToListAsync(ct);

        var byDay = raw
            .GroupBy(s => DateOnly.FromDateTime(s.SpentAtUtc))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Amount));

        var rows = new List<DayTotal>();

        for (var i = 13; i >= 0; i--)
        {
            var date = DateOnly.FromDateTime(todayStart.AddDays(-i));
            rows.Add(new DayTotal { Date = date, Total = byDay.GetValueOrDefault(date, 0m) });
        }

        return rows;
    }

    public async Task<List<Spending>> GetLast20SpendingsAsync(
        AppDbContext db, int chatId, CancellationToken ct = default)
    {
        return await db.Spendings
            .Where(s => s.ChatId == chatId)
            .OrderByDescending(s => s.SpentAtUtc)
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task<List<Chat>> GetChatsWithSpendingThisMonthAsync(
        AppDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return await db.Chats
            .Where(c => db.Spendings.Any(s =>
                s.ChatId == c.Id && s.SpentAtUtc >= monthStart && s.SpentAtUtc <= nowUtc))
            .ToListAsync(ct);
    }
}
