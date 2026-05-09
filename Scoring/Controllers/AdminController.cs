using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "checker")] // ИЗМЕНЕНО: Доступ имеет только Чекер (Андеррайтер)
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
            .Include(a => a.Maker) // ДОБАВЛЕНО: Подтягиваем Мейкера, чтобы Чекер видел, кто оформил анкету
            .Where(a => a.Status == ApplicationStatus.ManualReview) 
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
            
        return View(queue);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveManual(Guid id, bool isApproved, string comment)
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
            ChangedById = checkerUser.Id, // Фиксируем ID Чекера в истории
            Comment = comment,
            ChangedAt = DateTime.UtcNow
        };

        // 2. Обновляем заявку
        app.Status = newStatus;
        app.CheckerId = checkerUser.Id; // ИЗМЕНЕНО: Заменили OfficerId на CheckerId
        app.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(historyEntry);
        _context.Update(app);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(VerificationQueue));
    }
    
    // Просмотр истории конкретной заявки
    public async Task<IActionResult> ApplicationDetails(Guid id)
    {
        var application = await _context.LoanApplications
            .Include(a => a.LoanProduct)
            .Include(a => a.Maker)   // ДОБАВЛЕНО: Инфа об инициаторе
            .Include(a => a.Checker) // ДОБАВЛЕНО: Инфа о проверяющем (если уже назначен)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null) return NotFound();

        // Загружаем историю изменений для этой заявки
        var history = await _context.ApplicationHistories
            .Include(h => h.ChangedBy)
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        ViewBag.History = history;
        return View(application);
    }
}