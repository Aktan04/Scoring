using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scoring.Models
{
    [Table("scoring_results")]
    public class ScoringResult
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public Guid ApplicationId { get; set; }
        [ForeignKey("ApplicationId")]
        public virtual LoanApplication Application { get; set; }

        [Display(Name = "Итоговый балл")]
        public int TotalScore { get; set; }
        
        [Required]
        [Display(Name = "Решение системы")]
        public ScoringDecision Decision { get; set; } // ИСПОЛЬЗУЕМ ENUM
        
        [Display(Name = "Балл антифрода")]
        public int AntiFraudScore { get; set; }
        
        [Column(TypeName = "jsonb")]
        [Display(Name = "Лог расчета (JSON)")]
        public string RawResponseJson { get; set; } 
        
        [Display(Name = "Время расчета")]
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    // --- ENUM ДЛЯ РЕШЕНИЙ ---
    public enum ScoringDecision
    {
        [Display(Name = "Автоматически одобрено")]
        Approved = 1,
        
        [Display(Name = "Автоматический отказ")]
        Rejected = 2,
        
        [Display(Name = "Требуется ручная проверка")]
        ManualReview = 3
    }
}