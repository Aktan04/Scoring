using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;
using System.Text.Json;

namespace Scoring.Controllers;

[Authorize] // Защищаем контроллер на базовом уровне
public class LoanController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public LoanController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    
    // ПРОСМОТР ВСЕХ ЗАЯВОК: Доступно Мейкеру и Чекеру (Админ не видит)
    [Authorize(Roles = "maker, checker")]
    public async Task<IActionResult> Index()
    {
        var applications = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .Include(a => a.Maker) // Подтягиваем инфу о том, какой сотрудник создал
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        ViewBag.TotalCount = applications.Count;
        ViewBag.ApprovedCount = applications.Count(a => a.Status == ApplicationStatus.Approved);
        ViewBag.RejectedCount = applications.Count(a => a.Status == ApplicationStatus.Rejected);
        ViewBag.PendingCount = applications.Count(a => a.Status == ApplicationStatus.ManualReview); 

        return View(applications);
    }

    // СОЗДАНИЕ ЗАЯВКИ (GET): Доступно ТОЛЬКО Мейкеру
    [Authorize(Roles = "maker")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var products = await _context.LoanProducts.Where(p => p.IsActive).ToListAsync();
        ViewBag.Products = new SelectList(products, "Id", "Name");

        // Отправляем пустую модель, Мейкер вбивает все данные клиента вручную
        return View(new LoanApplication());
    }

    // СОЗДАНИЕ ЗАЯВКИ (POST): Доступно ТОЛЬКО Мейкеру
    [Authorize(Roles = "maker")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoanApplication application)
    {
        if (application.IncomeAmount <= 0)
        {
            ModelState.AddModelError("IncomeAmount", "Основной доход должен быть больше нуля");
        }

        if (ModelState.IsValid)
        {
            var makerUser = await _userManager.GetUserAsync(User);
        
            // ПРИВЯЗЫВАЕМ ЗАЯВКУ К СОТРУДНИКУ (Maker)
            application.MakerId = makerUser.Id;
            application.Status = ApplicationStatus.InScoring; 
            application.CreatedAt = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;
        
            _context.LoanApplications.Add(application);
            await _context.SaveChangesAsync();

            return RedirectToAction("ProcessScoring", new { id = application.Id });
        }
        
        var products = await _context.LoanProducts.Where(p => p.IsActive).ToListAsync();
        ViewBag.Products = new SelectList(products, "Id", "Name", application.LoanProductId);
        return View(application);
    }
    
    // АВТОМАТИЧЕСКИЙ СКОРИНГ: Срабатывает сразу после создания Мейкером
    [Authorize(Roles = "maker")]
    [HttpGet]
    public async Task<IActionResult> ProcessScoring(Guid id)
    {
        var app = await _context.LoanApplications.FindAsync(id);
        if (app == null) return NotFound();

        var rules = await _context.ScoringRules.Where(r => r.IsActive).ToListAsync();
        int totalScore = 0;
        var details = new Dictionary<string, string>(); 

        // 1. ЖЕСТКИЙ АНТИФРОД КОНТРОЛЬ
        var age = DateTime.Today.Year - app.BirthDate.Year;
        if (app.BirthDate > DateTime.Today.AddYears(-age)) age--;

        if (age < 21 || age > 65) {
            return await FinishScoring(app, 0, ScoringDecision.Rejected, "{\"Антифрод\": \"Возраст вне диапазона 21-65\"}");
        }
        
        if (app.HasPastDelinquencies) {
            return await FinishScoring(app, 0, ScoringDecision.Rejected, "{\"Антифрод\": \"Наличие открытых просрочек в Кредитном Бюро\"}");
        }

        var isBlacklisted = await _context.BlackListEntries.AnyAsync(b => b.Inn == app.Inn);
        if (isBlacklisted) {
            return await FinishScoring(app, 0, ScoringDecision.Rejected, "{\"Антифрод\": \"Клиент находится в черном списке\"}");
        }

        // 2. МАТЕМАТИКА ПО ENUM-ПРАВИЛАМ
        
        var ageRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.Age && age >= r.MinValue && age <= r.MaxValue);
        if (ageRule != null) {
            totalScore += ageRule.WeightPoints;
            details.Add("Возраст", $"{age} лет ({ageRule.WeightPoints} б.)");
        }

        var totalIncome = app.IncomeAmount + app.AdditionalIncome;
        var incomeRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.Income && totalIncome >= r.MinValue && totalIncome <= r.MaxValue);
        if (incomeRule != null) {
            totalScore += incomeRule.WeightPoints;
            details.Add("Доход", $"{totalIncome} сом ({incomeRule.WeightPoints} б.)");
        }

        var expRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.Experience && app.EmploymentYears >= r.MinValue && app.EmploymentYears <= r.MaxValue);
        if (expRule != null) {
            totalScore += expRule.WeightPoints;
            details.Add("Стаж", $"{app.EmploymentYears} лет ({expRule.WeightPoints} б.)");
        }

        decimal monthlyPayment = app.Amount / app.TermMonths;
        decimal dti = totalIncome > 0 ? (monthlyPayment / totalIncome) * 100 : 100;
        var dtiRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.DTI && dti >= r.MinValue && dti <= r.MaxValue);
        if (dtiRule != null) {
            totalScore += dtiRule.WeightPoints;
            details.Add("DTI", $"{dti:F1}% ({dtiRule.WeightPoints} б.)");
        }

        var depRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.DependentsCount && app.DependentsCount >= r.MinValue && app.DependentsCount <= r.MaxValue);
        if (depRule != null) {
            totalScore += depRule.WeightPoints;
            details.Add("Иждивенцы", $"{app.DependentsCount} чел. ({depRule.WeightPoints} б.)");
        }

        var realEstateRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.HasRealEstate && (app.HasRealEstate ? 1 : 0) >= r.MinValue && (app.HasRealEstate ? 1 : 0) <= r.MaxValue);
        if (realEstateRule != null) {
            totalScore += realEstateRule.WeightPoints;
            details.Add("Недвижимость", app.HasRealEstate ? $"Да ({realEstateRule.WeightPoints} б.)" : "Нет");
        }

        var vehicleRule = rules.FirstOrDefault(r => r.Parameter == ScoringParameter.HasVehicle && (app.HasVehicle ? 1 : 0) >= r.MinValue && (app.HasVehicle ? 1 : 0) <= r.MaxValue);
        if (vehicleRule != null) {
            totalScore += vehicleRule.WeightPoints;
            details.Add("Автомобиль", app.HasVehicle ? $"Да ({vehicleRule.WeightPoints} б.)" : "Нет");
        }

        // 3. ПРИНЯТИЕ РЕШЕНИЯ
        ScoringDecision decision;
        if (totalScore >= 70) decision = ScoringDecision.Approved;       
        else if (totalScore >= 40) decision = ScoringDecision.ManualReview; 
        else decision = ScoringDecision.Rejected;                        

        string jsonLog = JsonSerializer.Serialize(details); 

        return await FinishScoring(app, totalScore, decision, jsonLog);
    }

    private async Task<IActionResult> FinishScoring(LoanApplication app, int score, ScoringDecision decision, string jsonLog)
    {
        var result = new ScoringResult {
            ApplicationId = app.Id,
            TotalScore = score,
            Decision = decision,
            RawResponseJson = jsonLog,
            CalculatedAt = DateTime.UtcNow
        };

        app.Status = decision switch
        {
            ScoringDecision.Approved => ApplicationStatus.Approved,
            ScoringDecision.Rejected => ApplicationStatus.Rejected,
            _ => ApplicationStatus.ManualReview
        };
        
        app.UpdatedAt = DateTime.UtcNow;

        _context.ScoringResults.Add(result);
        _context.Update(app);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", new { id = app.Id });
    }
    
    // АНАЛИТИКА: Оставляем доступ Админу (для бизнес-отчетов) и Чекеру
    [Authorize(Roles = "admin, checker, maker")]
    public async Task<IActionResult> Dashboard()
    {
        var statsRaw = await _context.LoanApplications
            .GroupBy(a => a.Status)
            .Select(g => new { StatusEnum = g.Key, Count = g.Count() })
            .ToListAsync();

        var stats = statsRaw.Select(s => new { 
            Status = s.StatusEnum.ToString(), 
            Count = s.Count 
        }).ToList();

        var types = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .GroupBy(a => a.LoanProduct.Name)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.StatusLabels = stats.Select(s => s.Status).ToArray();
        ViewBag.StatusData = stats.Select(s => s.Count).ToArray();
    
        ViewBag.TypeLabels = types.Select(t => t.Type).ToArray();
        ViewBag.TypeData = types.Select(t => t.Count).ToArray();

        return View();
    }
    
    // ДЕТАЛИ ЗАЯВКИ: Мейкер и Чекер (Админ не имеет доступа к перс. данным клиентов)
    [Authorize(Roles = "maker, checker")]
    public async Task<IActionResult> Details(Guid id)
    {
        var result = await _context.ScoringResults
            .Include(r => r.Application)
                .ThenInclude(a => a.LoanProduct)
            .FirstOrDefaultAsync(r => r.ApplicationId == id);

        if (result == null) 
        {
            var application = await _context.LoanApplications
                .Include(a => a.LoanProduct)
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (application == null) return NotFound();
            return View("AppDetailsOnly", application);
        }

        return View(result);
    }
    
    // ДОГОВОР: Мейкер распечатывает договор клиенту
    [Authorize(Roles = "maker, checker")]
    public async Task<IActionResult> DownloadContract(Guid id)
    {
        var result = await _context.ScoringResults
            .Include(r => r.Application)
                .ThenInclude(a => a.LoanProduct) 
            .FirstOrDefaultAsync(r => r.ApplicationId == id);

        if (result == null || result.Decision != ScoringDecision.Approved)
        {
            return BadRequest("Договор доступен только для одобренных заявок.");
        }

        var content = $@"
        ИНДИВИДУАЛЬНЫЕ УСЛОВИЯ КРЕДИТНОГО ДОГОВОРА № {result.ApplicationId.ToString().Substring(0, 8)}
        ------------------------------------------------------------
        Дата: {DateTime.Now.ToShortDateString()}
        Кредитор: ОАО 'Кыргызский Технический Банк'
        Заемщик: {result.Application.LastName} {result.Application.FirstName}
        ИНН Заемщика: {result.Application.Inn}
        
        1. Сумма кредита: {result.Application.Amount:N0} сом.
        2. Срок кредита: {result.Application.TermMonths} месяцев.
        3. Цель: {result.Application.LoanProduct.Name}. 
        4. Результат скоринга: {result.TotalScore} баллов.
        
        Документ сформирован автоматически системой скоринга. 
        Необходима подпись ответственного сотрудника (Maker).";

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/plain", $"Contract_{id.ToString().Substring(0, 8)}.txt"); 
    }
}