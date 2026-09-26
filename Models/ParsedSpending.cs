namespace SpendWise.Models;
public class ParsedSpending
{
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string? Note { get; set; }
}