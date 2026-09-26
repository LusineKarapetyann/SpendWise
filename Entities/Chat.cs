namespace SpendWise.Entities;
public class Chat
{
    public int Id { get; set; }
    public long TelegramChatId { get; set; }
    public string ReportToken { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public List<Spending> Spendings { get; set; } = new();
}
