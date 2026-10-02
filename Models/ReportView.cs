namespace SpendWise.Models;

public class CategorySpending
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class ReportViewModel
{
    public string ChatTitle { get; set; } = "Chat Report";
    public string Token { get; set; } = string.Empty;

    public decimal CurrentMonthTotal { get; set; }
    public decimal Current7DaysTotal { get; set; }
    public decimal Previous7DaysTotal { get; set; }

    public List<CategorySpending> CategoryBreakdown { get; set; } = new();

    public List<SpendingView> Spendings { get; set; } = new();
}