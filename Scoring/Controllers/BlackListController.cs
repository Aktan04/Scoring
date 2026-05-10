using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "admin")] // Управление списком доступно только Админу
public class BlackListController : Controller
{
    private readonly ApplicationDbContext _context;
    public BlackListController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() 
        => View(await _context.BlackListEntries.OrderByDescending(b => b.CreatedAt).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlackListEntry entry)
    {
        if (ModelState.IsValid)
        {
            entry.IsActive = true;
            _context.Add(entry);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(entry);
    }

    // ВМЕСТО DELETE ТЕПЕРЬ TOGGLE STATUS (Soft Delete)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var entry = await _context.BlackListEntries.FindAsync(id);
        if (entry != null)
        {
            // Переключаем флаг
            entry.IsActive = !entry.IsActive; 
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}