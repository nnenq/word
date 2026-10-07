using System.ComponentModel.DataAnnotations;
using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Account;

public class LoginModel : AppPage
{
    readonly CinemaDb db;
    readonly IPasswordHasher<User> hasher;
    public LoginModel(CinemaDb db, IPasswordHasher<User> hasher) { this.db = db; this.hasher = hasher; }

    [BindProperty, Required(ErrorMessage = "Введите логин.")] public string Login { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Введите пароль.")] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var login = Login.Trim().ToLower();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Login.ToLower() == login);
        if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Неверный логин или пароль.");
            return Page();
        }
        if (user.IsBlocked)
        {
            ModelState.AddModelError("", "Учётная запись заблокирована. Обратитесь к администратору кинотеатра.");
            return Page();
        }
        await Auth.SignInAsync(HttpContext, user);
        Success($"Вы вошли как {user.FullName} ({user.Role.Name}).");
        return Redirect(Auth.SafeReturnUrl(ReturnUrl));
    }
}
