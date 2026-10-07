using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages;

public class IndexModel : PageModel
{
    readonly CinemaDb db;
    public IndexModel(CinemaDb db) => this.db = db;

    public List<Movie> Movies { get; set; } = new();
    public int MoviesCount { get; set; }
    public int SessionsToday { get; set; }
    public int HallsCount { get; set; }
    public Session? Next { get; set; }

    public async Task OnGetAsync()
    {
        Movies = await db.Movies.Include(m => m.Genre).Where(m => m.IsActive).OrderBy(m => m.Title).Take(8).ToListAsync();
        MoviesCount = await db.Movies.CountAsync(m => m.IsActive);
        var tomorrow = DateTime.Today.AddDays(1);
        SessionsToday = await db.Sessions.CountAsync(s => s.StartTime >= DateTime.Today && s.StartTime < tomorrow);
        HallsCount = await db.Halls.CountAsync();
        Next = await db.Sessions.Include(s => s.Movie).Where(s => s.StartTime > DateTime.Now).OrderBy(s => s.StartTime).FirstOrDefaultAsync();
    }
}
