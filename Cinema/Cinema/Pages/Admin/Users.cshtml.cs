using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class UsersModel : AppPage
{
    readonly CinemaDb db;
    public UsersModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public List<User> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();

    public async Task OnGetAsync()
    {
        Roles = await db.Roles.OrderBy(r => r.Id).ToListAsync();
        var q = db.Users.Include(u => u.Role).AsQueryable();
        if (!string.IsNullOrEmpty(Role)) q = q.Where(u => u.Role.Code == Role);
        if (Status == "active") q = q.Where(u => !u.IsBlocked);
        if (Status == "blocked") q = q.Where(u => u.IsBlocked);
        Users = (await q.OrderBy(u => u.RoleId).ThenBy(u => u.FullName).ToListAsync())
            .Where(u => Search.Match(Q, u.FullName, u.Login, u.Email)).ToList();
    }

    public async Task<IActionResult> OnPostBlockAsync(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) Error("Пользователь не найден.");
        else if (u.Id == User.UserId()) Error("Нельзя заблокировать собственную учётную запись.");
        else
        {
            u.IsBlocked = !u.IsBlocked;
            await db.SaveChangesAsync();
            Success(u.IsBlocked ? $"Пользователь {u.Login} заблокирован и больше не сможет войти." : $"Пользователь {u.Login} разблокирован.");
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) Error("Пользователь не найден.");
        else if (u.Id == User.UserId()) Error("Нельзя удалить собственную учётную запись.");
        else
        {
            db.Users.Remove(u);
            await db.SaveChangesAsync();
            Success($"Пользователь {u.Login} удалён.");
        }
        return RedirectToPage();
    }
}
