using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Cabinet;

/// <summary>
/// Электронный чек заказа. Открывается по номеру любого билета заказа.
/// Зритель видит только свои чеки, кассир и администратор — любые.
/// </summary>
public class ReceiptModel : AppPage
{
    readonly CinemaDb db;
    public ReceiptModel(CinemaDb db) => this.db = db;

    public TicketOrder Order { get; set; } = null!;
    public string Number => Order.First.Id.ToString("D6");
    /// <summary>Чек открыт сразу после покупки — показываются кнопки «что дальше».</summary>
    [BindProperty(SupportsGet = true)] public bool New { get; set; }
    /// <summary>Чек смотрит сотрудник кассы, а не сам покупатель.</summary>
    public bool ForStaff { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        if (t.UserId != User.UserId() && !User.IsStaff()) return Forbid();

        // Билеты того же заказа: тот же сеанс и покупатель, затем группировка по времени покупки
        var same = await db.Tickets.Include(x => x.Session).ThenInclude(s => s.Movie).Include(x => x.Session).ThenInclude(s => s.Hall)
            .Include(x => x.SoldBy)
            .Where(x => x.SessionId == t.SessionId && x.UserId == t.UserId && x.SoldById == t.SoldById && x.BuyerName == t.BuyerName)
            .ToListAsync();
        Order = TicketOrder.Group(same).First(o => o.Tickets.Any(x => x.Id == id));
        ForStaff = User.IsStaff() && t.UserId != User.UserId();
        return Page();
    }
}
