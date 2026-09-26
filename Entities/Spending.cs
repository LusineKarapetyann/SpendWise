namespace SpendWise.Entities;

public class Spending
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public Chat? Chat { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string? Note { get; set; }
    public DateTime SpentAtUtc { get; set; }
}