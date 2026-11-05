// Классы предметной области: фильм, сеанс, билет.

namespace CinemaDebug;

public class Movie
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public int DurationMin { get; init; }
    public string AgeRating { get; init; } = "0+";

    public override string ToString() => $"{Title} ({AgeRating}, {DurationMin} мин)";
}

public class Session
{
    public int Id { get; init; }
    public Movie Movie { get; init; } = null!;
    public string Hall { get; init; } = "";
    public int Rows { get; init; }
    public int SeatsPerRow { get; init; }
    public DateTime Start { get; init; }
    public int Price { get; init; }

    /// <summary>Схема зала: true — место занято.</summary>
    public bool[,] Seats { get; private set; } = new bool[0, 0];

    public int Capacity => Rows * SeatsPerRow;

    public void CreateSeats() => Seats = new bool[Rows, SeatsPerRow];

    public int SoldCount()
    {
        int count = 0;
        foreach (bool taken in Seats)
            if (taken) count++;
        return count;
    }

    public override string ToString() =>
        $"#{Id} {Start:dd.MM HH:mm} | {Movie.Title,-22} | {Hall,-8} | {Price} руб. | свободно {Capacity - SoldCount()} из {Capacity}";
}

public enum TicketStatus { Paid, Returned }

public class Ticket
{
    public int Id { get; init; }
    public Session Session { get; init; } = null!;
    public int Row { get; init; }
    public int Seat { get; init; }
    public int Price { get; init; }
    public TicketStatus Status { get; set; } = TicketStatus.Paid;

    public override string ToString() =>
        $"Билет №{Id}: {Session.Movie.Title}, {Session.Start:dd.MM HH:mm}, {Session.Hall}, ряд {Row}, место {Seat}, {Price} руб." +
        (Status == TicketStatus.Returned ? " [ВОЗВРАЩЁН]" : "");
}
