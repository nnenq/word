using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Учебные группы: добавление, переименование, удаление (только пустой группы).</summary>
public class GroupsModel : AppPage
{
    readonly AppDb db;
    readonly Audit audit;
    public GroupsModel(AppDb db, Audit audit) { this.db = db; this.audit = audit; }

    public List<(StudentGroup G, int Count)> Rows { get; set; } = new();

    public async Task OnGetAsync() =>
        Rows = (await db.StudentGroups.OrderBy(g => g.Name).Select(g => new { g, c = g.Members.Count }).ToListAsync()).Select(x => (x.g, x.c)).ToList();

    static string? Check(string? name) => string.IsNullOrWhiteSpace(name) ? "Введите название группы." : name.Trim().Length > 50 ? "Не длиннее 50 символов." : null;

    public async Task<IActionResult> OnPostAddAsync(string? name)
    {
        if (Check(name) is string e) { Error(e); return RedirectToPage(); }
        name = name!.Trim();
        if (await db.StudentGroups.AnyAsync(g => g.Name == name)) { Error("Такая группа уже есть."); return RedirectToPage(); }
        db.StudentGroups.Add(new StudentGroup { Name = name });
        await db.SaveChangesAsync();
        await audit.WriteAsync("Группа добавлена", name);
        Success($"Группа {name} добавлена.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string? name)
    {
        var g = await db.StudentGroups.FindAsync(id);
        if (g == null) return NotFound();
        if (Check(name) is string e) { Error(e); return RedirectToPage(); }
        name = name!.Trim();
        if (await db.StudentGroups.AnyAsync(x => x.Name == name && x.Id != id)) { Error("Такая группа уже есть."); return RedirectToPage(); }
        g.Name = name;
        await db.SaveChangesAsync();
        Success("Группа переименована.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var g = await db.StudentGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) return NotFound();
        if (g.Members.Count > 0) { Error($"В группе {g.Name} есть студенты — сначала переведите их в другую группу."); return RedirectToPage(); }
        db.StudentGroups.Remove(g);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Группа удалена", g.Name);
        Success($"Группа {g.Name} удалена.");
        return RedirectToPage();
    }
}
