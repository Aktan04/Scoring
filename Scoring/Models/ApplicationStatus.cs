using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Scoring.Models;

namespace CreditScoringSystem.Models
{
    [Table("application_statuses")]
    public class ApplicationStatus
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Название статуса")]
        public string Name { get; set; } // Например: Draft, Scoring, Approved, Rejected, ManualReview

        // Навигационное свойство для связи с заявками
        public virtual ICollection<LoanApplication> Applications { get; set; }
    }
}