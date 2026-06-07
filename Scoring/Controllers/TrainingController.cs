using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;
using Scoring.Services;
using System.Text.Json;

namespace Scoring.Controllers;

/// <summary>
/// Панель управления ML-моделью.
/// Доступна только администратору.
/// </summary>
[Authorize(Roles = "admin")]
public class TrainingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User>    _userManager;
    private readonly ModelTrainer         _trainer;

    public TrainingController(ApplicationDbContext context,
                              UserManager<User> userManager,
                              ModelTrainer trainer)
    {
        _context     = context;
        _userManager = userManager;
        _trainer     = trainer;
    }

    // ── Главная страница: датасет + история моделей ───────────────────────
    public async Task<IActionResult> Index()
    {
        var totalRecords  = await _context.TrainingRecords.CountAsync();
        var defaultCount  = await _context.TrainingRecords.CountAsync(r => r.IsDefault);
        var syntheticCount= await _context.TrainingRecords.CountAsync(r => r.SourceApplicationId == null);
        var realCount     = totalRecords - syntheticCount;

        var models = await _context.ModelCoefficients
            .OrderByDescending(m => m.TrainedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalRecords   = totalRecords;
        ViewBag.DefaultCount   = defaultCount;
        ViewBag.DefaultRate    = totalRecords > 0 ? (double)defaultCount / totalRecords : 0;
        ViewBag.SyntheticCount = syntheticCount;
        ViewBag.RealCount      = realCount;
        ViewBag.Models         = models;

        return View();
    }

    // ── Запуск обучения ────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Train()
    {
        var data = await _context.TrainingRecords.ToListAsync();

        if (data.Count < 50)
        {
            TempData["Error"] = $"Недостаточно данных ({data.Count} записей). Минимум 50.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            var mc   = _trainer.Train(data, user?.FullName ?? "admin");

            _context.ModelCoefficients.Add(mc);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Модель #{mc.Id} обучена. AUC = {mc.TrainAuc:F3}, Gini = {mc.Gini:F3}. " +
                                  $"Активируйте её чтобы применить к новым заявкам.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка обучения: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── Активировать модель ────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        // Деактивируем все
        await _context.ModelCoefficients
            .Where(m => m.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, false));

        // Активируем выбранную
        var mc = await _context.ModelCoefficients.FindAsync(id);
        if (mc == null) { TempData["Error"] = "Модель не найдена."; return RedirectToAction(nameof(Index)); }

        mc.IsActive = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Модель #{id} активирована. Все новые заявки будут считаться по ней.";
        return RedirectToAction(nameof(Index));
    }

    // ── Детали модели (скоркарта + IV) ────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var mc = await _context.ModelCoefficients.FindAsync(id);
        if (mc == null) return NotFound();

        var iv = JsonSerializer.Deserialize<Dictionary<string, double>>(mc.FeatureIvJson)
                 ?? new Dictionary<string, double>();
        ViewBag.IV = iv;
        return View(mc);
    }

    // ── Добавить реальную заявку в датасет ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToDataset(Guid applicationId, bool isDefault)
    {
        var app = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null) { TempData["Error"] = "Заявка не найдена."; return RedirectToAction(nameof(Index)); }

        bool already = await _context.TrainingRecords
            .AnyAsync(r => r.SourceApplicationId == applicationId);
        if (already) { TempData["Error"] = "Эта заявка уже добавлена в датасет."; return RedirectToAction(nameof(Index)); }

        int age = DateTime.Today.Year - app.BirthDate.Year;
        if (app.BirthDate > DateTime.Today.AddYears(-age)) age--;

        decimal totalIncome = app.IncomeAmount + app.AdditionalIncome;
        decimal rate = (app.LoanProduct?.InterestRate ?? 20m) / 100m / 12m;
        decimal monthlyPayment;
        if (rate == 0 || app.TermMonths == 0)
            monthlyPayment = app.Amount / Math.Max(app.TermMonths, 1);
        else
        {
            double r = (double)rate;
            int n = app.TermMonths;
            monthlyPayment = (decimal)((double)app.Amount * r * Math.Pow(1+r,n) / (Math.Pow(1+r,n) - 1));
        }
        decimal dti = totalIncome > 0 ? monthlyPayment / totalIncome * 100m : 100m;

        _context.TrainingRecords.Add(new TrainingRecord
        {
            Age            = age,
            Income         = totalIncome,
            ExpYears       = app.EmploymentYears,
            Dti            = dti,
            Dependents     = app.DependentsCount,
            HasRealEstate  = app.HasRealEstate,
            HasVehicle     = app.HasVehicle,
            HadDelinquency = app.HasPastDelinquencies,
            IsDefault      = isDefault,
            SourceApplicationId = app.Id,
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = isDefault
            ? "Добавлено как дефолт. Клиент не выплатил кредит."
            : "Добавлено как выплаченный кредит.";
 
        return RedirectToAction("Details", "Loan", new { id = applicationId });        
    }
}