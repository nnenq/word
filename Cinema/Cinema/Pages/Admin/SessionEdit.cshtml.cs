using System.ComponentModel.DataAnnotations;
using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class SessionInput
{
    [Required(ErrorMessage = "Выберите фильм.")] public int? MovieId { get; set; }
    [Required(ErrorMessage = "Выберите зал.")] public int? HallId { get; set; }
    [Required(ErrorMessage = "Укажите дату и время начала.")] public DateTime? StartTime { get; set; }
    [Required(ErrorMessage = "Укажите цену билета."), Range(50, 5000, ErrorMessage = "Цена билета — от 50 до 5000 ₽.")]
    public int? Price { get; set; }
}

public class SessionEditModel : AppPage
{
    readonly CinemaDb db;
    public SessionEditModel(CinemaDb db) => this.db = db;

    public int? Id { get; set; }
    public int SoldCount { get; set; }
    [BindProperty] public SessionInput Input { get; set; } = new();
    public List<SelectListItem> MovieItems { get; set; } = new();
    public List<SelectListItem> HallItems { get; set; } = new();

    async Task LoadListsAsync()
    {
        MovieItems = await db.Movies.Where(m => m.IsActive || m.Id == Input.MovieId).OrderBy(m => m.Title)
            .Select(m => new SelectListItem($"{m.Title} ({m.DurationMin} мин)", m.Id.ToString())).ToListAsync();
        HallItems = await db.Halls.OrderBy(h => h.Name).Select(h => new SelectListItem(h.Name, h.Id.ToString())).ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        if (id == null)
        {
            Input.StartTime = DateTime.Today.AddDays(1).AddHours(18);
            Input.Price = 400;
        }
        else
        {
            var s = await db.Sessions.FindAsync(id);
            if (s == null) return NotFound();
            Input = new SessionInput { MovieId = s.MovieId, HallId = s.HallId, StartTime = s.StartTime, Price = s.Price };
            SoldCount = await db.Tickets.CountAsync(t => t.SessionId == id && t.Status == TicketStatus.Paid);
        }
        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        Session? existing = null;
        if (id != null)
        {
            existing = await db.Sessions.FindAsync(id);
            if (existing == null) return NotFound();
            SoldCount = await db.Tickets.CountAsync(t => t.SessionId == id && t.Status == TicketStatus.Paid);
            if (SoldCount > 0)
            {
                // Проданы билеты: фильм, зал и время не меняются (поля формы заблокированы)
                Input.MovieId = existing.MovieId;
                Input.HallId = existing.HallId;
                Input.StartTime = existing.StartTime;
                ModelState.Remove("Input.MovieId"); ModelState.Remove("Input.HallId"); ModelState.Remove("Input.StartTime");
            }
        }

        if (ModelState.IsValid)
        {
            var movie = await db.Movies.FindAsync(Input.MovieId);
            var hall = await db.Halls.FindAsync(Input.HallId);
            if (movie == null) ModelState.AddModelError("Input.MovieId", "Такого фильма нет.");
            else if (!movie.IsActive && existing?.MovieId != movie.Id) ModelState.AddModelError("Input.MovieId", "Фильм снят с проката — сначала верните его в прокат.");
            if (hall == null) ModelState.AddModelError("Input.HallId", "Такого зала нет.");

            var start = Input.StartTime!.Value;
            bool timeChanged = existing == null || existing.StartTime != start;
            if (timeChanged && start <= DateTime.Now.AddMinutes(30))
                ModelState.AddModelError("Input.StartTime", "Сеанс можно запланировать не раньше чем через 30 минут от текущего времени.");
            if (start.Hour < 8 && start.Hour >= 3)
                ModelState.AddModelError("Input.StartTime", "Кинотеатр не работает с 03:00 до 08:00.");

            if (movie != null && hall != null && ModelState.IsValid)
            {
                // Проверка пересечения с другими сеансами в этом зале
                var end = start.AddMinutes(movie.DurationMin + 15);
                var others = await db.Sessions.Include(s => s.Movie)
                    .Where(s => s.HallId == hall.Id && s.Id != id && s.StartTime < end && s.StartTime > start.AddHours(-6)).ToListAsync();
                var conflict = others.FirstOrDefault(s => s.StartTime.AddMinutes(s.Movie.DurationMin + 15) > start);
                if (conflict != null)
                    ModelState.AddModelError("Input.StartTime",
                        $"Зал занят: в {conflict.StartTime:HH:mm} идёт «{conflict.Movie.Title}» до {conflict.StartTime.AddMinutes(conflict.Movie.DurationMin + 15):HH:mm} (с уборкой).");
            }
        }
        if (!ModelState.IsValid) { await LoadListsAsync(); return Page(); }

        var session = existing ?? new Session();
        if (existing == null) db.Sessions.Add(session);
        session.MovieId = Input.MovieId!.Value;
        session.HallId = Input.HallId!.Value;
        session.StartTime = Input.StartTime!.Value;
        session.Price = Input.Price!.Value;
        await db.SaveChangesAsync();
        Success(existing == null ? $"Сеанс на {session.StartTime:dd.MM.yyyy HH:mm} добавлен." : "Изменения сеанса сохранены.");
        return Redirect("/Admin/Sessions?from=" + session.StartTime.ToString("yyyy-MM-dd"));
    }
}
