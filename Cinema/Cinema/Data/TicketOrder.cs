using Cinema.Models;

namespace Cinema.Data;

/// <summary>
/// Заказ — билеты, купленные вместе: один сеанс, один покупатель, одно время покупки.
/// Отдельной таблицы нет, заказы собираются из билетов при выводе на экран.
/// </summary>
public class TicketOrder
{
    public Session Session { get; init; } = null!;
    public List<Ticket> Tickets { get; init; } = new();
    public Ticket First => Tickets[0];

    public List<Ticket> Active => Tickets.Where(t => t.Status == TicketStatus.Paid).ToList();
    public int ActiveSum => Active.Sum(t => t.Price);
    public int TotalSum => Tickets.Sum(t => t.Price);
    public bool AllReturned => Tickets.All(t => t.Status == TicketStatus.Returned);
    public bool Started => Session.StartTime <= DateTime.Now;

    /// <summary>Билеты заказа, которые этот пользователь может вернуть прямо сейчас.</summary>
    public List<Ticket> Returnable(int userId, bool isStaff) =>
        Active.Where(t => TicketService.CanReturn(t, userId, isStaff) == null).ToList();

    public string Seats => string.Join(", ", Tickets.GroupBy(t => t.Row)
        .Select(g => $"ряд {g.Key}: {string.Join(", ", g.Select(t => t.Seat))}"));

    /// <summary>Собирает билеты в заказы (порядок заказов — как у первых билетов в списке).</summary>
    public static List<TicketOrder> Group(IEnumerable<Ticket> tickets) =>
        tickets.GroupBy(t => (t.SessionId, t.UserId, t.SoldById, t.BuyerName, t.CreatedAt.Ticks / TimeSpan.TicksPerMinute))
            .Select(g => new TicketOrder { Session = g.First().Session, Tickets = g.OrderBy(t => t.Row).ThenBy(t => t.Seat).ToList() })
            .ToList();
}
