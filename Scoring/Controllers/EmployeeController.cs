using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scoring.Models;

namespace Scoring.Controllers;

[Authorize(Roles = "admin")] // Доступ только для Администратора системы
public class EmployeeController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public EmployeeController(UserManager<User> userManager, RoleManager<IdentityRole<int>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // 1. СПИСОК ВСЕХ СОТРУДНИКОВ
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        
        // Получаем роли для каждого пользователя, чтобы вывести их в таблице
        var userRoles = new Dictionary<int, string>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userRoles[user.Id] = roles.FirstOrDefault() ?? "Без роли";
        }

        ViewBag.UserRoles = userRoles;
        return View(users);
    }

    // 2. ФОРМА СОЗДАНИЯ СОТРУДНИКА (GET)
    [HttpGet]
    public IActionResult Create()
    {
        // Подготавливаем список ролей для выпадающего списка
        var rolesList = new List<SelectListItem>
        {
            new SelectListItem { Value = "maker", Text = "Мейкер (Оформитель заявок)" },
            new SelectListItem { Value = "checker", Text = "Чекер (Андеррайтер)" },
            new SelectListItem { Value = "admin", Text = "Системный администратор" }
        };
        ViewBag.Roles = rolesList;

        return View();
    }

    // 3. СОХРАНЕНИЕ СОТРУДНИКА (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegisterEmployeeViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Проверяем, нет ли уже такого email
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Сотрудник с таким Email уже существует в системе.");
            }
            else
            {
                var user = new User 
                { 
                    UserName = model.Email, 
                    Email = model.Email, 
                    FullName = model.FullName 
                };
                
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Назначаем выбранную роль
                    await _userManager.AddToRoleAsync(user, model.Role);
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
        }

        // Если ошибка валидации — возвращаем форму обратно вместе со списком ролей
        ViewBag.Roles = new List<SelectListItem>
        {
            new SelectListItem { Value = "maker", Text = "Мейкер (Оформитель заявок)" },
            new SelectListItem { Value = "checker", Text = "Чекер (Андеррайтер)" },
            new SelectListItem { Value = "admin", Text = "Системный администратор" }
        };
        return View(model);
    }
}