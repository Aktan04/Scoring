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
        // 1. Создаем только внутренние банковские роли (Maker-Checker)
        string[] roles = { "admin", "maker", "checker" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        // 2. Создаем тестовых сотрудников банка
        var admin = await CreateUserAsync(userManager, "admin@bank.com", "Администратор Системы", "admin");
        var maker = await CreateUserAsync(userManager, "maker@bank.com", "Мейкер (Оформитель) Иванов", "maker");
        var checker = await CreateUserAsync(userManager, "checker@bank.com", "Чекер (Андеррайтер) Смирнов", "checker");

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

        // 4. Заполнение правил скоринга (Матрица)
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

        // 5. Генерация тестовых заявок (Создаются Мейкером)
        if (!await context.LoanApplications.AnyAsync() && maker != null)
        {
            var products = await context.LoanProducts.ToListAsync();
            var random = new Random();

            var applications = new List<LoanApplication>
            {
                // Заявки в очереди на проверку (Чекера еще нет, так как решение не принято)
                CreateMockApp(maker, products[0], ApplicationStatus.ManualReview, 150000, 12, "11204199000111", "Нурлан", "Асанов"),
                CreateMockApp(maker, products[1], ApplicationStatus.ManualReview, 800000, 36, "21204199000222", "Айнура", "Садыкова"),
                CreateMockApp(maker, products[3], ApplicationStatus.ManualReview, 2500000, 24, "11204199000333", "Иван", "Иванов"),

                // Одобренные заявки (Проверены Чекером)
                CreateMockApp(maker, products[0], ApplicationStatus.Approved, 50000, 6, "11204199000444", "Елена", "Смирнова", checker),
                CreateMockApp(maker, products[2], ApplicationStatus.Approved, 3500000, 120, "21204199000555", "Бакыт", "Керимов", checker),
                CreateMockApp(maker, products[0], ApplicationStatus.Approved, 120000, 12, "21204199000666", "Азамат", "Токтогулов", checker),

                // Отказанные заявки (Отклонены Чекером)
                CreateMockApp(maker, products[1], ApplicationStatus.Rejected, 1500000, 60, "11204199000777", "Мария", "Ким", checker),
                CreateMockApp(maker, products[3], ApplicationStatus.Rejected, 5000000, 36, "21204199000888", "Руслан", "Батыров", checker),

                // Заявки "На скоринге" (Только что созданы Мейкером)
                CreateMockApp(maker, products[0], ApplicationStatus.InScoring, 200000, 18, "11204199000999", "Гульзат", "Маматова"),
                CreateMockApp(maker, products[0], ApplicationStatus.InScoring, 80000, 6, "21204199000000", "Дмитрий", "Волков")
            };

            context.LoanApplications.AddRange(applications);
            await context.SaveChangesAsync();

            // Создаем фейковые результаты скоринга для обработанных заявок
            foreach (var app in applications.Where(a => a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.Rejected))
            {
                context.ScoringResults.Add(new ScoringResult
                {
                    ApplicationId = app.Id,
                    TotalScore = app.Status == ApplicationStatus.Approved ? random.Next(70, 95) : random.Next(10, 39),
                    Decision = app.Status == ApplicationStatus.Approved ? ScoringDecision.Approved : ScoringDecision.Rejected,
                    RawResponseJson = "{\"Возраст\": \"+15\", \"Доход\": \"+30\", \"Стаж\": \"+10\", \"DTI\": \"+25\"}", 
                    CalculatedAt = app.CreatedAt.AddMinutes(2)
                });
            }
            await context.SaveChangesAsync();
        }
        
        // 6. Загрузка синтетического датасета (1000 записей) для обучения модели
        if (!await context.TrainingRecords.AnyAsync())
        {
            // Читаем JSON с синтетическими данными
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "training_seed.json");
    
            if (File.Exists(jsonPath))
            {
                var json = await File.ReadAllTextAsync(jsonPath);
                var records = System.Text.Json.JsonSerializer.Deserialize<List<TrainingRecordSeed>>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
 
                if (records != null)
                {
                    var dbRecords = records.Select(r => new TrainingRecord
                    {
                        Age            = r.Age,
                        Income         = r.Income,
                        ExpYears       = r.ExpYears,
                        Dti            = (decimal)r.Dti,
                        Dependents     = r.Dependents,
                        HasRealEstate  = r.HasRealEstate,
                        HasVehicle     = r.HasVehicle,
                        HadDelinquency = r.HadDelinquency,
                        IsDefault      = r.IsDefault,
                        SourceApplicationId = null   // синтетические
                    }).ToList();
 
                    context.TrainingRecords.AddRange(dbRecords);
                    await context.SaveChangesAsync();
            
                }
            }
        }
    }

    // Вспомогательный метод создания пользователя (сотрудника)
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

    // Вспомогательный метод для генерации заявок от имени Мейкера
    private static LoanApplication CreateMockApp(
        User maker, 
        LoanProduct product, 
        ApplicationStatus status, 
        decimal amount, 
        int term, 
        string inn, 
        string firstName, 
        string lastName, 
        User? checker = null)
    {
        var random = new Random();
        
        return new LoanApplication
        {
            // ПРИВЯЗКА К СОТРУДНИКАМ БАНКА
            MakerId = maker.Id,
            CheckerId = checker?.Id,
            
            LoanProductId = product.Id,
            Status = status,
            
            // ДАННЫЕ КЛИЕНТА (Вбиваются вручную)
            FirstName = firstName,
            LastName = lastName,
            Inn = inn,
            PassportSerial = "ID" + random.Next(1000000, 9999999).ToString(),
            BirthDate = new DateTime(random.Next(1970, 2000), random.Next(1, 12), random.Next(1, 28)).ToUniversalTime(),
            
            MaritalStatus = (MaritalStatus)random.Next(1, 5),
            DependentsCount = random.Next(0, 4),
            EducationLevel = (EducationLevel)random.Next(1, 5),
            EmploymentType = (EmploymentType)random.Next(1, 6),
            EmployerIndustry = "IT / Торговля",
            
            IncomeAmount = random.Next(40000, 150000),
            AdditionalIncome = random.Next(0, 30000),
            EmploymentYears = random.Next(2, 15),
            
            HasRealEstate = random.Next(0, 2) == 1,
            HasVehicle = random.Next(0, 2) == 1,
            ActiveLoansCount = random.Next(0, 3),
            HasPastDelinquencies = random.Next(0, 10) > 8, 
            
            Amount = amount,
            TermMonths = term,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    private class TrainingRecordSeed
    {
        public int     Age           { get; set; }
        public decimal Income        { get; set; }
        public int     ExpYears      { get; set; }
        public double  Dti           { get; set; }
        public int     Dependents    { get; set; }
        public bool    HasRealEstate { get; set; }
        public bool    HasVehicle    { get; set; }
        public bool    HadDelinquency{ get; set; }
        public bool    IsDefault     { get; set; }
    }
}