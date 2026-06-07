using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "checker")] // Доступ имеет только Чекер (Андеррайтер)
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Очередь на ручную проверку
    public async Task<IActionResult> VerificationQueue()
    {
        var queue = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .Include(a => a.Maker) // Подтягиваем Мейкера, чтобы Чекер видел, кто оформил анкету
            .Where(a => a.Status == ApplicationStatus.ManualReview) 
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
            
        return View(queue);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ДОБАВЛЕН ПАРАМЕТР addToBlacklist
    public async Task<IActionResult> ApproveManual(Guid id, bool isApproved, string comment, bool addToBlacklist = false)
    {
        var app = await _context.LoanApplications.FindAsync(id);
        if (app == null) return NotFound();

        var checkerUser = await _userManager.GetUserAsync(User);

        ApplicationStatus oldStatus = app.Status;
        ApplicationStatus newStatus = isApproved ? ApplicationStatus.Approved : ApplicationStatus.Rejected; 

        // 1. Создаем запись в истории
        var historyEntry = new ApplicationHistory
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedById = checkerUser.Id, // Фиксируем ID Чекера
            Comment = comment,
            ChangedAt = DateTime.UtcNow
        };

        // 2. Обновляем заявку
        app.Status = newStatus;
        app.CheckerId = checkerUser.Id; 
        app.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(historyEntry);
        _context.Update(app);

        // 3. ЛОГИКА АНТИФРОДА: Если отказано и стоит галочка
        if (!isApproved && addToBlacklist)
        {
            bool alreadyBlacklisted = await _context.BlackListEntries.AnyAsync(b => b.Inn == app.Inn && b.IsActive);
            if (!alreadyBlacklisted)
            {
                _context.BlackListEntries.Add(new BlackListEntry
                {
                    Inn = app.Inn,
                    Reason = $"[Авто-добавление] Отказ по заявке №{app.Id.ToString().Substring(0,8)}. Андеррайтер: {checkerUser.FullName}. Комментарий: {comment}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Loan", new { id = app.Id });
    }
    
    // Просмотр истории и деталей конкретной заявки
    public async Task<IActionResult> ApplicationDetails(Guid id)
    {
        var application = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .Include(a => a.Maker)   // Инфа об инициаторе
            .Include(a => a.Checker) // Инфа о проверяющем (если уже назначен)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null) return NotFound();

        // Загружаем историю изменений
        var history = await _context.ApplicationHistories
            .Include(h => h.ChangedBy)
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        ViewBag.History = history;
        return View(application);
    }
}