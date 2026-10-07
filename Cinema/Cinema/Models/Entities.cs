using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cinema.Models;

/// <summary>Коды ролей (хранятся в таблице Roles и в cookie пользователя).</summary>
public static class RoleCodes
{
    public const string Admin = "admin";
    public const string Cashier = "cashier";
    public const string Viewer = "viewer";
}

public class Role
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = "";
    [MaxLength(50)] public string Name { get; set; } = "";
    public List<User> Users { get; set; } = new();
}

public class User
{
    public int Id { get; set; }
    [MaxLength(20)] public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(100)] public string Email { get; set; } = "";
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Genre
{
    public int Id { get; set; }
    [MaxLength(50)] public string Name { get; set; } = "";
    public List<Movie> Movies { get; set; } = new();
}

public class Movie
{
    public int Id { get; set; }
    [MaxLength(100)] public string Title { get; set; } = "";
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
    public int DurationMin { get; set; }
    [MaxLength(5)] public string AgeRating { get; set; } = "0+";
    public int Year { get; set; }
    [MaxLength(100)] public string Director { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    /// <summary>Фильм в прокате (показывается в афише).</summary>
    public bool IsActive { get; set; } = true;
    public List<Session> Sessions { get; set; } = new();

    public static readonly string[] AgeRatings = { "0+", "6+", "12+", "16+", "18+" };
}

public class Hall
{
    public int Id { get; set; }
    [MaxLength(50)] public string Name { get; set; } = "";
    public int Rows { get; set; }
    public int SeatsPerRow { get; set; }
    [MaxLength(200)] public string Description { get; set; } = "";
    public List<Session> Sessions { get; set; } = new();

    [NotMapped] public int Capacity => Rows * SeatsPerRow;
}

/// <summary>Сеанс — показ фильма в зале в заданное время по заданной цене.</summary>
public class Session
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public DateTime StartTime { get; set; }
    /// <summary>Цена билета, руб.</summary>
    public int Price { get; set; }
    public List<Ticket> Tickets { get; set; } = new();

    /// <summary>Время окончания с учётом 15 минут на уборку зала.</summary>
    public DateTime EndTimeWithCleaning(int durationMin) => StartTime.AddMinutes(durationMin + 15);
}

public enum TicketStatus { Paid = 0, Returned = 1 }

public class Ticket
{
    public int Id { get; set; }
    [MaxLength(12)] public string Code { get; set; } = "";
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public int Row { get; set; }
    public int Seat { get; set; }
    public int Price { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Paid;
    /// <summary>Зритель, купивший билет на сайте (null — продажа на кассе без аккаунта).</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(100)] public string BuyerName { get; set; } = "";
    /// <summary>Кассир, продавший билет (null — покупка на сайте).</summary>
    public int? SoldById { get; set; }
    public User? SoldBy { get; set; }
    [MaxLength(20)] public string Channel { get; set; } = "Сайт";
    [MaxLength(20)] public string Payment { get; set; } = "Карта";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReturnedAt { get; set; }
}
