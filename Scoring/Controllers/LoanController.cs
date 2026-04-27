using CreditScoringSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Добавлено для SelectList
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

public class LoanController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public LoanController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    
    public async Task<IActionResult> Index()
    {
        var applications = await _context.LoanApplications
            .Include(a => a.Status)
            .Include(a => a.LoanProduct) // ИЗМЕНЕНО: Подтягиваем продукт для отображения
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        ViewBag.TotalCount = applications.Count;
        ViewBag.ApprovedCount = applications.Count(a => a.StatusId == 3);
        ViewBag.RejectedCount = applications.Count(a => a.StatusId == 4);
        ViewBag.PendingCount = applications.Count(a => a.StatusId == 5); 

        return View(applications);
    }

    [Authorize(Roles = "user")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
    
        // ИЗМЕНЕНО: Передаем список кредитных продуктов во View для выпадающего списка
        var products = await _context.LoanProducts.ToListAsync();
        ViewBag.Products = new SelectList(products, "Id", "Name");

        var model = new LoanApplication
        {
            FirstName = user.FullName.Split(' ').FirstOrDefault() ?? "",
            LastName = user.FullName.Split(' ').LastOrDefault() ?? ""
        };
    
        return View(model);
    }

    [Authorize(Roles = "user")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoanApplication application)
    {
        // ИЗМЕНЕНО: Защита от деления на ноль при расчете DTI
        if (application.IncomeAmount <= 0)
        {
            ModelState.AddModelError("IncomeAmount", "Доход должен быть больше нуля");
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
        
            // ИЗМЕНЕНО: Жесткая привязка заявки к ID авторизованного клиента
            application.UserId = user.Id;

            application.StatusId = 2; // "На скоринге"
            application.CreatedAt = DateTime.UtcNow;
        
            _context.LoanApplications.Add(application);
            await _context.SaveChangesAsync();

            return RedirectToAction("ProcessScoring", new { id = application.Id });
        }
        
        // Если ошибка валидации, нужно заново заполнить ViewBag для dropdown
        ViewBag.Products = new SelectList(await _context.LoanProducts.ToListAsync(), "Id", "Name", application.LoanProductId);
        return View(application);
    }
    
    // Метод ProcessScoring остается без изменений, он считает только математику!
    [HttpGet]
    public async Task<IActionResult> ProcessScoring(Guid id)
    {
        
        var app = await _context.LoanApplications.FindAsync(id);
        var rules = await _context.ScoringRules.Where(r => r.IsActive).ToListAsync();
        
        if (app == null) return NotFound();

        int totalScore = 0;
        var details = new List<string>();

        var age = DateTime.Today.Year - app.BirthDate.Year;
        if (app.BirthDate > DateTime.Today.AddYears(-age)) age--;

        if (age < 21 || age > 65) {
            return await FinishScoring(app, 0, "Rejected", "АНТИФРОД: Возраст вне диапазона 21-65");
        }
        var isBlacklisted = await _context.BlackListEntries.AnyAsync(b => b.Inn == app.Inn);
        if (isBlacklisted)
        {
            return await FinishScoring(app, 0, "Rejected", "АНТИФРОД: Клиент находится в черном списке");
        }
        var ageRule = rules.FirstOrDefault(r => r.ParameterName == "Age" && age >= r.MinValue && age <= r.MaxValue);
        if (ageRule != null) {
            totalScore += ageRule.WeightPoints;
            details.Add($"Возраст ({age} лет): +{ageRule.WeightPoints} баллов");
        }

        var incomeRule = rules.FirstOrDefault(r => r.ParameterName == "Income" && app.IncomeAmount >= r.MinValue && app.IncomeAmount <= r.MaxValue);
        if (incomeRule != null) {
            totalScore += incomeRule.WeightPoints;
            details.Add($"Доход ({app.IncomeAmount} сом): +{incomeRule.WeightPoints} баллов");
        }

        var expRule = rules.FirstOrDefault(r => r.ParameterName == "Experience" && app.EmploymentYears >= r.MinValue && app.EmploymentYears <= r.MaxValue);
        if (expRule != null) {
            totalScore += expRule.WeightPoints;
            details.Add($"Стаж ({app.EmploymentYears} лет): +{expRule.WeightPoints} баллов");
        }

        decimal monthlyPayment = app.Amount / app.TermMonths;
        decimal dti = (monthlyPayment / app.IncomeAmount) * 100;
        var dtiRule = rules.FirstOrDefault(r => r.ParameterName == "DTI" && dti >= r.MinValue && dti <= r.MaxValue);
        if (dtiRule != null) {
            totalScore += dtiRule.WeightPoints;
            details.Add($"Нагрузка DTI ({dti:F1}%): {dtiRule.WeightPoints} баллов");
        }

        string decision;
        if (totalScore >= 70) decision = "Approved";       
        else if (totalScore >= 40) decision = "ManualReview"; 
        else decision = "Rejected";                        

        return await FinishScoring(app, totalScore, decision, string.Join("; ", details));
    }

    private async Task<IActionResult> FinishScoring(LoanApplication app, int score, string decision, string log)
    {
        var result = new ScoringResult {
            ApplicationId = app.Id,
            TotalScore = score,
            Decision = decision,
            RawResponseJson = log,
            CalculatedAt = DateTime.UtcNow
        };

        app.StatusId = (decision == "Approved") ? 3 : (decision == "Rejected" ? 4 : 5);
        
        _context.ScoringResults.Add(result);
        _context.Update(app);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", new { id = app.Id });
    }
    
    [Authorize(Roles = "officer, admin")]
    public async Task<IActionResult> VerificationQueue()
    {
        var queue = await _context.LoanApplications
            .Include(a => a.Status)
            .Include(a => a.LoanProduct) // ИЗМЕНЕНО: Офицер должен видеть название продукта
            .Where(a => a.StatusId == 5) 
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        return View(queue);
    }

    [HttpPost]
    [Authorize(Roles = "officer, admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveManual(Guid id, bool isApproved, string comment)
    {
        var app = await _context.LoanApplications.FindAsync(id);
        if (app == null) return NotFound();

        app.StatusId = isApproved ? 3 : 4; 
    
        var result = await _context.ScoringResults.FirstOrDefaultAsync(r => r.ApplicationId == id);
        if (result != null)
        {
            var officer = await _userManager.GetUserAsync(User); // Получаем офицера
            result.RawResponseJson += $"; РУЧНОЕ РЕШЕНИЕ ({officer.FullName}): {(isApproved ? "Одобрено" : "Отказ")}; Причина: {comment}";
            
            // ИЗМЕНЕНО: Привязываем офицера к заявке
            app.OfficerId = officer.Id;
        }

        _context.Update(app);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(VerificationQueue));
    }
    
    [Authorize(Roles = "admin, officer")]
    public async Task<IActionResult> Dashboard()
    {
        var stats = await _context.LoanApplications
            .GroupBy(a => a.Status.Name)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // ИЗМЕНЕНО: Теперь группируем по названию связанного продукта, а не по строке
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
    
    public async Task<IActionResult> Details(Guid id)
    {
        var result = await _context.ScoringResults
            .Include(r => r.Application)
                .ThenInclude(a => a.Status)
            .Include(r => r.Application)
                .ThenInclude(a => a.LoanProduct) // ИЗМЕНЕНО: Добавили Include для продукта
            .FirstOrDefaultAsync(r => r.ApplicationId == id);

        if (result == null) 
        {
            var application = await _context.LoanApplications
                .Include(a => a.Status)
                .Include(a => a.LoanProduct) // ИЗМЕНЕНО
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (application == null) return NotFound();
            return View("AppDetailsOnly", application);
        }

        return View(result);
    }
    
    [Authorize(Roles = "user")]
    public async Task<IActionResult> MyApplications()
    {
        var currentUser = await _userManager.GetUserAsync(User);
    
        // ИЗМЕНЕНО: Надежный поиск по Foreign Key и подтягивание продукта
        var myApps = await _context.LoanApplications
            .Include(a => a.Status)
            .Include(a => a.LoanProduct)
            .Where(a => a.UserId == currentUser.Id) 
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(myApps);
    }
    
    [Authorize]
    public async Task<IActionResult> DownloadContract(Guid id)
    {
        var result = await _context.ScoringResults
            .Include(r => r.Application)
                .ThenInclude(a => a.LoanProduct) // ИЗМЕНЕНО: Чтобы получить название
            .FirstOrDefaultAsync(r => r.ApplicationId == id);

        if (result == null || result.Decision != "Approved")
        {
            return BadRequest("Договор доступен только для одобренных заявок.");
        }

        // ИЗМЕНЕНО: Вызываем result.Application.LoanProduct.Name
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
        
        Документ сформирован автоматически системой скоринга.";

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/plain", $"Contract_{id.ToString().Substring(0, 8)}.txt"); 
    }
}