using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Pages.Account;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

public class UserInput
{
    [Required(ErrorMessage = "Введите ФИО."), RegularExpression(UserRules.NamePattern, ErrorMessage = UserRules.NameMessage)] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Введите логин."), RegularExpression(UserRules.LoginPattern, ErrorMessage = UserRules.LoginMessage)] public string Login { get; set; } = "";
    [EmailAddress(ErrorMessage = "Почта указана неверно.")] public string? Email { get; set; }
    public string Role { get; set; } = Roles.Student;
    public int? GroupId { get; set; }
    public bool IsBlocked { get; set; }
    /// <summary>Новый пароль (при создании — обязателен, при изменении — только если нужно сменить).</summary>
    [RegularExpression(UserRules.PasswordPattern, ErrorMessage = UserRules.PasswordMessage)] public string? Password { get; set; }
}

/// <summary>Создание и изменение учётной записи: роль, группа, блокировка, сброс пароля.</summary>
public class UserEditModel : AppPage
{
    readonly AppDb db;
    readonly IPasswordHasher<User> hasher;
    readonly Audit audit;
    public UserEditModel(AppDb db, IPasswordHasher<User> hasher, Audit audit) { this.db = db; this.hasher = hasher; this.audit = audit; }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public UserInput Input { get; set; } = new();
    public List<SelectListItem> Groups { get; set; } = new();
    public bool IsSelf => Id == User.UserId();

    async Task LoadGroupsAsync() => Groups = await db.StudentGroups.OrderBy(g => g.Name).Select(g => new SelectListItem(g.Name, g.Id.ToString())).ToListAsync();

    public async Task<IActionResult> OnGetAsync(string? role)
    {
        await LoadGroupsAsync();
        if (Id == null) { Input.Role = role == Roles.Teacher ? Roles.Teacher : Roles.Student; return Page(); }
        var u = await db.Users.Include(x => x.Memberships).FirstOrDefaultAsync(x => x.Id == Id);
        if (u == null) return NotFound();
        Input = new UserInput { FullName = u.FullName, Login = u.Login, Email = u.Email, Role = u.Role, IsBlocked = u.IsBlocked, GroupId = u.Memberships.FirstOrDefault()?.GroupId };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadGroupsAsync();
        Input.Login = Input.Login?.Trim() ?? "";
        Input.FullName = Input.FullName?.Trim() ?? "";
        if (Input.Role is not (Roles.Teacher or Roles.Student)) ModelState.AddModelError("Input.Role", "Выберите роль.");
        if (Id == null && string.IsNullOrEmpty(Input.Password)) ModelState.AddModelError("Input.Password", "Задайте пароль.");
        if (await db.Users.AnyAsync(u => u.Login == Input.Login && u.Id != Id)) ModelState.AddModelError("Input.Login", "Такой логин уже занят.");
        if (Input.Role == Roles.Student && Input.GroupId == null) ModelState.AddModelError("Input.GroupId", "Выберите группу студента.");
        if (IsSelf && (Input.IsBlocked || Input.Role != Roles.Teacher)) ModelState.AddModelError("", "Нельзя заблокировать себя или снять с себя роль преподавателя.");
        if (!ModelState.IsValid) return Page();

        User u;
        if (Id == null) { u = new User(); db.Users.Add(u); }
        else
        {
            u = await db.Users.Include(x => x.Memberships).FirstOrDefaultAsync(x => x.Id == Id) ?? throw new InvalidOperationException();
            db.GroupMemberships.RemoveRange(u.Memberships);
        }
        var changes = new List<string>();
        if (Id != null && u.Role != Input.Role) changes.Add($"роль: {Roles.Name(u.Role)} → {Roles.Name(Input.Role)}");
        if (Id != null && u.IsBlocked != Input.IsBlocked) changes.Add(Input.IsBlocked ? "заблокирован" : "разблокирован");
        if (!string.IsNullOrEmpty(Input.Password) && Id != null) changes.Add("пароль сброшен");
        u.FullName = Input.FullName; u.Login = Input.Login; u.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        u.Role = Input.Role; u.IsBlocked = Input.IsBlocked;
        if (!string.IsNullOrEmpty(Input.Password)) u.PasswordHash = hasher.HashPassword(u, Input.Password);
        if (Input.Role == Roles.Student && Input.GroupId != null) u.Memberships = new() { new GroupMembership { GroupId = Input.GroupId.Value } };
        await db.SaveChangesAsync();
        await audit.WriteAsync(Id == null ? "Учётная запись создана" : "Учётная запись изменена",
            $"{u.FullName} ({u.Login}, {Roles.Name(u.Role)})" + (changes.Count > 0 ? ": " + string.Join(", ", changes) : ""));
        Success(Id == null ? $"Учётная запись {u.FullName} создана." : "Изменения сохранены.");
        return Redirect("/Teacher/Users");
    }
}
