using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class MoviesModel : AppPage
{
    readonly CinemaDb db;
    public MoviesModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? Genre { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Active { get; set; }
    public List<Movie> Movies { get; set; } = new();
    public List<Genre> Genres { get; set; } = new();

    public async Task OnGetAsync()
    {
        Genres = await db.Genres.OrderBy(g => g.Name).ToListAsync();
        var q = db.Movies.Include(m => m.Genre).Include(m => m.Sessions).AsQueryable();
        if (Genre is int g) q = q.Where(m => m.GenreId == g);
        if (Active is bool a) q = q.Where(m => m.IsActive == a);
        Movies = (await q.OrderBy(m => m.Title).ToListAsync()).Where(m => Search.Match(Q, m.Title, m.Director)).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var m = await db.Movies.Include(x => x.Sessions).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) Error("Фильм не найден.");
        else if (m.Sessions.Count > 0)
            Error($"Нельзя удалить фильм «{m.Title}»: у него есть сеансы ({m.Sessions.Count}). Сначала удалите сеансы или снимите фильм с проката.");
        else
        {
            db.Movies.Remove(m);
            await db.SaveChangesAsync();
            Success($"Фильм «{m.Title}» удалён.");
        }
        return RedirectToPage();
    }
}
