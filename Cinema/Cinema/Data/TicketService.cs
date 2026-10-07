using Cinema.Models;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data;

/// <summary>Продажа и возврат билетов. Используется зрителем (сайт) и кассиром (касса).</summary>
public class TicketService
{
    public const int MaxSeatsPerOrder = 6;
    /// <summary>Зритель может вернуть билет не позже чем за час до начала сеанса.</summary>
    public static readonly TimeSpan ViewerReturnDeadline = TimeSpan.FromHours(1);

    readonly CinemaDb db;
    public TicketService(CinemaDb db) => this.db = db;

    /// <summary>Места сеанса, на которые уже проданы билеты.</summary>
    public async Task<HashSet<(int Row, int Seat)>> TakenSeatsAsync(int sessionId) =>
        (await db.Tickets.Where(t => t.SessionId == sessionId && t.Status == TicketStatus.Paid)
                         .Select(t => new { t.Row, t.Seat }).ToListAsync())
        .Select(x => (x.Row, x.Seat)).ToHashSet();

    /// <summary>
    /// Оформляет билеты на выбранные места. Возвращает созданные билеты или текст ошибки.
    /// Места передаются строками вида "3-7" (ряд-место).
    /// </summary>
    public async Task<(List<Ticket> Tickets, string? Error)> SellAsync(
        int sessionId, IEnumerable<string> seatKeys, User? buyer, User? cashier, string buyerName, string payment)
    {
        var session = await db.Sessions.Include(s => s.Hall).Include(s => s.Movie).FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return (new(), "Сеанс не найден.");
        if (session.StartTime <= DateTime.Now) return (new(), "Продажа на этот сеанс закрыта: сеанс уже начался.");

        var places = new List<(int Row, int Seat)>();
        foreach (var key in seatKeys.Distinct())
        {
            var parts = key.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var r) || !int.TryParse(parts[1], out var s)
                || r < 1 || r > session.Hall.Rows || s < 1 || s > session.Hall.SeatsPerRow)
                return (new(), $"Неверное место: {key}.");
            places.Add((r, s));
        }
        if (places.Count == 0) return (new(), "Выберите хотя бы одно место на схеме зала.");
        if (cashier == null && places.Count > MaxSeatsPerOrder) return (new(), $"За один заказ можно купить не больше {MaxSeatsPerOrder} билетов.");
        if (string.IsNullOrWhiteSpace(buyerName)) return (new(), "Укажите имя покупателя.");

        var taken = await TakenSeatsAsync(sessionId);
        var busy = places.Where(taken.Contains).ToList();
        if (busy.Count > 0)
            return (new(), "Места уже заняты: " + string.Join(", ", busy.Select(p => $"ряд {p.Row}, место {p.Seat}")) + ". Выберите другие.");

        int next = (await db.Tickets.MaxAsync(t => (int?)t.Id) ?? 0) + 1;
        var tickets = places.Select((p, i) => new Ticket
        {
            Code = $"T-{next + i:D6}", SessionId = sessionId, Row = p.Row, Seat = p.Seat, Price = session.Price,
            UserId = buyer?.Id, BuyerName = buyerName.Trim(), SoldById = cashier?.Id,
            Channel = cashier == null ? "Сайт" : "Касса", Payment = payment,
        }).ToList();
        db.Tickets.AddRange(tickets);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Кто-то купил это место одновременно с нами: сработал уникальный индекс базы данных
            db.ChangeTracker.Clear();
            return (new(), "Пока вы выбирали, одно из мест купили. Обновите схему зала и выберите другие места.");
        }
        return (tickets, null);
    }

    /// <summary>Возврат билета. Зритель может вернуть только свой билет и не позже чем за час до сеанса.</summary>
    public async Task<string?> ReturnAsync(int ticketId, User by, bool isStaff)
    {
        var t = await db.Tickets.Include(x => x.Session).FirstOrDefaultAsync(x => x.Id == ticketId);
        if (t == null) return "Билет не найден.";
        if (t.Status == TicketStatus.Returned) return "Билет уже возвращён.";
        if (!isStaff && t.UserId != by.Id) return "Можно вернуть только свой билет.";
        if (t.Session.StartTime <= DateTime.Now) return "Сеанс уже начался, вернуть билет нельзя.";
        if (!isStaff && t.Session.StartTime - DateTime.Now < ViewerReturnDeadline)
            return "Вернуть билет через сайт можно не позже чем за час до начала сеанса. Обратитесь в кассу кинотеатра.";
        t.Status = TicketStatus.Returned;
        t.ReturnedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return null;
    }
}
