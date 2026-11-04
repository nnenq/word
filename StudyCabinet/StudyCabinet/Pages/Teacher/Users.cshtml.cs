using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Управление учётными записями: список, поиск, фильтр по роли и группе, удаление.</summary>
public class UsersModel : AppPage
{
    readonly AppDb db;
    readonly Audit audit;
    public UsersModel(AppDb db, Audit audit) { this.db = db; this.audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public int? Group { get; set; }
    public List<User> Users { get; set; } = new();
    public List<StudentGroup> Groups { get; set; } = new();

    public async Task OnGetAsync()
    {
        Groups = await db.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        var q = db.Users.Include(u => u.Memberships).ThenInclude(m => m.Group).AsQueryable();
        if (Role is Roles.Teacher or Roles.Student) q = q.Where(u => u.Role == Role);
        if (Group != null) q = q.Where(u => u.Memberships.Any(m => m.GroupId == Group));
        Users = await q.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
        if (!string.IsNullOrWhiteSpace(Q)) Users = Users.Where(u => Grading.Normalize(u.FullName + " " + u.Login + " " + u.Email).Contains(Grading.Normalize(Q))).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (id == User.UserId()) { Error("Нельзя удалить свою учётную запись."); return RedirectToPage(); }
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (u.Role == Roles.Teacher && await db.Users.CountAsync(x => x.Role == Roles.Teacher && !x.IsBlocked) <= 1)
        { Error("Нельзя удалить последнего преподавателя."); return RedirectToPage(); }
        db.Users.Remove(u);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Учётная запись удалена", $"{u.FullName} ({u.Login}, {Roles.Name(u.Role)}) — вместе с результатами обучения");
        Success($"Учётная запись {u.FullName} удалена.");
        return RedirectToPage();
    }
}
