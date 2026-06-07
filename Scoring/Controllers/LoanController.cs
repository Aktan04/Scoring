using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;
using System.Text.Json;
using Scoring.Services;

namespace Scoring.Controllers;

[Authorize] // Защищаем контроллер на базовом уровне
public class LoanController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ScoringService _scoreService;
    private readonly ContractPdfService _pdf;


    public LoanController(ApplicationDbContext context, UserManager<User> userManager, ScoringService scoreService, ContractPdfService pdf)
    {
        _pdf = pdf;
        _context = context;
        _userManager = userManager;
        _scoreService = scoreService;
    }
    
    // ПРОСМОТР ВСЕХ ЗАЯВОК: Доступно Мейкеру и Чекеру (Админ не видит)
    [Authorize(Roles = "maker, checker, admin")]
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
            if (application.BirthDate.Kind != DateTimeKind.Utc)
                application.BirthDate = DateTime.SpecifyKind(application.BirthDate, DateTimeKind.Utc);
            
            var makerUser = await _userManager.GetUserAsync(User);
        
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
    
    [Authorize(Roles = "maker")]
    [HttpGet]
    public async Task<IActionResult> ProcessScoring(Guid id)
    {
        var app = await _context.LoanApplications
            .Include(a => a.LoanProduct)   // Нужен для аннуитетного расчёта DTI
            .FirstOrDefaultAsync(a => a.Id == id);
 
        if (app == null) return NotFound();
 
        // ── 1. ЖЁСТКИЕ АНТИФРОД-ФИЛЬТРЫ (Hard Cutoff) ──────────────────────────
        // Эти проверки срабатывают ДО скоринга: клиент получает отказ немедленно,
        // балл не рассчитывается.
 
        int age = DateTime.Today.Year - app.BirthDate.Year;
        if (app.BirthDate > DateTime.Today.AddYears(-age)) age--;
 
        if (age < 21 || age > 65)
            return await FinishScoring(app, 0, ScoringDecision.Rejected,
                """{"antiFraud":"Возраст вне допустимого диапазона 21–65 лет"}""");
 
        if (app.HasPastDelinquencies)
            return await FinishScoring(app, 0, ScoringDecision.Rejected,
                """{"antiFraud":"Наличие просрочек в кредитном бюро"}""");
 
        var isBlacklisted = await _context.BlackListEntries
            .AnyAsync(b => b.Inn == app.Inn && b.IsActive);
        if (isBlacklisted)
            return await FinishScoring(app, 0, ScoringDecision.Rejected,
                """{"antiFraud":"Клиент в чёрном списке"}""");
 
        // ── 2. WOE-СКОРКАРТА ────────────────────────────────────────────────────
        var result = _scoreService.Calculate(app);
 
        return await FinishScoring(app, result.TotalScore, result.Decision, result.JsonLog);
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
    
    [Authorize(Roles = "admin, checker, maker")]
    public async Task<IActionResult> Dashboard()
    {
        var all = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .ToListAsync();

        var scores = await _context.ScoringResults.ToListAsync();

        // Статусы
        var statuses = all.GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToList();

        // Продукты
        var types = all
            .GroupBy(a => a.LoanProduct?.Name ?? "Без продукта")
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        // Скоринговые баллы по бакетам (только реальные заявки со скорингом)
        var realScores = scores.Where(s => s.TotalScore > 0).Select(s => s.TotalScore).ToList();
        var scoreBuckets = new[]
        {
            new { Label = "450-499", Count = realScores.Count(s => s >= 450 && s < 500) },
            new { Label = "500-539", Count = realScores.Count(s => s >= 500 && s < 540) },
            new { Label = "540-559", Count = realScores.Count(s => s >= 540 && s < 560) },
            new { Label = "560-579", Count = realScores.Count(s => s >= 560 && s < 580) },
            new { Label = "580-599", Count = realScores.Count(s => s >= 580 && s < 600) },
            new { Label = "600+",    Count = realScores.Count(s => s >= 600) },
        };

        // Динамика по дням (последние 14 дней)
        var since = DateTime.UtcNow.AddDays(-13).Date;
        var byDay = all
            .Where(a => a.CreatedAt.Date >= since)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key.ToString("dd.MM"), Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToList();

        // Заполняем пропущенные дни нулями
        var days    = Enumerable.Range(0, 14).Select(i => DateTime.UtcNow.AddDays(-13 + i).Date).ToList();
        var dayMap  = byDay.ToDictionary(d => d.Date, d => d.Count);
        var dayLabels = days.Select(d => d.ToString("dd.MM")).ToList();
        var dayData   = days.Select(d => dayMap.GetValueOrDefault(d.ToString("dd.MM"), 0)).ToList();

        // KPI
        int total      = all.Count;
        int approved   = all.Count(a => a.Status == ApplicationStatus.Approved);
        int rejected   = all.Count(a => a.Status == ApplicationStatus.Rejected);
        int manual     = all.Count(a => a.Status == ApplicationStatus.ManualReview);
        double approvalRate = total > 0 ? (double)approved / total * 100 : 0;
        double avgScore     = realScores.Count > 0 ? realScores.Average() : 0;
        decimal totalAmount = all.Where(a => a.Status == ApplicationStatus.Approved).Sum(a => a.Amount);

        ViewBag.Total        = total;
        ViewBag.Approved     = approved;
        ViewBag.Rejected     = rejected;
        ViewBag.Manual       = manual;
        ViewBag.ApprovalRate = approvalRate.ToString("F1");
        ViewBag.AvgScore     = avgScore.ToString("F0");
        ViewBag.TotalAmount  = totalAmount;

        ViewBag.StatusLabels = statuses.Select(s => s.Status).ToArray();
        ViewBag.StatusData   = statuses.Select(s => s.Count).ToArray();
        ViewBag.TypeLabels   = types.Select(t => t.Type).ToArray();
        ViewBag.TypeData     = types.Select(t => t.Count).ToArray();
        ViewBag.ScoreLabels  = scoreBuckets.Select(b => b.Label).ToArray();
        ViewBag.ScoreData    = scoreBuckets.Select(b => b.Count).ToArray();
        ViewBag.DayLabels    = dayLabels.ToArray();
        ViewBag.DayData      = dayData.ToArray();

        return View();
    }
    
    [Authorize(Roles = "maker, checker, admin")]
    public async Task<IActionResult> Details(Guid id)
    {
        var app = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .Include(a => a.Maker)
            .Include(a => a.Checker)
            .FirstOrDefaultAsync(a => a.Id == id);
 
        if (app == null) return NotFound();
 
        var scoring = await _context.ScoringResults
            .FirstOrDefaultAsync(r => r.ApplicationId == id);
 
        var history = await _context.ApplicationHistories
            .Include(h => h.ChangedBy)
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
 
        var alreadyInDataset = await _context.TrainingRecords
            .AnyAsync(r => r.SourceApplicationId == id);
 
        ViewBag.Application      = app;
        ViewBag.ScoringResult    = scoring;
        ViewBag.History          = history;
        ViewBag.AlreadyInDataset = alreadyInDataset;
 
        return View("UniversalDetails");
    }
    
    [Authorize(Roles = "maker, checker")]
    public async Task<IActionResult> DownloadContract(Guid id)
    {
        var scoring = await _context.ScoringResults
            .Include(r => r.Application)
            .ThenInclude(a => a.LoanProduct)
            .FirstOrDefaultAsync(r => r.ApplicationId == id);
 
        if (scoring == null || scoring.Decision != ScoringDecision.Approved)
            return BadRequest("Договор доступен только для одобренных заявок.");
 
        var bytes = _pdf.Generate(scoring.Application, scoring);
 
        return File(
            bytes,
            "application/pdf",
            $"Contract_{id.ToString().Substring(0, 8).ToUpper()}.pdf"
        );
    }
}