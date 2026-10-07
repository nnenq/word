using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class HallsModel : AppPage
{
    readonly CinemaDb db;
    public HallsModel(CinemaDb db) => this.db = db;

    public List<Hall> Halls { get; set; } = new();

    public async Task OnGetAsync() => Halls = await db.Halls.Include(h => h.Sessions).OrderBy(h => h.Name).ToListAsync();

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var h = await db.Halls.Include(x => x.Sessions).FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) Error("Зал не найден.");
        else if (h.Sessions.Count > 0) Error($"Нельзя удалить «{h.Name}»: в нём запланированы сеансы ({h.Sessions.Count}).");
        else
        {
            db.Halls.Remove(h);
            await db.SaveChangesAsync();
            Success($"Зал «{h.Name}» удалён.");
        }
        return RedirectToPage();
    }
}
