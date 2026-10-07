using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Cashier;

/// <summary>Поиск проданных билетов и возврат на кассе (кассир, администратор).</summary>
public class TicketsModel : AppPage
{
    const int PageSize = 200;
    readonly CinemaDb db;
    readonly TicketService tickets;
    public TicketsModel(CinemaDb db, TicketService tickets) { this.db = db; this.tickets = tickets; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Channel { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true, Name = "session")] public int? SessionId { get; set; }

    public List<Ticket> Tickets { get; set; } = new();
    public int Total { get; set; }
    public int Sum { get; set; }

    public async Task OnGetAsync()
    {
        var q = db.Tickets.Include(t => t.Session).ThenInclude(s => s.Movie).Include(t => t.Session).ThenInclude(s => s.Hall)
                  .Include(t => t.SoldBy).AsQueryable();
        if (Status == "paid") q = q.Where(t => t.Status == TicketStatus.Paid);
        if (Status == "returned") q = q.Where(t => t.Status == TicketStatus.Returned);
        if (!string.IsNullOrEmpty(Channel)) q = q.Where(t => t.Channel == Channel);
        if (SessionId is int sid) q = q.Where(t => t.SessionId == sid);
        if (Date is DateTime d) { var next = d.Date.AddDays(1); q = q.Where(t => t.Session.StartTime >= d.Date && t.Session.StartTime < next); }

        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        list = list.Where(t => Search.Match(Q, t.Code, t.BuyerName, t.Session.Movie.Title)).ToList();
        Total = list.Count;
        Sum = list.Where(t => t.Status == TicketStatus.Paid).Sum(t => t.Price);
        Tickets = list.Take(PageSize).ToList();
    }

    public async Task<IActionResult> OnPostReturnAsync(int id)
    {
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        var error = await tickets.ReturnAsync(id, me, isStaff: true);
        if (error != null) Error(error);
        else
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == id);
            Success($"Возврат оформлен: билет {t.Code}, вернуть покупателю {Fmt.Money(t.Price)}.");
        }
        // Возвращаемся к тому же списку с фильтрами (только адрес внутри сайта)
        var referer = Request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var u) && u.AbsolutePath.StartsWith("/Cashier/Tickets")
            ? LocalRedirect(u.PathAndQuery)
            : Redirect("/Cashier/Tickets");
    }
}
