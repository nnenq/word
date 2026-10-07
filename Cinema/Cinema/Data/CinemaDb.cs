using Cinema.Models;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data;

/// <summary>Контекст базы данных SQLite (файл cinema.db) — таблицы и связи между ними.</summary>
public class CinemaDb : DbContext
{
    public CinemaDb(DbContextOptions<CinemaDb> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>().HasIndex(r => r.Code).IsUnique();
        b.Entity<User>().HasIndex(u => u.Login).IsUnique();
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Genre>().HasIndex(g => g.Name).IsUnique();
        b.Entity<Movie>().HasOne(m => m.Genre).WithMany(g => g.Movies).HasForeignKey(m => m.GenreId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Hall>().HasIndex(h => h.Name).IsUnique();
        b.Entity<Session>().HasOne(s => s.Movie).WithMany(m => m.Sessions).HasForeignKey(s => s.MovieId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Session>().HasOne(s => s.Hall).WithMany(h => h.Sessions).HasForeignKey(s => s.HallId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Session>().HasIndex(s => s.StartTime);

        b.Entity<Ticket>().HasIndex(t => t.Code).IsUnique();
        b.Entity<Ticket>().HasOne(t => t.Session).WithMany(s => s.Tickets).HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Ticket>().HasOne(t => t.SoldBy).WithMany().HasForeignKey(t => t.SoldById).OnDelete(DeleteBehavior.SetNull);
        // Одно место на сеанс нельзя продать дважды: уникальный индекс только по действующим (оплаченным) билетам
        b.Entity<Ticket>().HasIndex(t => new { t.SessionId, t.Row, t.Seat }).IsUnique().HasFilter("Status = 0");
    }
}
