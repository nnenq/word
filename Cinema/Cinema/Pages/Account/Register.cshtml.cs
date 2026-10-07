using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Account;

public class RegisterModel : AppPage
{
    readonly CinemaDb db;
    readonly IPasswordHasher<User> hasher;
    public RegisterModel(CinemaDb db, IPasswordHasher<User> hasher) { this.db = db; this.hasher = hasher; }

    [BindProperty] public RegisterInput Input { get; set; } = new();

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/Cabinet") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Login = Input.Login?.Trim() ?? "";
        Input.Email = Input.Email?.Trim().ToLowerInvariant() ?? "";
        Input.FullName = Input.FullName?.Trim() ?? "";
        if (!ModelState.IsValid) return Page();

        if (await db.Users.AnyAsync(u => u.Login.ToLower() == Input.Login.ToLower()))
            ModelState.AddModelError("Input.Login", "Этот логин уже занят. Придумайте другой.");
        if (await db.Users.AnyAsync(u => u.Email == Input.Email))
            ModelState.AddModelError("Input.Email", "Пользователь с такой почтой уже зарегистрирован.");
        if (!ModelState.IsValid) return Page();

        var role = await db.Roles.FirstAsync(r => r.Code == RoleCodes.Viewer);
        var user = new User { Login = Input.Login, FullName = Input.FullName, Email = Input.Email, Role = role };
        user.PasswordHash = hasher.HashPassword(user, Input.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await Auth.SignInAsync(HttpContext, user);
        Success($"Регистрация прошла успешно. Добро пожаловать, {user.FullName}!");
        return Redirect("/Cabinet");
    }
}
