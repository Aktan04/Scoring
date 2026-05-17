using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace Scoring.Models
{
    [Table("loan_applications")]
    public class LoanApplication
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // --- 1. СВЯЗИ С ПОЛЬЗОВАТЕЛЯМИ И ПРОДУКТАМИ ---
        
        [Required]
        [Display(Name = "Инициатор (Maker)")]
        public int MakerId { get; set; }
        [ForeignKey("MakerId")]
        [ValidateNever]
        public virtual User Maker { get; set; } // Тот, кто заполнил анкету со слов клиента

        [Display(Name = "Проверяющий (Checker)")]
        public int? CheckerId { get; set; }
        [ForeignKey("CheckerId")]
        [ValidateNever]
        public virtual User Checker { get; set; } // Тот, кто вынес финальное решение (если был ManualReview)

        [Required]
        [Display(Name = "Кредитный продукт")]
        public int LoanProductId { get; set; }
        [ValidateNever]
        public virtual LoanProduct LoanProduct { get; set; }

        // --- 2. ПАРАМЕТРЫ КРЕДИТА ---

        [Required]
        [Display(Name = "Статус заявки")]
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft; // Используем Enum вместо отдельной таблицы

        [Required]
        [Range(1000, 10000000)]
        [Display(Name = "Сумма (сом)")]
        [Precision(18, 2)] // Обязательно для PostgreSQL (18 цифр всего, 2 после запятой)
        public decimal Amount { get; set; }

        [Required]
        [Range(3, 240)]
        [Display(Name = "Срок (мес.)")]
        public int TermMonths { get; set; }

        // --- 3. ПЕРСОНАЛЬНЫЕ ДАННЫЕ (Анкета) ---

        [Required]
        [StringLength(100)] // Ограничение для базы данных
        [Display(Name = "Имя")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }

        [Required]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "ИНН должен состоять из 14 символов")]
        [Display(Name = "ИНН")]
        public string Inn { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Серия/номер паспорта")]
        public string PassportSerial { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Дата рождения")]
        public DateTime BirthDate { get; set; }

        // --- 4. СОЦИАЛЬНО-ДЕМОГРАФИЧЕСКИЕ ДАННЫЕ ---

        [Display(Name = "Семейное положение")]
        public MaritalStatus MaritalStatus { get; set; }

        [Display(Name = "Количество иждивенцев (детей)")]
        [Range(0, 20)]
        public int DependentsCount { get; set; }

        [Display(Name = "Уровень образования")]
        public EducationLevel EducationLevel { get; set; }

        // --- 5. ФИНАНСОВЫЕ ПОКАЗАТЕЛИ И ТРУДОУСТРОЙСТВО ---

        [Display(Name = "Тип занятости")]
        public EmploymentType EmploymentType { get; set; }

        [StringLength(100)]
        [Display(Name = "Сфера деятельности (Отрасль)")]
        public string EmployerIndustry { get; set; }

        [Display(Name = "Стаж работы (полных лет)")]
        [Range(0, 50)]
        public int EmploymentYears { get; set; }

        [Required]
        [Display(Name = "Основной доход (сом)")]
        [Precision(18, 2)]
        public decimal IncomeAmount { get; set; }

        [Display(Name = "Дополнительный доход (сом)")]
        [Precision(18, 2)]
        public decimal AdditionalIncome { get; set; }

        // --- 6. АКТИВЫ ---

        [Display(Name = "В собственности есть недвижимость")]
        public bool HasRealEstate { get; set; }

        [Display(Name = "В собственности есть автомобиль")]
        public bool HasVehicle { get; set; }

        // --- 7. КРЕДИТНАЯ ИСТОРИЯ (Агрегированные данные) ---

        [Display(Name = "Количество текущих кредитов")]
        [Range(0, 100)]
        public int ActiveLoansCount { get; set; }

        [Display(Name = "Имелись ли просрочки в прошлом")]
        public bool HasPastDelinquencies { get; set; }

        // --- 8. АУДИТ И ВРЕМЯ ---

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } // Для фиксации времени последнего изменения
    }

    // =========================================================
    // ENUMS (Перечисления для строгой типизации)
    // =========================================================

    public enum ApplicationStatus
    {
        [Display(Name = "Черновик")]
        Draft = 1,
        
        [Display(Name = "На скоринге")]
        InScoring = 2,
        
        [Display(Name = "Одобрено")]
        Approved = 3,
        
        [Display(Name = "Отказано")]
        Rejected = 4,
        
        [Display(Name = "Ручная проверка")]
        ManualReview = 5
    }

    public enum MaritalStatus
    {
        [Display(Name = "Холост / Не замужем")]
        Single = 1,
        
        [Display(Name = "В браке")]
        Married = 2,
        
        [Display(Name = "В разводе")]
        Divorced = 3,
        
        [Display(Name = "Вдовец / Вдова")]
        Widow = 4
    }

    public enum EducationLevel
    {
        [Display(Name = "Среднее")]
        Secondary = 1,
        
        [Display(Name = "Среднее специальное")]
        Vocational = 2,
        
        [Display(Name = "Высшее (Бакалавр)")]
        Bachelor = 3,
        
        [Display(Name = "Высшее (Магистр/Специалист)")]
        Master = 4
    }

    public enum EmploymentType
    {
        [Display(Name = "Полная занятость (Найм)")]
        FullTime = 1,
        
        [Display(Name = "Частичная занятость")]
        PartTime = 2,
        
        [Display(Name = "Собственный бизнес / ИП")]
        BusinessOwner = 3,
        
        [Display(Name = "Фриланс / Самозанятый")]
        Freelance = 4,
        
        [Display(Name = "Пенсионер")]
        Pensioner = 5
    }
}