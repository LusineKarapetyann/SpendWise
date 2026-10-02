namespace SpendWise.Models;

public class SpendingView
{
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; 
    public DateTime SpentAtUtc { get; set; }
}