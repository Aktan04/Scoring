using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CreditScoringSystem.Models;

namespace Scoring.Models
{
    [Table("loan_applications")]
    public class LoanApplication
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // --- НОВЫЕ ПОЛЯ ДЛЯ СВЯЗИ С ПОЛЬЗОВАТЕЛЕМ ---
        [Required]
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        // --------------------------------------------

        [Display(Name = "Статус")]
        public int StatusId { get; set; }
        public virtual ApplicationStatus Status { get; set; }

        public int LoanProductId { get; set; }
        
        public virtual LoanProduct LoanProduct { get; set; }

        [Required, Range(1000, 10000000), Display(Name = "Сумма")]
        public decimal Amount { get; set; }

        [Required, Range(3, 240), Display(Name = "Срок (мес.)")]
        public int TermMonths { get; set; }

        // Персональные данные
        [Required, Display(Name = "Имя")]
        public string FirstName { get; set; }

        [Required, Display(Name = "Фамилия")]
        public string LastName { get; set; }

        [Required, StringLength(14), Display(Name = "ИНН")]
        public string Inn { get; set; }

        [Required, Display(Name = "Серия/номер паспорта")]
        public string PassportSerial { get; set; }

        [DataType(DataType.Date), Display(Name = "Дата рождения")]
        public DateTime BirthDate { get; set; }

        // Финансовые показатели
        [Required, Display(Name = "Ежемесячный доход")]
        public decimal IncomeAmount { get; set; }

        [Display(Name = "Стаж работы (лет)")]
        public int EmploymentYears { get; set; }

        [Display(Name = "Семейное положение")]
        public string FamilyStatus { get; set; }
        
        public int? OfficerId { get; set; }
        [ForeignKey("OfficerId")]
        public virtual User Officer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}