using SpendWise.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SpendWise.Services;

public static class SpendingParser
{
    private static readonly Regex CategoryPattern = new("^[a-zA-Z]+$", RegexOptions.Compiled);

    public const string FormatHelp =
        "Format: <amount> <category> [note...] — e.g. \"4.50 coffee\" or \"32.10 groceries lidl\". " +
        "Amount is a plain positive number (. or , as decimal separator), category is one word, letters only.";

    public static bool TryParse(string line, out ParsedSpending? spending, out string? error)
    {
        spending = null;
        error = null;

        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            error = FormatHelp;
            return false;
        }

        var amountToken = parts[0].Replace(',', '.');

        if (!decimal.TryParse(amountToken, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0)
        {
            error = $"First value must be a positive amount (got \"{parts[0]}\"). {FormatHelp}";
            return false;
        }

        var category = parts[1];

        if (!CategoryPattern.IsMatch(category))
        {
            error = $"Category must be one word, letters only (got \"{category}\"). {FormatHelp}";
            return false;
        }

        var note = parts.Length > 2 ? string.Join(' ', parts[2..]) : null;

        spending = new ParsedSpending
        {
            Amount = amount,
            Category = category.ToLowerInvariant(),
            Note = note
        };

        return true;
    }
}
