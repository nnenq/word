using Cinema.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
            new Movie { Title = "Звёздный рубеж", Poster = "/posters/star.jpg", Genre = g["Фантастика"], DurationMin = 142, AgeRating = "12+", Year = 2026, Director = "А. Вершинин",
                Description = "Экипаж исследовательского корабля получает сигнал с края Солнечной системы и отправляется навстречу неизвестности." },
            new Movie { Title = "Бабушка на связи", Poster = "/posters/grandma.jpg", Genre = g["Комедия"], DurationMin = 98, AgeRating = "6+", Year = 2026, Director = "Е. Котова",
                Description = "Бабушка осваивает смартфон и случайно становится звездой соцсетей. Семейная комедия для всех возрастов." },
            new Movie { Title = "Тихий берег", Poster = "/posters/shore.jpg", Genre = g["Драма"], DurationMin = 117, AgeRating = "16+", Year = 2025, Director = "М. Лаврентьев",
                Description = "История возвращения в родной приморский посёлок и попытки исправить ошибки прошлого." },
            new Movie { Title = "Котёнок Тим и большой город", Poster = "/posters/kitten.jpg", Genre = g["Мультфильм"], DurationMin = 85, AgeRating = "0+", Year = 2026, Director = "О. Рябова",
                Description = "Любопытный котёнок теряется в мегаполисе и находит новых друзей на пути домой." },
            new Movie { Title = "Последний рубеж", Poster = "/posters/action.jpg", Genre = g["Боевик"], DurationMin = 128, AgeRating = "18+", Year = 2026, Director = "Д. Громов",
                Description = "Бывший спецназовец берётся за последнее задание, которое оказывается ловушкой." },
            new Movie { Title = "Дом на холме", Poster = "/posters/house.jpg", Genre = g["Ужасы"], DurationMin = 104, AgeRating = "18+", Year = 2025, Director = "С. Орлов",
                Description = "Семья переезжает в старинный особняк, где по ночам происходят необъяснимые вещи." },
            new Movie { Title = "Тайна северного маяка", Poster = "/posters/lighthouse.jpg", Genre = g["Приключения"], DurationMin = 111, AgeRating = "12+", Year = 2026, Director = "И. Белова",
                Description = "Трое школьников раскрывают загадку заброшенного маяка и находят старинную карту." },
            new Movie { Title = "Старый фильм", Poster = "/posters/old.jpg", Genre = g["Драма"], DurationMin = 95, AgeRating = "12+", Year = 2020, Director = "Н. Петров",
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
        var sessions = MakeSessions(movies.Where(m => m.IsActive).ToArray(), halls, DateTime.Today, 7);
        db.Sessions.AddRange(sessions);

        // Ещё несколько зрителей, чтобы продажи через сайт были у разных людей
        var buyers = new[]
        {
            ("orlova", "Орлова Дарья"), ("kim", "Ким Артём"), ("belov", "Белов Игорь"), ("sokolova", "Соколова Ева"),
            ("morozov", "Морозов Илья"), ("lebedeva", "Лебедева Нина"), ("volkov", "Волков Денис"), ("zaitseva", "Зайцева Ольга"),
        }.Select(x => U(x.Item1, "Viewer123", x.Item2, x.Item1 + "@mail.ru", viewer)).ToArray();
        db.Users.AddRange(buyers);

        // Проданные билеты заказами: в заказе 1–4 места рядом в одном ряду, часть через сайт, часть на кассе
        int code = 1;
        var taken = sessions.ToDictionary(s => s, _ => new HashSet<(int, int)>());
        List<Ticket>? Order(Session s, int seats, User? buyer, DateTime created, string? payment = null)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int row = rnd.Next(1, s.Hall.Rows + 1), first = rnd.Next(1, s.Hall.SeatsPerRow - seats + 2);
                var places = Enumerable.Range(first, seats).Select(p => (row, p)).ToList();
                if (places.Any(taken[s].Contains)) continue;
                places.ForEach(p => taken[s].Add(p));
                var list = places.Select(p => new Ticket
                {
                    Code = $"T-{code++:D6}", Session = s, Row = p.row, Seat = p.p, Price = s.Price,
                    User = buyer, BuyerName = buyer?.FullName ?? "Покупатель на кассе",
                    SoldBy = buyer == null ? users[1] : null, Channel = buyer == null ? "Касса" : "Сайт",
                    Payment = payment ?? (buyer != null || rnd.Next(2) == 0 ? "Карта" : "Наличные"), CreatedAt = created,
                }).ToList();
                db.Tickets.AddRange(list);
                return list;
            }
            return null;
        }
        DateTime Bought(Session s) => Earlier(s.StartTime.AddHours(-rnd.Next(3, 72)), DateTime.Now.AddMinutes(-rnd.Next(5, 300)));

        foreach (var s in sessions.Where((_, i) => i % 2 == 0))
        {
            int orders = rnd.Next(2, Math.Max(3, s.Hall.Capacity / 7));
            for (int i = 0; i < orders; i++)
                Order(s, rnd.Next(1, 5), rnd.Next(100) < 55 ? buyers[rnd.Next(buyers.Length)] : null, Bought(s));
        }

        // У демо-зрителей Петрова и Ивановой по несколько понятных заказов: два предстоящих и один возвращённый
        var future = sessions.Where(s => s.StartTime > DateTime.Now.AddHours(3)).ToList();
        foreach (var (who, shift) in new[] { (users[2], 0), (users[3], 5) })
        {
            Order(future[(shift + 4) % future.Count], 2, who, DateTime.Now.AddHours(-20));
            Order(future[(shift + 30) % future.Count], 3, who, DateTime.Now.AddHours(-2));
            var back = Order(future[(shift + 15) % future.Count], 1, who, DateTime.Now.AddDays(-1));
            back?.ForEach(t => { t.Status = TicketStatus.Returned; t.ReturnedAt = DateTime.Now.AddHours(-12); });
        }
        db.SaveChanges();
    }

    static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;

    /// <summary>Сеансы на несколько дней вперёд: в каждом зале по 4 показа в день, фильмы чередуются.</summary>
    static List<Session> MakeSessions(Movie[] active, Hall[] halls, DateTime from, int days)
    {
        int[] startHours = { 10, 13, 16, 19 };
        var sessions = new List<Session>();
        for (int day = 0; day < days; day++)
            for (int h = 0; h < halls.Length; h++)
                for (int k = 0; k < startHours.Length; k++)
                {
                    var date = from.AddDays(day);
                    var movie = active[(date.DayOfYear + h * 2 + k) % active.Length];
                    int basePrice = h == 2 ? 600 : h == 1 ? 350 : 400;
                    sessions.Add(new Session
                    {
                        Movie = movie, Hall = halls[h], StartTime = date.AddHours(startHours[k] + h * 0.25),
                        Price = basePrice + (startHours[k] >= 19 ? 100 : 0),
                    });
                }
        return sessions;
    }

    static readonly Dictionary<string, string> Posters = new()
    {
        ["Звёздный рубеж"] = "/posters/star.jpg", ["Бабушка на связи"] = "/posters/grandma.jpg", ["Тихий берег"] = "/posters/shore.jpg",
        ["Котёнок Тим и большой город"] = "/posters/kitten.jpg", ["Последний рубеж"] = "/posters/action.jpg", ["Дом на холме"] = "/posters/house.jpg",
        ["Тайна северного маяка"] = "/posters/lighthouse.jpg", ["Старый фильм"] = "/posters/old.jpg",
    };

    /// <summary>
    /// Выполняется при каждом запуске: дополняет базу, созданную прошлой версией программы (столбец постера),
    /// и добавляет сеансы, чтобы расписание всегда было заполнено на неделю вперёд.
    /// </summary>
    public static void Upgrade(CinemaDb db)
    {
        bool hasPoster = db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Movies') WHERE name = 'Poster'")
            .AsEnumerable().First() > 0;
        if (!hasPoster)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Movies ADD COLUMN Poster TEXT NULL");
            foreach (var (title, file) in Posters)
                db.Database.ExecuteSql($"UPDATE Movies SET Poster = {file} WHERE Title = {title}");
        }

        var active = db.Movies.Where(m => m.IsActive).OrderBy(m => m.Id).ToArray();
        var halls = db.Halls.OrderBy(h => h.Id).ToArray();
        if (active.Length == 0 || halls.Length == 0) return;
        var last = db.Sessions.Max(s => (DateTime?)s.StartTime);
        var from = last == null || last.Value.Date < DateTime.Today ? DateTime.Today : last.Value.Date.AddDays(1);
        int days = (DateTime.Today.AddDays(7) - from).Days;
        if (days <= 0) return;
        db.Sessions.AddRange(MakeSessions(active, halls, from, days));
        db.SaveChanges();
    }
}
