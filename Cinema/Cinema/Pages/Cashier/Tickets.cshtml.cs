using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Cashier;

/// <summary>
/// Проданные билеты (кассир, администратор): все фильмы списком сверху, билеты сгруппированы в заказы,
/// возврат одного места, всего заказа или нескольких отмеченных заказов сразу.
/// </summary>
public class TicketsModel : AppPage
{
    const int PageSize = 150;
    readonly CinemaDb db;
    readonly TicketService tickets;
    public TicketsModel(CinemaDb db, TicketService tickets) { this.db = db; this.tickets = tickets; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Channel { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true, Name = "session")] public int? SessionId { get; set; }
    [BindProperty(SupportsGet = true, Name = "movie")] public int? MovieId { get; set; }

    public List<TicketOrder> Orders { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TicketCount { get; set; }
    public int Sum { get; set; }
    /// <summary>Фильмы для строки быстрого выбора и число действующих билетов на будущие сеансы.</summary>
    public List<(Movie Movie, int Active)> Movies { get; set; } = new();
    public int MeId { get; set; }

    public async Task OnGetAsync()
    {
        MeId = User.UserId() ?? 0;
        var activeByMovie = await db.Tickets.Where(t => t.Status == TicketStatus.Paid && t.Session.StartTime > DateTime.Now)
            .GroupBy(t => t.Session.MovieId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        Movies = (await db.Movies.Where(m => m.IsActive || activeByMovie.Keys.Contains(m.Id)).OrderBy(m => m.Title).ToListAsync())
            .Select(m => (m, activeByMovie.GetValueOrDefault(m.Id))).ToList();

        var q = db.Tickets.Include(t => t.Session).ThenInclude(s => s.Movie).Include(t => t.Session).ThenInclude(s => s.Hall)
                  .Include(t => t.SoldBy).AsQueryable();
        if (Status == "paid") q = q.Where(t => t.Status == TicketStatus.Paid);
        if (Status == "upcoming") q = q.Where(t => t.Status == TicketStatus.Paid && t.Session.StartTime > DateTime.Now);
        if (Status == "returned") q = q.Where(t => t.Status == TicketStatus.Returned);
        if (!string.IsNullOrEmpty(Channel)) q = q.Where(t => t.Channel == Channel);
        if (SessionId is int sid) q = q.Where(t => t.SessionId == sid);
        if (MovieId is int mid) q = q.Where(t => t.Session.MovieId == mid);
        if (Date is DateTime d) { var next = d.Date.AddDays(1); q = q.Where(t => t.Session.StartTime >= d.Date && t.Session.StartTime < next); }

        // Поиск отбирает заказы целиком: по номеру одного билета находится весь заказ
        var orders = TicketOrder.Group(await q.ToListAsync())
            .Where(o => o.Tickets.Any(t => Search.Match(Q, t.Code, t.BuyerName, t.Session.Movie.Title))).ToList();
        TicketCount = orders.Sum(o => o.Tickets.Count);
        Sum = orders.Sum(o => o.ActiveSum);
        // Сначала ближайшие будущие сеансы (их чаще всего возвращают), потом прошедшие — от новых к старым
        orders = orders
            .OrderBy(o => o.Started).ThenBy(o => o.Started ? -o.Session.StartTime.Ticks : o.Session.StartTime.Ticks).ThenByDescending(o => o.First.CreatedAt)
            .ToList();
        TotalOrders = orders.Count;
        Orders = orders.Take(PageSize).ToList();
    }

    /// <summary>Возврат: одного билета, заказа или всех отмеченных заказов.</summary>
    public async Task<IActionResult> OnPostReturnAsync(string? pick, List<string> ids)
    {
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        // Кнопка у заказа или места передаёт свои билеты в pick, кнопка «Вернуть выбранные» — отмеченные галочки
        var list = string.IsNullOrEmpty(pick) ? ParseIds(ids) : ParseIds(new[] { pick });
        var (back, error) = await tickets.ReturnManyAsync(list, me, isStaff: true);
        if (error != null) Error(error);
        else
        {
            var codes = back.Count <= 4 ? ": " + string.Join(", ", back.Select(t => t.Code)) : "";
            Success($"Возврат оформлен — {back.Count} {Plural(back.Count, "билет", "билета", "билетов")}{codes}. Вернуть покупателю {Fmt.Money(back.Sum(t => t.Price))}.");
        }
        // Возвращаемся к тому же списку с фильтрами (только адрес внутри сайта)
        var referer = Request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var u) && u.AbsolutePath.StartsWith("/Cashier/Tickets")
            ? LocalRedirect(u.PathAndQuery)
            : Redirect("/Cashier/Tickets");
    }
}
