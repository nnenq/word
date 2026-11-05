// Практическое занятие № 12. Применение отладочных классов в проекте.
// Вариант 13 — «Кинотеатр»: продажа и возврат билетов на сеансы.
// Отладочные сообщения (Debug) видны в окне «Вывод» Visual Studio,
// журнал работы (Trace) дополнительно пишется в файл cinema_trace.log рядом с программой.

using System.Diagnostics;
using System.Text;
using CinemaDebug;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Настройка журнала: все сообщения Trace (и Debug) дополнительно пишутся в файл.
string logPath = Path.Combine(AppContext.BaseDirectory, "cinema_trace.log");
Trace.Listeners.Add(new TextWriterTraceListener(logPath));
Trace.AutoFlush = true;
Trace.WriteLine($"===== Запуск программы {DateTime.Now:dd.MM.yyyy HH:mm:ss} =====");

#if DEBUG
Console.WriteLine("Сборка: Debug (отладочные сообщения включены)");
#else
Console.WriteLine("Сборка: Release (отладочные сообщения Debug отключены)");
#endif

var service = new BookingService(DemoData.CreateSessions());

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== КИНОТЕАТР =====");
    Console.WriteLine("1 — Афиша (список сеансов)");
    Console.WriteLine("2 — Схема зала");
    Console.WriteLine("3 — Купить билеты");
    Console.WriteLine("4 — Вернуть билет");
    Console.WriteLine("5 — Проданные билеты и выручка");
    Console.WriteLine("0 — Выход");
    Console.Write("Выберите пункт: ");
    string? choice = Console.ReadLine()?.Trim();
    if (choice == "0" || choice == null) break;

    try
    {
        switch (choice)
        {
            case "1":
                foreach (Session s in service.Sessions)
                {
                    string mark = s.Start <= DateTime.Now ? "  (уже начался)" : "";
                    Console.WriteLine(s + mark);
                }
                break;

            case "2":
                Session hall = service.FindSession(ReadInt("Номер сеанса: "));
                ShowHall(hall);
                break;

            case "3":
                int sessionId = ReadInt("Номер сеанса: ");
                ShowHall(service.FindSession(sessionId));
                Console.Write("Места через пробел в виде ряд-место (например 5-7 5-8): ");
                var places = ParsePlaces(Console.ReadLine() ?? "");
                List<Ticket> bought = service.Buy(sessionId, places);
                Console.WriteLine("Покупка выполнена:");
                bought.ForEach(t => Console.WriteLine("  " + t));
                Console.WriteLine($"Итого к оплате: {bought.Sum(t => t.Price)} руб.");
                break;

            case "4":
                Ticket returned = service.Return(ReadInt("Номер билета: "));
                Console.WriteLine($"Билет №{returned.Id} возвращён, к выдаче {returned.Price} руб.");
                break;

            case "5":
                if (service.Tickets.Count == 0) Console.WriteLine("Билетов пока не продано.");
                foreach (Ticket t in service.Tickets) Console.WriteLine(t);
                Console.WriteLine($"Выручка: {service.Revenue()} руб.");
                break;

            default:
                Console.WriteLine("Нет такого пункта меню.");
                break;
        }
    }
    catch (FormatException)
    {
        Console.WriteLine("Ошибка: введите число (места — в виде ряд-место, например 5-7).");
        Trace.TraceWarning("Неверный формат ввода");
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        // ArgumentOutOfRangeException тоже попадает сюда: он наследник ArgumentException.
        Console.WriteLine("Ошибка: " + ex.Message);
        Trace.TraceWarning(ex.GetType().Name + ": " + ex.Message);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Непредвиденная ошибка: " + ex.Message);
        Trace.TraceError(ex.ToString());
    }
}

Trace.WriteLine($"===== Завершение работы. Выручка: {service.Revenue()} руб. =====");
Trace.Flush();
Console.WriteLine($"Журнал работы сохранён в файл: {logPath}");

static int ReadInt(string prompt)
{
    Console.Write(prompt);
    return int.Parse((Console.ReadLine() ?? "").Trim());
}

static List<(int Row, int Seat)> ParsePlaces(string text)
{
    var result = new List<(int, int)>();
    foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        string[] rs = part.Split('-');
        if (rs.Length != 2) throw new FormatException();
        result.Add((int.Parse(rs[0]), int.Parse(rs[1])));
    }
    return result;
}

static void ShowHall(Session s)
{
    Console.WriteLine($"{s.Movie}, {s.Start:dd.MM HH:mm}, зал «{s.Hall}», цена {s.Price} руб.");
    Console.Write("        ");
    for (int c = 1; c <= s.SeatsPerRow; c++) Console.Write($"{c,3}");
    Console.WriteLine();
    for (int r = 1; r <= s.Rows; r++)
    {
        Console.Write($"ряд {r,2}: ");
        for (int c = 1; c <= s.SeatsPerRow; c++)
            Console.Write(s.Seats[r - 1, c - 1] ? "  X" : "  .");
        Console.WriteLine();
    }
    Console.WriteLine("(. — свободно, X — занято)");
}
