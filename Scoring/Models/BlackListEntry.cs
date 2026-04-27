namespace Scoring.Models;

public class BlackListEntry
{
    public int Id { get; set; }
    public string Inn { get; set; }
    public string Reason { get; set; } // "Подозрение на мошенничество"
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}