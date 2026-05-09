namespace Scoring.Models;

public class ApplicationHistory
{
    public int Id { get; set; }
    public Guid ApplicationId { get; set; }
    public virtual LoanApplication Application { get; set; }
    
    public ApplicationStatus OldStatus { get; set; }
    public ApplicationStatus NewStatus { get; set; }
    
    public int ChangedById { get; set; } // Какой сотрудник изменил
    public virtual User ChangedBy { get; set; }
    
    public string Comment { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}