using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Tickets;

/// <summary>Выбор мест на схеме зала и покупка (зритель) или продажа на кассе (кассир, администратор).</summary>
public class BuyModel : AppPage
{
    readonly CinemaDb db;
    readonly TicketService tickets;
    public BuyModel(CinemaDb db, TicketService tickets) { this.db = db; this.tickets = tickets; }

    public Session Session { get; set; } = null!;
    public HashSet<(int Row, int Seat)> Taken { get; set; } = new();
    public bool Closed { get; set; }

    [BindProperty] public List<string> Seats { get; set; } = new();
    [BindProperty] public string BuyerName { get; set; } = "";
    [BindProperty] public string Payment { get; set; } = "Наличные";

    async Task<bool> LoadAsync(int id)
    {
        var s = await db.Sessions.Include(x => x.Movie).Include(x => x.Hall).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return false;
        Session = s;
        Taken = await tickets.TakenSeatsAsync(id);
        Closed = s.StartTime <= DateTime.Now;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        bool staff = User.IsStaff();

        var (sold, error) = staff
            ? await tickets.SellAsync(id, Seats, null, me, string.IsNullOrWhiteSpace(BuyerName) ? "Покупатель на кассе" : BuyerName,
                                      Payment is "Карта" or "Наличные" ? Payment : "Наличные")
            : await tickets.SellAsync(id, Seats, me, null, me.FullName, "Карта");
        if (error != null)
        {
            TempData["Error"] = error;
            Taken = await tickets.TakenSeatsAsync(id);
            Seats = Seats.Where(k => !Taken.Any(t => $"{t.Row}-{t.Seat}" == k)).ToList();
            return Page();
        }

        var places = string.Join(", ", sold.Select(t => $"ряд {t.Row} место {t.Seat}"));
        Success($"Оформлено билетов: {sold.Count} на сумму {Fmt.Money(sold.Sum(t => t.Price))} ({places}). Номера: {string.Join(", ", sold.Select(t => t.Code))}.");
        return staff ? Redirect("/Cashier/Tickets?session=" + id) : Redirect("/Cabinet");
    }
}
