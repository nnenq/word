using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class SessionsModel : AppPage
{
    readonly CinemaDb db;
    public SessionsModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public int? Movie { get; set; }
    [BindProperty(SupportsGet = true)] public int? Hall { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<Session> Sessions { get; set; } = new();
    public List<Movie> Movies { get; set; } = new();
    public List<Hall> Halls { get; set; } = new();
    public Dictionary<int, int> Sold { get; set; } = new();

    public async Task OnGetAsync(DateTime? from, DateTime? to)
    {
        From = (from ?? DateTime.Today).Date;
        To = (to ?? From.AddDays(6)).Date;
        if (To < From) To = From;
        Movies = await db.Movies.OrderBy(m => m.Title).ToListAsync();
        Halls = await db.Halls.OrderBy(h => h.Name).ToListAsync();
        var end = To.AddDays(1);
        var q = db.Sessions.Include(s => s.Movie).Include(s => s.Hall).Where(s => s.StartTime >= From && s.StartTime < end);
        if (Movie is int m) q = q.Where(s => s.MovieId == m);
        if (Hall is int h) q = q.Where(s => s.HallId == h);
        Sessions = await q.OrderBy(s => s.StartTime).ThenBy(s => s.HallId).ToListAsync();
        var ids = Sessions.Select(s => s.Id).ToList();
        Sold = await db.Tickets.Where(t => ids.Contains(t.SessionId) && t.Status == TicketStatus.Paid)
            .GroupBy(t => t.SessionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var s = await db.Sessions.Include(x => x.Movie).Include(x => x.Tickets).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) Error("Сеанс не найден.");
        else if (s.Tickets.Any(t => t.Status == TicketStatus.Paid))
            Error($"Нельзя удалить сеанс: на него продано билетов — {s.Tickets.Count(t => t.Status == TicketStatus.Paid)}. Сначала оформите возврат билетов.");
        else if (s.Tickets.Count > 0)
            Error("Нельзя удалить сеанс: по нему есть история продаж (возвращённые билеты).");
        else
        {
            db.Sessions.Remove(s);
            await db.SaveChangesAsync();
            Success($"Сеанс {s.StartTime:dd.MM HH:mm} «{s.Movie.Title}» удалён.");
        }
        return Redirect(Request.Headers.Referer.ToString() is var r && Uri.TryCreate(r, UriKind.Absolute, out var u) && u.AbsolutePath == "/Admin/Sessions"
            ? u.PathAndQuery : "/Admin/Sessions");
    }
}
