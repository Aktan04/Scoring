using System.ComponentModel.DataAnnotations;

namespace Scoring.Models;

public class RegisterEmployeeViewModel
{
    [Required(ErrorMessage = "Укажите рабочий Email")]
    [EmailAddress(ErrorMessage = "Некорректный формат Email")]
    [Display(Name = "Email (Логин)")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Укажите ФИО сотрудника")]
    [Display(Name = "ФИО сотрудника")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Укажите временный пароль")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; }

    [Required(ErrorMessage = "Выберите должность")]
    [Display(Name = "Должность (Роль)")]
    public string Role { get; set; }
}