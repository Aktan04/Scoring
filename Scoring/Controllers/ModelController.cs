using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;
using System.Text.Json;

namespace Scoring.Controllers;

/// <summary>
/// Страница «Методология модели» — читает IV из активной модели в БД.
/// Доступна любому авторизованному сотруднику.
/// </summary>
[Authorize]
public class ModelController : Controller
{
    private readonly ApplicationDbContext _context;

    public ModelController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Explain()
    {
        // Берём активную модель из БД
        var activeModel = await _context.ModelCoefficients
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.TrainedAt)
            .FirstOrDefaultAsync();

        Dictionary<string, double> iv;

        if (activeModel != null && !string.IsNullOrEmpty(activeModel.FeatureIvJson))
        {
            // IV из обученной модели
            iv = JsonSerializer.Deserialize<Dictionary<string, double>>(activeModel.FeatureIvJson)
                 ?? FallbackIV();
        }
        else
        {
            // Fallback: значения из первоначальной скоркарты
            iv = FallbackIV();
        }

        ViewBag.FeatureIV   = iv;
        ViewBag.ActiveModel = activeModel;   // null если модель ещё не обучена

        return View();
    }

    // Значения IV из нашего синтетического датасета (используется до первого обучения)
    private static Dictionary<string, double> FallbackIV() => new()
    {
        { "DTI (Долговая нагрузка)",  0.5594 },
        { "Доход",                    0.1699 },
        { "Возраст",                  0.1266 },
        { "Стаж работы",              0.0790 },
        { "Наличие недвижимости",     0.0695 },
        { "Количество иждивенцев",    0.0415 },
        { "Наличие автомобиля",       0.0098 },
    };
}