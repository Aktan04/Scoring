using CreditScoringSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

public class RulesController : Controller
{
    private readonly ApplicationDbContext _context;

    public RulesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Список всех активных правил
    public async Task<IActionResult> Index()
    {
        var rules = await _context.ScoringRules.OrderBy(r => r.ParameterName).ToListAsync();
        return View(rules);
    }

    // Редактирование конкретного правила (баллов)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await _context.ScoringRules.FindAsync(id);
        if (rule == null) return NotFound();
        return View(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ScoringRule rule)
    {
        if (ModelState.IsValid)
        {
            _context.Update(rule);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(rule);
    }
}