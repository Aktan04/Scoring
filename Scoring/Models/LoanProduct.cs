using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scoring.Models;

[Table("loan_products")]
public class LoanProduct
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Название продукта")]
    public string Name { get; set; } // Например: "Потребительский", "Ипотека"

    [Display(Name = "Минимальная сумма")]
    public decimal MinAmount { get; set; }

    [Display(Name = "Максимальная сумма")]
    public decimal MaxAmount { get; set; }

    [Display(Name = "Процентная ставка (%)")]
    public decimal InterestRate { get; set; }

    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;

    // Навигационное свойство (один продукт = много заявок)
    public virtual ICollection<LoanApplication> Applications { get; set; }
}