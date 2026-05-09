using CreditScoringSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "admin")] // Доступ только администраторам
public class RulesController : Controller
{
    private readonly ApplicationDbContext _context;

    public RulesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Чтение (Read) - Список всех правил
    public async Task<IActionResult> Index()
    {
        // Сортируем сначала по параметру, затем по минимальному значению
        // Это сгруппирует логику в таблице
        var rules = await _context.ScoringRules
            .OrderBy(r => r.ParameterName)
            .ThenBy(r => r.MinValue)
            .ToListAsync();
        return View(rules);
    }

    // Создание (Create) - GET
    [HttpGet]
    public IActionResult Create() => View();

    // Создание (Create) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScoringRule rule)
    {
        if (ModelState.IsValid)
        {
            _context.Add(rule);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(rule);
    }

    // Редактирование (Update) - GET
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await _context.ScoringRules.FindAsync(id);
        if (rule == null) return NotFound();
        return View(rule);
    }

    // Редактирование (Update) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScoringRule rule)
    {
        if (id != rule.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(rule);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.ScoringRules.AnyAsync(e => e.Id == rule.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(rule);
    }

    // Удаление (Delete) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _context.ScoringRules.FindAsync(id);
        if (rule != null)
        {
            _context.ScoringRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}