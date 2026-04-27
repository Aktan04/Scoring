using Microsoft.AspNetCore.Identity;
using Scoring.Models;

public class AdminInitializer
{
    public static async Task SeedAdminUser(RoleManager<IdentityRole<int>> _roleManager, UserManager<User> _userManager)
    {
        string adminEmail = "admin@bank.com";
        string adminPassword = "admin"; // Для ВКР простая политика паролей — это ок
        
        // Роли согласно специфике кредитного отдела
        var roles = new [] { "admin", "officer", "user" }; 
        
        foreach (var role in roles)
        {
            if (await _roleManager.FindByNameAsync(role) is null)
                await _roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        if (await _userManager.FindByNameAsync(adminEmail) == null)
        {
            User admin = new User { 
                Email = adminEmail, 
                UserName = "admin", 
                FullName = "Системный Администратор" 
            };
            IdentityResult result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await _userManager.AddToRoleAsync(admin, "admin");
        }
    }
}