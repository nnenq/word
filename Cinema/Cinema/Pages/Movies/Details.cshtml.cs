using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Movies;

public class DetailsModel : PageModel
{
    readonly CinemaDb db;
    public DetailsModel(CinemaDb db) => this.db = db;

    public Movie Movie { get; set; } = null!;
    public List<IGrouping<DateTime, Session>> Days { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var movie = await db.Movies.Include(m => m.Genre).FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null) return NotFound();
        Movie = movie;
        var sessions = await db.Sessions.Include(s => s.Hall)
            .Where(s => s.MovieId == id && s.StartTime > DateTime.Now).OrderBy(s => s.StartTime).ToListAsync();
        Days = sessions.GroupBy(s => s.StartTime.Date).ToList();
        return Page();
    }
}
