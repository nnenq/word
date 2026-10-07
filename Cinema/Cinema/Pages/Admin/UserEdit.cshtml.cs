using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Cinema.Data;
using Cinema.Models;
using Cinema.Pages.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class UserInput
{
    [Required(ErrorMessage = "Введите ФИО.")]
    [RegularExpression(UserRules.NamePattern, ErrorMessage = UserRules.NameMessage)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Введите логин.")]
    [RegularExpression(UserRules.LoginPattern, ErrorMessage = UserRules.LoginMessage)]
    public string Login { get; set; } = "";

    [Required(ErrorMessage = "Введите электронную почту.")]
    [EmailAddress(ErrorMessage = "Электронная почта указана неверно. Пример: name@mail.ru")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Выберите роль.")]
    public string RoleCode { get; set; } = RoleCodes.Viewer;

    public string? Password { get; set; }
    public bool IsBlocked { get; set; }
}

/// <summary>Создание пользователя с назначением роли и изменение пользователя администратором.</summary>
public class UserEditModel : AppPage
{
    readonly CinemaDb db;
    readonly IPasswordHasher<User> hasher;
    public UserEditModel(CinemaDb db, IPasswordHasher<User> hasher) { this.db = db; this.hasher = hasher; }

    public int? Id { get; set; }
    public bool IsSelf => Id != null && Id == User.UserId();
    [BindProperty] public UserInput Input { get; set; } = new();
    public List<Role> Roles { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        Roles = await db.Roles.OrderBy(r => r.Id).ToListAsync();
        if (id == null) return Page();
        var u = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        Input = new UserInput { FullName = u.FullName, Login = u.Login, Email = u.Email, RoleCode = u.Role.Code, IsBlocked = u.IsBlocked };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        Roles = await db.Roles.OrderBy(r => r.Id).ToListAsync();
        User? user = null;
        if (id != null)
        {
            user = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
            if (user == null) return NotFound();
            Input.Login = user.Login; // логин не меняется
            ModelState.Remove("Input.Login");
            if (IsSelf) { Input.RoleCode = user.Role.Code; Input.IsBlocked = false; ModelState.Remove("Input.RoleCode"); }
        }
        Input.FullName = Input.FullName?.Trim() ?? "";
        Input.Email = Input.Email?.Trim().ToLowerInvariant() ?? "";
        Input.Login = Input.Login?.Trim() ?? "";

        var role = Roles.FirstOrDefault(r => r.Code == Input.RoleCode);
        if (role == null) ModelState.AddModelError("Input.RoleCode", "Выберите роль из списка.");
        if (id == null && string.IsNullOrEmpty(Input.Password)) ModelState.AddModelError("Input.Password", "Задайте пароль нового пользователя.");
        if (!string.IsNullOrEmpty(Input.Password) && !Regex.IsMatch(Input.Password, UserRules.PasswordPattern))
            ModelState.AddModelError("Input.Password", UserRules.PasswordMessage);
        if (id == null && await db.Users.AnyAsync(u => u.Login.ToLower() == Input.Login.ToLower()))
            ModelState.AddModelError("Input.Login", "Этот логин уже занят.");
        if (await db.Users.AnyAsync(u => u.Email == Input.Email && u.Id != id))
            ModelState.AddModelError("Input.Email", "Эта почта уже используется другим пользователем.");
        if (!ModelState.IsValid) return Page();

        if (user == null) { user = new User { Login = Input.Login }; db.Users.Add(user); }
        user.FullName = Input.FullName;
        user.Email = Input.Email;
        user.RoleId = role!.Id;
        user.IsBlocked = Input.IsBlocked;
        if (!string.IsNullOrEmpty(Input.Password)) user.PasswordHash = hasher.HashPassword(user, Input.Password);
        await db.SaveChangesAsync();
        Success(id == null ? $"Пользователь {user.Login} создан с ролью «{role.Name}»." : $"Пользователь {user.Login} сохранён (роль «{role.Name}»).");
        return Redirect("/Admin/Users");
    }
}
