using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages;

public class ScheduleModel : PageModel
{
    readonly CinemaDb db;
    public ScheduleModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public int? Movie { get; set; }
    [BindProperty(SupportsGet = true)] public int? Hall { get; set; }

    public DateTime Date { get; set; }
    public List<Session> Sessions { get; set; } = new();
    public List<Movie> Movies { get; set; } = new();
    public List<Hall> Halls { get; set; } = new();
    public Dictionary<int, int> Sold { get; set; } = new();

    public async Task OnGetAsync(DateTime? date)
    {
        Date = (date ?? DateTime.Today).Date;
        Movies = await db.Movies.Where(m => m.IsActive).OrderBy(m => m.Title).ToListAsync();
        Halls = await db.Halls.OrderBy(h => h.Name).ToListAsync();
        var next = Date.AddDays(1);
        var q = db.Sessions.Include(s => s.Movie).Include(s => s.Hall).Where(s => s.StartTime >= Date && s.StartTime < next);
        if (Movie is int m) q = q.Where(s => s.MovieId == m);
        if (Hall is int h) q = q.Where(s => s.HallId == h);
        Sessions = await q.OrderBy(s => s.StartTime).ThenBy(s => s.HallId).ToListAsync();
        var ids = Sessions.Select(s => s.Id).ToList();
        Sold = await db.Tickets.Where(t => ids.Contains(t.SessionId) && t.Status == TicketStatus.Paid)
            .GroupBy(t => t.SessionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }
}
