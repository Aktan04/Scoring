using System.ComponentModel.DataAnnotations.Schema;
using Scoring.Models;

namespace CreditScoringSystem.Models
{
    [Table("scoring_results")]
    public class ScoringResult
    {
        public int Id { get; set; }
        
        public Guid ApplicationId { get; set; }
        public virtual LoanApplication Application { get; set; }

        public int TotalScore { get; set; }
        
        public string Decision { get; set; } // Approved, Rejected, ManualReview
        
        public int AntiFraudScore { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string RawResponseJson { get; set; } // Храним детали расчета
        
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}