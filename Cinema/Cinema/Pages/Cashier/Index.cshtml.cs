using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Cashier;

public class IndexModel : PageModel
{
    readonly CinemaDb db;
    public IndexModel(CinemaDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public DateTime Date { get; set; }
    public List<Session> Sessions { get; set; } = new();
    public Dictionary<int, int> Sold { get; set; } = new();
    public int MySoldToday { get; set; }
    public int MyRevenueToday { get; set; }

    public async Task OnGetAsync(DateTime? date)
    {
        Date = (date ?? DateTime.Today).Date;
        var next = Date.AddDays(1);
        Sessions = await db.Sessions.Include(s => s.Movie).Include(s => s.Hall)
            .Where(s => s.StartTime >= Date && s.StartTime < next).OrderBy(s => s.StartTime).ToListAsync();
        Sessions = Sessions.Where(s => Search.Match(Q, s.Movie.Title)).ToList();
        var ids = Sessions.Select(s => s.Id).ToList();
        Sold = await db.Tickets.Where(t => ids.Contains(t.SessionId) && t.Status == TicketStatus.Paid)
            .GroupBy(t => t.SessionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        var me = User.UserId();
        var mine = await db.Tickets.Where(t => t.SoldById == me && t.Status == TicketStatus.Paid && t.CreatedAt >= DateTime.Today)
            .Select(t => t.Price).ToListAsync();
        MySoldToday = mine.Count;
        MyRevenueToday = mine.Sum();
    }
}
