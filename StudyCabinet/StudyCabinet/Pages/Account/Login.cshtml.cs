using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Account;

public class LoginModel : AppPage
{
    readonly AppDb db;
    readonly IPasswordHasher<User> hasher;
    readonly ILogger<LoginModel> log;
    public LoginModel(AppDb db, IPasswordHasher<User> hasher, ILogger<LoginModel> log) { this.db = db; this.hasher = hasher; this.log = log; }

    [BindProperty, Required(ErrorMessage = "Введите логин.")] public string Login { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Введите пароль.")] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == Login.Trim());
        if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, Password) == PasswordVerificationResult.Failed)
        {
            log.LogWarning("Неудачный вход: логин {Login}, адрес {Ip}", Login, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError("", "Неверный логин или пароль.");
            return Page();
        }
        if (user.IsBlocked)
        {
            ModelState.AddModelError("", "Учётная запись заблокирована. Обратитесь к преподавателю.");
            return Page();
        }
        await Auth.SignInAsync(HttpContext, user);
        return LocalRedirect(ReturnUrl is { Length: > 1 } ? Auth.SafeReturnUrl(ReturnUrl) : user.Role == Roles.Teacher ? "/Teacher" : "/Study");
    }
}
