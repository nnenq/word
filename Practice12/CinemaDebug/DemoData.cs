// Демонстрационные данные: фильмы и сеансы на сегодня и завтра.

namespace CinemaDebug;

public static class DemoData
{
    public static List<Session> CreateSessions()
    {
        var movies = new[]
        {
            new Movie { Id = 1, Title = "Звёздный путь домой", DurationMin = 128, AgeRating = "12+" },
            new Movie { Id = 2, Title = "Тайна старого маяка", DurationMin = 104, AgeRating = "16+" },
            new Movie { Id = 3, Title = "Весёлые еноты", DurationMin = 86, AgeRating = "6+" },
        };

        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        var sessions = new List<Session>
        {
            new() { Id = 1, Movie = movies[2], Hall = "Малый", Rows = 5, SeatsPerRow = 8, Start = today.AddHours(10), Price = 250 },
            new() { Id = 2, Movie = movies[0], Hall = "Большой", Rows = 8, SeatsPerRow = 12, Start = today.AddHours(19), Price = 450 },
            new() { Id = 3, Movie = movies[1], Hall = "Малый", Rows = 5, SeatsPerRow = 8, Start = today.AddHours(21.5), Price = 400 },
            new() { Id = 4, Movie = movies[2], Hall = "Большой", Rows = 8, SeatsPerRow = 12, Start = tomorrow.AddHours(11), Price = 250 },
            new() { Id = 5, Movie = movies[0], Hall = "Большой", Rows = 8, SeatsPerRow = 12, Start = tomorrow.AddHours(18), Price = 450 },
            new() { Id = 6, Movie = movies[1], Hall = "Малый", Rows = 5, SeatsPerRow = 8, Start = tomorrow.AddHours(20.5), Price = 400 },
        };
        sessions.ForEach(s => s.CreateSeats());
        return sessions;
    }
}
