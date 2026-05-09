using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "admin")]
public class ProductController : Controller
{
    private readonly ApplicationDbContext _context;
    
    public ProductController(ApplicationDbContext context) => _context = context;

    // Чтение (Read) - Список продуктов
    public async Task<IActionResult> Index()
    {
        var products = await _context.LoanProducts.OrderBy(p => p.Name).ToListAsync();
        return View(products);
    }

    // Создание (Create) - GET
    [HttpGet]
    public IActionResult Create() => View();

    // Создание (Create) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoanProduct product)
    {
        if (ModelState.IsValid)
        {
            _context.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // Редактирование (Update) - GET
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.LoanProducts.FindAsync(id);
        if (product == null) return NotFound();
        
        return View(product);
    }

    // Редактирование (Update) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LoanProduct product)
    {
        if (id != product.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(product);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.LoanProducts.AnyAsync(e => e.Id == product.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // Удаление (Delete) - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // Не удаляем продукт, если по нему уже есть заявки, чтобы не сломать базу (Foreign Key)
        var hasApplications = await _context.LoanApplications.AnyAsync(a => a.LoanProductId == id);
        if (hasApplications)
        {
            // Если есть связи, лучше просто сделать продукт неактивным
            var product = await _context.LoanProducts.FindAsync(id);
            if (product != null)
            {
                product.IsActive = false;
                _context.Update(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        var entryToDelete = await _context.LoanProducts.FindAsync(id);
        if (entryToDelete != null)
        {
            _context.LoanProducts.Remove(entryToDelete);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}