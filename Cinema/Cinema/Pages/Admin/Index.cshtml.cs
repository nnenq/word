using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class IndexModel : PageModel
{
    readonly CinemaDb db;
    public IndexModel(CinemaDb db) => this.db = db;

    public record MovieStat(string Title, int Count, int Revenue);

    public int RevenueToday { get; set; }
    public int SoldToday { get; set; }
    public int RevenueWeek { get; set; }
    public int UsersCount { get; set; }
    public int MoviesCount { get; set; }
    public List<MovieStat> TopMovies { get; set; } = new();
    public List<Session> Upcoming { get; set; } = new();
    public Dictionary<int, int> SoldBySession { get; set; } = new();

    public async Task OnGetAsync()
    {
        var paid = db.Tickets.Where(t => t.Status == TicketStatus.Paid);
        var today = await paid.Where(t => t.CreatedAt >= DateTime.Today).Select(t => t.Price).ToListAsync();
        SoldToday = today.Count;
        RevenueToday = today.Sum();
        RevenueWeek = (await paid.Where(t => t.CreatedAt >= DateTime.Today.AddDays(-6)).Select(t => t.Price).ToListAsync()).Sum();
        UsersCount = await db.Users.CountAsync();
        MoviesCount = await db.Movies.CountAsync();

        TopMovies = (await paid.Select(t => new { t.Session.Movie.Title, t.Price }).ToListAsync())
            .GroupBy(x => x.Title).Select(g => new MovieStat(g.Key, g.Count(), g.Sum(x => x.Price)))
            .OrderByDescending(x => x.Revenue).ToList();

        Upcoming = await db.Sessions.Include(s => s.Movie).Include(s => s.Hall)
            .Where(s => s.StartTime > DateTime.Now).OrderBy(s => s.StartTime).Take(8).ToListAsync();
        var ids = Upcoming.Select(s => s.Id).ToList();
        SoldBySession = await paid.Where(t => ids.Contains(t.SessionId)).GroupBy(t => t.SessionId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }
}
