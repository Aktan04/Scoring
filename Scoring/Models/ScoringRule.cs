using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scoring.Models
{
    [Table("scoring_rules")]
    public class ScoringRule
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [Display(Name = "Параметр оценки")]
        public ScoringParameter Parameter { get; set; } // ИСПОЛЬЗУЕМ ENUM
        
        [Display(Name = "Минимальное значение")]
        [Precision(18, 2)] // Важно для денежных параметров (Доход)
        public decimal? MinValue { get; set; }
        
        [Display(Name = "Максимальное значение")]
        [Precision(18, 2)]
        public decimal? MaxValue { get; set; }
        
        [Required]
        [Display(Name = "Весовой балл")]
        public int WeightPoints { get; set; } 
        
        [Display(Name = "Активно")]
        public bool IsActive { get; set; } = true;
    }

    // --- ENUM ДЛЯ ПАРАМЕТРОВ ---
    public enum ScoringParameter
    {
        [Display(Name = "Возраст (Age)")] 
        Age = 1,
        
        [Display(Name = "Доход (Income)")] 
        Income = 2,
        
        [Display(Name = "Стаж работы (Experience)")] 
        Experience = 3,
        
        [Display(Name = "Долговая нагрузка (DTI)")] 
        DTI = 4,
        
        [Display(Name = "Количество иждивенцев")] 
        DependentsCount = 5,
        
        [Display(Name = "Семейное положение")] 
        MaritalStatus = 6,
        
        [Display(Name = "Наличие недвижимости")] 
        HasRealEstate = 7,
        
        [Display(Name = "Наличие автомобиля")] 
        HasVehicle = 8
    }
}