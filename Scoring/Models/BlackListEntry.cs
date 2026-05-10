namespace Scoring.Models;

public class BlackListEntry
{
    public int Id { get; set; }
    public string Inn { get; set; }
    public string Reason { get; set; } // "Подозрение на мошенничествоxnjkt"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}