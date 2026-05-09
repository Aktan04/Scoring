using System.ComponentModel.DataAnnotations.Schema;

namespace Scoring.Models
{
    [Table("scoring_rules")]
    public class ScoringRule
    {
        public int Id { get; set; }
        
        public string ParameterName { get; set; } // Например: "Age", "Income"
        
        public decimal? MinValue { get; set; }
        
        public decimal? MaxValue { get; set; }
        
        public int WeightPoints { get; set; } // Баллы за этот диапазон
        
        public bool IsActive { get; set; } = true;
    }
}