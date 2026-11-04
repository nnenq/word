using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Account;

public class RegisterInput
{
    [Required(ErrorMessage = "Введите ФИО."), RegularExpression(UserRules.NamePattern, ErrorMessage = UserRules.NameMessage)]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Выберите группу.")] public int? GroupId { get; set; }
    [Required(ErrorMessage = "Введите логин."), RegularExpression(UserRules.LoginPattern, ErrorMessage = UserRules.LoginMessage)]
    public string Login { get; set; } = "";
    [EmailAddress(ErrorMessage = "Почта указана неверно.")] public string? Email { get; set; }
    [Required(ErrorMessage = "Введите пароль."), RegularExpression(UserRules.PasswordPattern, ErrorMessage = UserRules.PasswordMessage)]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "Повторите пароль."), Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
    public string Confirm { get; set; } = "";
}

/// <summary>Самостоятельная регистрация — только для студентов. Роль назначается автоматически.</summary>
public class RegisterModel : AppPage
{
    readonly AppDb db;
    readonly IPasswordHasher<User> hasher;
    public RegisterModel(AppDb db, IPasswordHasher<User> hasher) { this.db = db; this.hasher = hasher; }

    [BindProperty] public RegisterInput Input { get; set; } = new();
    public List<SelectListItem> Groups { get; set; } = new();

    async Task LoadAsync() => Groups = await db.StudentGroups.OrderBy(g => g.Name).Select(g => new SelectListItem(g.Name, g.Id.ToString())).ToListAsync();

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Login = Input.Login?.Trim() ?? "";
        Input.FullName = Input.FullName?.Trim() ?? "";
        if (await db.Users.AnyAsync(u => u.Login == Input.Login)) ModelState.AddModelError("Input.Login", "Такой логин уже занят.");
        if (Input.GroupId is int g && !await db.StudentGroups.AnyAsync(x => x.Id == g)) ModelState.AddModelError("Input.GroupId", "Такой группы нет.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        var user = new User { Login = Input.Login, FullName = Input.FullName, Email = Input.Email?.Trim(), Role = Roles.Student };
        user.PasswordHash = hasher.HashPassword(user, Input.Password);
        user.Memberships.Add(new GroupMembership { GroupId = Input.GroupId!.Value });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await Auth.SignInAsync(HttpContext, user);
        Success($"Регистрация прошла успешно. Добро пожаловать, {user.FullName}! Начните с первой лекции любой дисциплины.");
        return Redirect("/Study");
    }
}
