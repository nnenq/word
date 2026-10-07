using Cinema.Models;
using Microsoft.AspNetCore.Identity;

namespace Cinema.Data;

/// <summary>Начальные данные: роли, демо-пользователи, жанры, фильмы, залы, сеансы на неделю и проданные билеты.</summary>
public static class Seed
{
    public static void Run(CinemaDb db, IPasswordHasher<User> hasher)
    {
        if (db.Roles.Any()) return;

        var admin = new Role { Code = RoleCodes.Admin, Name = "Администратор" };
        var cashier = new Role { Code = RoleCodes.Cashier, Name = "Кассир" };
        var viewer = new Role { Code = RoleCodes.Viewer, Name = "Зритель" };
        db.Roles.AddRange(admin, cashier, viewer);

        User U(string login, string pass, string name, string email, Role role)
        {
            var u = new User { Login = login, FullName = name, Email = email, Role = role, CreatedAt = DateTime.Now.AddDays(-30) };
            u.PasswordHash = hasher.HashPassword(u, pass);
            return u;
        }
        var users = new[]
        {
            U("admin", "Admin123", "Кузнецова Анна", "admin@cinema.local", admin),
            U("cashier", "Cashier123", "Смирнова Ольга", "cashier@cinema.local", cashier),
            U("viewer", "Viewer123", "Петров Алексей", "petrov@mail.ru", viewer),
            U("ivanova", "Viewer123", "Иванова Мария", "ivanova@mail.ru", viewer),
        };
        db.Users.AddRange(users);

        var g = new[] { "Фантастика", "Комедия", "Драма", "Мультфильм", "Боевик", "Ужасы", "Приключения" }
            .ToDictionary(n => n, n => new Genre { Name = n });
        db.Genres.AddRange(g.Values);

        var movies = new[]
        {
            new Movie { Title = "Звёздный рубеж", Genre = g["Фантастика"], DurationMin = 142, AgeRating = "12+", Year = 2026, Director = "А. Вершинин",
                Description = "Экипаж исследовательского корабля получает сигнал с края Солнечной системы и отправляется навстречу неизвестности." },
            new Movie { Title = "Бабушка на связи", Genre = g["Комедия"], DurationMin = 98, AgeRating = "6+", Year = 2026, Director = "Е. Котова",
                Description = "Бабушка осваивает смартфон и случайно становится звездой соцсетей. Семейная комедия для всех возрастов." },
            new Movie { Title = "Тихий берег", Genre = g["Драма"], DurationMin = 117, AgeRating = "16+", Year = 2025, Director = "М. Лаврентьев",
                Description = "История возвращения в родной приморский посёлок и попытки исправить ошибки прошлого." },
            new Movie { Title = "Котёнок Тим и большой город", Genre = g["Мультфильм"], DurationMin = 85, AgeRating = "0+", Year = 2026, Director = "О. Рябова",
                Description = "Любопытный котёнок теряется в мегаполисе и находит новых друзей на пути домой." },
            new Movie { Title = "Последний рубеж", Genre = g["Боевик"], DurationMin = 128, AgeRating = "18+", Year = 2026, Director = "Д. Громов",
                Description = "Бывший спецназовец берётся за последнее задание, которое оказывается ловушкой." },
            new Movie { Title = "Дом на холме", Genre = g["Ужасы"], DurationMin = 104, AgeRating = "18+", Year = 2025, Director = "С. Орлов",
                Description = "Семья переезжает в старинный особняк, где по ночам происходят необъяснимые вещи." },
            new Movie { Title = "Тайна северного маяка", Genre = g["Приключения"], DurationMin = 111, AgeRating = "12+", Year = 2026, Director = "И. Белова",
                Description = "Трое школьников раскрывают загадку заброшенного маяка и находят старинную карту." },
            new Movie { Title = "Старый фильм", Genre = g["Драма"], DurationMin = 95, AgeRating = "12+", Year = 2020, Director = "Н. Петров",
                Description = "Фильм снят с проката и не показывается в афише.", IsActive = false },
        };
        db.Movies.AddRange(movies);

        var halls = new[]
        {
            new Hall { Name = "Зал 1 «Большой»", Rows = 10, SeatsPerRow = 14, Description = "Экран 18 м, Dolby Atmos" },
            new Hall { Name = "Зал 2 «Малый»", Rows = 7, SeatsPerRow = 10, Description = "Уютный зал для камерных показов" },
            new Hall { Name = "Зал 3 «VIP»", Rows = 4, SeatsPerRow = 8, Description = "Кресла-реклайнеры, обслуживание в зале" },
        };
        db.Halls.AddRange(halls);

        // Сеансы на 7 дней: в каждом зале по 4 показа в день
        var rnd = new Random(13);
        var sessions = new List<Session>();
        int[] startHours = { 10, 13, 16, 19 };
        var active = movies.Where(m => m.IsActive).ToArray();
        for (int day = 0; day < 7; day++)
            for (int h = 0; h < halls.Length; h++)
                for (int k = 0; k < startHours.Length; k++)
                {
                    var movie = active[(day + h * 2 + k) % active.Length];
                    int basePrice = h == 2 ? 600 : h == 1 ? 350 : 400;
                    sessions.Add(new Session
                    {
                        Movie = movie, Hall = halls[h], StartTime = DateTime.Today.AddDays(day).AddHours(startHours[k] + h * 0.25),
                        Price = basePrice + (startHours[k] >= 19 ? 100 : 0),
                    });
                }
        db.Sessions.AddRange(sessions);

        // Проданные билеты: часть через сайт (зрители), часть на кассе
        int code = 1;
        foreach (var s in sessions.Where((_, i) => i % 2 == 0))
        {
            int count = rnd.Next(3, Math.Max(4, s.Hall.Capacity / 3));
            var taken = new HashSet<(int, int)>();
            for (int i = 0; i < count; i++)
            {
                var place = (rnd.Next(1, s.Hall.Rows + 1), rnd.Next(1, s.Hall.SeatsPerRow + 1));
                if (!taken.Add(place)) continue;
                bool online = rnd.Next(2) == 0;
                var buyer = online ? users[2 + rnd.Next(2)] : null;
                db.Tickets.Add(new Ticket
                {
                    Code = $"T-{code++:D6}", Session = s, Row = place.Item1, Seat = place.Item2, Price = s.Price,
                    User = buyer, BuyerName = buyer?.FullName ?? "Покупатель на кассе",
                    SoldBy = online ? null : users[1], Channel = online ? "Сайт" : "Касса", Payment = online ? "Карта" : (rnd.Next(2) == 0 ? "Наличные" : "Карта"),
                    CreatedAt = s.StartTime.AddDays(-1) < DateTime.Now ? s.StartTime.AddHours(-3) : DateTime.Now.AddHours(-rnd.Next(1, 48)),
                });
            }
        }
        db.SaveChanges();
    }
}
