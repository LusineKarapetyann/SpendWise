using System.Globalization;
using System.Text;

namespace SpendWise.Services;

public static class DigestMessageBuilder
{
    public static string Build(MonthStats stats, string currency, string reportUrl)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"This month: {Fmt(stats.MonthTotal)} {currency} ({stats.MonthCount} entries)");
        sb.AppendLine($"Last 7 days: {Fmt(stats.Last7Total)} {currency}");
        sb.AppendLine($"Previous 7 days: {Fmt(stats.Previous7Total)} {currency}");
        sb.AppendLine($"Typical day this month: {Fmt(stats.TypicalDay)} {currency}");

        if (stats.TopCategories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Top categories:");
            foreach (var c in stats.TopCategories)
            {
                sb.AppendLine($"  • {c.Category}: {Fmt(c.Total)} {currency}");
            }
        }

        sb.Append($"\nFull report: {reportUrl}");

        return sb.ToString();
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
