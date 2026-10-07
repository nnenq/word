using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Movies;

public class IndexModel : PageModel
{
    readonly CinemaDb db;
    public IndexModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? Genre { get; set; }
    [BindProperty(SupportsGet = true)] public string? Age { get; set; }
    [BindProperty(SupportsGet = true)] public bool Today { get; set; }

    public List<Movie> Movies { get; set; } = new();
    public List<Genre> Genres { get; set; } = new();

    public async Task OnGetAsync()
    {
        Genres = await db.Genres.OrderBy(g => g.Name).ToListAsync();
        var query = db.Movies.Include(m => m.Genre).Where(m => m.IsActive);
        if (Genre is int g) query = query.Where(m => m.GenreId == g);
        if (!string.IsNullOrEmpty(Age)) query = query.Where(m => m.AgeRating == Age);
        if (Today)
        {
            var tomorrow = DateTime.Today.AddDays(1);
            query = query.Where(m => m.Sessions.Any(s => s.StartTime > DateTime.Now && s.StartTime < tomorrow));
        }
        Movies = await query.OrderBy(m => m.Title).ToListAsync();
        // Поиск без учёта регистра (SQLite сам по себе различает регистр русских букв)
        if (!string.IsNullOrWhiteSpace(Q))
            Movies = Movies.Where(m => Search.Match(Q, m.Title, m.Director)).ToList();
    }
}
