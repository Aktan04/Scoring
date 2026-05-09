using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

[Authorize(Roles = "officer, admin")]
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
            // .Include(a => a.Status) <--- УДАЛЕНО! Это больше не нужно
            .Include(a => a.LoanProduct)
            .Where(a => a.Status == ApplicationStatus.ManualReview) // ИСПОЛЬЗУЕМ ENUM
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
            
        return View(queue);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveManual(Guid id, bool isApproved, string comment)
    {
        var app = await _context.LoanApplications.FindAsync(id);
        if (app == null) return NotFound();

        var officer = await _userManager.GetUserAsync(User);

        // Строгая типизация вместо int
        ApplicationStatus oldStatus = app.Status;
        ApplicationStatus newStatus = isApproved ? ApplicationStatus.Approved : ApplicationStatus.Rejected; 

        // 1. Создаем запись в истории
        var historyEntry = new ApplicationHistory
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedById = officer.Id,
            Comment = comment,
            ChangedAt = DateTime.UtcNow
        };

        // 2. Обновляем заявку
        app.Status = newStatus;
        app.OfficerId = officer.Id;
        app.UpdatedAt = DateTime.UtcNow; // Фиксируем время изменения

        _context.ApplicationHistories.Add(historyEntry);
        _context.Update(app);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(VerificationQueue));
    }
    
    // Просмотр истории конкретной заявки
    public async Task<IActionResult> ApplicationDetails(Guid id)
    {
        var application = await _context.LoanApplications
            // .Include(a => a.Status) <--- УДАЛЕНО!
            .Include(a => a.LoanProduct)
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