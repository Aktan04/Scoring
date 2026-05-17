using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Scoring.Models;

namespace Scoring.Controllers;

public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AccountController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null) 
            => View(new LoginViewModel { ReturnUrl = returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Находим юзера по email до авторизации
                var user = await _userManager.FindByEmailAsync(model.Email);
        
                if (user != null)
                {
                    // Проверяем статус блокировки
                    if (!user.IsActive)
                    {
                        ModelState.AddModelError("", "Учетная запись заблокирована администратором.");
                        return View(model);
                    }

                    var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
                    if (result.Succeeded)
                    {
                        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                            return Redirect(model.ReturnUrl);
                
                        return RedirectToAction("Index", "Home");
                    }
                }
        
                ModelState.AddModelError("", "Неправильный логин и (или) пароль");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        
        [Authorize] // Доступно любому авторизованному сотруднику
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        
                if (result.Succeeded)
                {
                    // Обновляем сессию (чтобы юзера не выкинуло из системы после смены пароля)
                    await _signInManager.RefreshSignInAsync(user);
            
                    // Используем TempData для вывода уведомления об успехе на главной
                    TempData["SuccessMessage"] = "Ваш пароль был успешно изменен!";
                    return RedirectToAction("Index", "Home");
                }

                // Если старый пароль неверный или новый не подходит под политику (нет цифр и т.д.)
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }
    }