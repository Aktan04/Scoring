using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scoring.Data; 

public static class DbInitializer
{
    public static async Task SeedDataAsync(
        ApplicationDbContext context, 
        UserManager<User> userManager, 
        RoleManager<IdentityRole<int>> roleManager)
    {
        // 1. Создаем роли, если их нет
        string[] roles = { "admin", "officer", "user" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        // 2. Создаем тестовых пользователей (Офицер и Клиенты)
        var officer = await CreateUserAsync(userManager, "officer1@test.com", "Офицер Смирнов", "officer");
        var client1 = await CreateUserAsync(userManager, "client1@test.com", "Иван Иванов", "user");
        var client2 = await CreateUserAsync(userManager, "client2@test.com", "Анна Смирнова", "user");

        // 3. Заполнение кредитных продуктов
        if (!await context.LoanProducts.AnyAsync())
        {
            context.LoanProducts.AddRange(
                new LoanProduct { Name = "Потребительский Экспресс", MinAmount = 10000, MaxAmount = 250000, InterestRate = 24.5m, IsActive = true },
                new LoanProduct { Name = "Автокредит", MinAmount = 300000, MaxAmount = 3000000, InterestRate = 18.0m, IsActive = true },
                new LoanProduct { Name = "Ипотека Семейная", MinAmount = 1000000, MaxAmount = 15000000, InterestRate = 8.5m, IsActive = true },
                new LoanProduct { Name = "Кредит для Бизнеса", MinAmount = 500000, MaxAmount = 10000000, InterestRate = 15.5m, IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        // 4. Заполнение правил скоринга (ИСПОЛЬЗУЕМ ENUM)
        if (!await context.ScoringRules.AnyAsync())
        {
            context.ScoringRules.AddRange(
                new ScoringRule { Parameter = ScoringParameter.Age, MinValue = 21, MaxValue = 25, WeightPoints = 5, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Age, MinValue = 26, MaxValue = 50, WeightPoints = 15, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Age, MinValue = 51, MaxValue = 65, WeightPoints = 10, IsActive = true },
                
                new ScoringRule { Parameter = ScoringParameter.Income, MinValue = 0, MaxValue = 30000, WeightPoints = 0, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Income, MinValue = 30001, MaxValue = 80000, WeightPoints = 15, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Income, MinValue = 80001, MaxValue = 1000000, WeightPoints = 30, IsActive = true },
                
                new ScoringRule { Parameter = ScoringParameter.Experience, MinValue = 0, MaxValue = 1, WeightPoints = 0, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Experience, MinValue = 1.1m, MaxValue = 3, WeightPoints = 10, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.Experience, MinValue = 3.1m, MaxValue = 50, WeightPoints = 25, IsActive = true },
                
                new ScoringRule { Parameter = ScoringParameter.DTI, MinValue = 0, MaxValue = 30, WeightPoints = 25, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.DTI, MinValue = 30.1m, MaxValue = 50, WeightPoints = 5, IsActive = true },
                new ScoringRule { Parameter = ScoringParameter.DTI, MinValue = 50.1m, MaxValue = 100, WeightPoints = -20, IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        // 5. Генерация тестовых заявок
        if (!await context.LoanApplications.AnyAsync() && client1 != null && client2 != null)
        {
            var products = await context.LoanProducts.ToListAsync();
            var random = new Random();

            var applications = new List<LoanApplication>
            {
                // Заявки в очереди на проверку 
                CreateMockApp(client1, products[0], ApplicationStatus.ManualReview, 150000, 12, "11204199000111"),
                CreateMockApp(client2, products[1], ApplicationStatus.ManualReview, 800000, 36, "21204199000222"),
                CreateMockApp(client1, products[3], ApplicationStatus.ManualReview, 2500000, 24, "11204199000111"),

                // Одобренные заявки 
                CreateMockApp(client1, products[0], ApplicationStatus.Approved, 50000, 6, "11204199000111", officer),
                CreateMockApp(client2, products[2], ApplicationStatus.Approved, 3500000, 120, "21204199000222", officer),
                CreateMockApp(client2, products[0], ApplicationStatus.Approved, 120000, 12, "21204199000222", officer),

                // Отказанные заявки 
                CreateMockApp(client1, products[1], ApplicationStatus.Rejected, 1500000, 60, "11204199000111", officer),
                CreateMockApp(client2, products[3], ApplicationStatus.Rejected, 5000000, 36, "21204199000222", officer),

                // Заявки "На скоринге" 
                CreateMockApp(client1, products[0], ApplicationStatus.InScoring, 200000, 18, "11204199000111"),
                CreateMockApp(client2, products[0], ApplicationStatus.InScoring, 80000, 6, "21204199000222")
            };

            context.LoanApplications.AddRange(applications);
            await context.SaveChangesAsync();

            // Создаем фейковые результаты скоринга для обработанных заявок (ИСПОЛЬЗУЕМ ENUM)
            foreach (var app in applications.Where(a => a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.Rejected))
            {
                context.ScoringResults.Add(new ScoringResult
                {
                    ApplicationId = app.Id,
                    TotalScore = app.Status == ApplicationStatus.Approved ? random.Next(70, 95) : random.Next(10, 39),
                    Decision = app.Status == ApplicationStatus.Approved ? ScoringDecision.Approved : ScoringDecision.Rejected,
                    RawResponseJson = "{\"Age\": \"+15\", \"Income\": \"+30\", \"Experience\": \"+10\", \"DTI\": \"+25\"}", // Имитация JSON
                    CalculatedAt = app.CreatedAt.AddMinutes(2)
                });
            }
            await context.SaveChangesAsync();
        }
    }

    private static async Task<User> CreateUserAsync(UserManager<User> userManager, string email, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new User { UserName = email, Email = email, FullName = fullName };
            var result = await userManager.CreateAsync(user, "Test!123"); 
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
        return user;
    }

    // Обрати внимание на замену int statusId -> ApplicationStatus status
    private static LoanApplication CreateMockApp(User user, LoanProduct product, ApplicationStatus status, decimal amount, int term, string inn, User? officer = null)
    {
        var random = new Random();
        var parts = user.FullName.Split(' ');
        
        return new LoanApplication
        {
            UserId = user.Id,
            LoanProductId = product.Id,
            Status = status, // Enum
            OfficerId = officer?.Id,
            FirstName = parts.FirstOrDefault() ?? "Имя",
            LastName = parts.LastOrDefault() ?? "Фамилия",
            Inn = inn,
            PassportSerial = "ID" + random.Next(1000000, 9999999).ToString(), // Генерация паспорта
            BirthDate = new DateTime(random.Next(1970, 2000), random.Next(1, 12), random.Next(1, 28)).ToUniversalTime(),
            
            // Новые поля заполняем случайными или дефолтными данными
            MaritalStatus = (MaritalStatus)random.Next(1, 5),
            DependentsCount = random.Next(0, 4),
            EducationLevel = (EducationLevel)random.Next(1, 5),
            EmploymentType = (EmploymentType)random.Next(1, 6),
            EmployerIndustry = "IT / Финансы",
            
            IncomeAmount = random.Next(40000, 150000),
            AdditionalIncome = random.Next(0, 30000),
            EmploymentYears = random.Next(2, 15),
            
            HasRealEstate = random.Next(0, 2) == 1,
            HasVehicle = random.Next(0, 2) == 1,
            ActiveLoansCount = random.Next(0, 3),
            HasPastDelinquencies = random.Next(0, 10) > 8, // Имитируем просрочки примерно в 10% случаев
            
            Amount = amount,
            TermMonths = term,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)) 
        };
    }
}