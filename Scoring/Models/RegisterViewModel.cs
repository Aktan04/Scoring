using System.ComponentModel.DataAnnotations;

namespace Scoring.Models;

public class RegisterViewModel
{
    [Required] public string Email { get; set; }
    [Required] public string FullName { get; set; }
    [Required] public string NickName { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; }
    [DataType(DataType.Password), Compare("Password")] public string ConfirmPassword { get; set; }
}