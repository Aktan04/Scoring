using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

[Authorize(Roles = "admin, officer")]
public class BlacklistController : Controller
{
    private readonly ApplicationDbContext _context;
    public BlacklistController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.BlackListEntries.ToListAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlackListEntry entry)
    {
        if (ModelState.IsValid)
        {
            _context.Add(entry);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(entry);
    }
}