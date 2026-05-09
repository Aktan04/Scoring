using Microsoft.AspNetCore.Identity;

namespace Scoring.Models;

public class User : IdentityUser<int>
{
    public string FullName { get; set; } // Дополнительное поле для сотрудника
}