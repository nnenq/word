// Логика продажи и возврата билетов.
// Здесь используются отладочные классы из пространства имён System.Diagnostics:
//   Debug     — сообщения и проверки только для отладочной сборки (Debug);
//   Trace     — журнал работы, остаётся и в итоговой сборке (Release);
//   Stopwatch — замер времени выполнения операции;
//   [Conditional("DEBUG")] — метод вызывается только в отладочной сборке.

using System.Diagnostics;

namespace CinemaDebug;

public class BookingService
{
    public const int MaxSeatsPerOrder = 6;

    private readonly List<Session> _sessions;
    private readonly List<Ticket> _tickets = new();
    private int _nextTicketId = 1;

    public BookingService(List<Session> sessions)
    {
        _sessions = sessions;
        Trace.TraceInformation($"Сервис продажи запущен. Сеансов: {sessions.Count}");
    }

    public IReadOnlyList<Session> Sessions => _sessions;
    public IReadOnlyList<Ticket> Tickets => _tickets;

    public Session FindSession(int id) =>
        _sessions.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Сеанс #{id} не найден.");

    /// <summary>Покупка билетов на несколько мест одного сеанса.</summary>
    public List<Ticket> Buy(int sessionId, List<(int Row, int Seat)> places)
    {
        var timer = Stopwatch.StartNew();
        Debug.WriteLine($"Buy: сеанс #{sessionId}, мест: {places.Count}");
        Debug.Indent();   // следующие сообщения Debug будут с отступом
        try
        {
            Session session = FindSession(sessionId);

            if (places.Count == 0)
                throw new ArgumentException("Не выбрано ни одного места.");
            if (places.Count > MaxSeatsPerOrder)
                throw new ArgumentException($"За один раз можно купить не больше {MaxSeatsPerOrder} билетов.");
            if (session.Start <= DateTime.Now)
            {
                Trace.TraceWarning($"Попытка купить билет на начавшийся сеанс #{session.Id}");
                throw new InvalidOperationException("Сеанс уже начался, продажа закрыта.");
            }

            // Сначала проверяем все места, и только потом занимаем: либо продаются все, либо ни одного.
            foreach (var (row, seat) in places)
            {
                Debug.WriteLine($"проверка места: ряд {row}, место {seat}");
                if (row < 1 || row > session.Rows || seat < 1 || seat > session.SeatsPerRow)
                    throw new ArgumentOutOfRangeException(null, $"В зале «{session.Hall}» нет места ряд {row}, место {seat}.");
                if (session.Seats[row - 1, seat - 1])
                    throw new InvalidOperationException($"Место ряд {row}, место {seat} уже занято.");
            }
            if (places.Distinct().Count() != places.Count)
                throw new ArgumentException("Одно и то же место указано несколько раз.");

            int soldBefore = session.SoldCount();
            var result = new List<Ticket>();
            foreach (var (row, seat) in places)
            {
                session.Seats[row - 1, seat - 1] = true;
                var ticket = new Ticket { Id = _nextTicketId++, Session = session, Row = row, Seat = seat, Price = session.Price };
                _tickets.Add(ticket);
                result.Add(ticket);
            }

            // Проверка: занятых мест должно стать больше ровно на число купленных билетов.
            Debug.Assert(session.SoldCount() == soldBefore + places.Count, "Счётчик занятых мест не совпадает с числом проданных билетов");

            timer.Stop();
            Trace.TraceInformation($"Продано {result.Count} бил. на сеанс #{session.Id} на сумму {result.Sum(t => t.Price)} руб. ({timer.Elapsed.TotalMilliseconds:F3} мс)");
            PrintHallMap(session);
            return result;
        }
        finally
        {
            Debug.Unindent();   // отступ убирается даже если возникло исключение
        }
    }

    /// <summary>Возврат билета по номеру.</summary>
    public Ticket Return(int ticketId)
    {
        Debug.WriteLine($"Return: билет №{ticketId}");
        Ticket ticket = _tickets.FirstOrDefault(t => t.Id == ticketId)
            ?? throw new ArgumentException($"Билет №{ticketId} не найден.");

        if (ticket.Status == TicketStatus.Returned)
            throw new InvalidOperationException("Этот билет уже возвращён.");
        if (ticket.Session.Start <= DateTime.Now)
            throw new InvalidOperationException("Сеанс уже начался, вернуть билет нельзя.");

        Debug.Assert(ticket.Session.Seats[ticket.Row - 1, ticket.Seat - 1], "Место проданного билета должно быть занято");

        ticket.Status = TicketStatus.Returned;
        ticket.Session.Seats[ticket.Row - 1, ticket.Seat - 1] = false;
        Trace.TraceInformation($"Возврат билета №{ticket.Id}, вернули {ticket.Price} руб.");
        return ticket;
    }

    /// <summary>Выручка по оплаченным (не возвращённым) билетам.</summary>
    public int Revenue()
    {
        int total = 0;
        foreach (Ticket t in _tickets)
        {
            if (t.Status == TicketStatus.Paid)
                total += t.Price;
        }
        Debug.WriteLineIf(total == 0, "Revenue: пока нет ни одного оплаченного билета");
        return total;
    }

    /// <summary>
    /// Печатает схему зала в окно «Вывод» Visual Studio.
    /// Благодаря атрибуту Conditional в сборке Release вызов этого метода полностью убирается компилятором.
    /// </summary>
    [Conditional("DEBUG")]
    private static void PrintHallMap(Session s)
    {
        Debug.WriteLine($"Схема зала «{s.Hall}» (X — занято):");
        for (int r = 0; r < s.Rows; r++)
        {
            var line = new char[s.SeatsPerRow];
            for (int c = 0; c < s.SeatsPerRow; c++)
                line[c] = s.Seats[r, c] ? 'X' : '.';
            Debug.WriteLine($"  ряд {r + 1,2}: {new string(line)}");
        }
    }
}
