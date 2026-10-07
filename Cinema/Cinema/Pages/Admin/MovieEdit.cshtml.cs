using System.ComponentModel.DataAnnotations;
using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class MovieInput
{
    [Required(ErrorMessage = "Введите название фильма."), StringLength(100, ErrorMessage = "Название — не длиннее 100 символов.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Выберите жанр.")]
    public int? GenreId { get; set; }

    [Required(ErrorMessage = "Укажите режиссёра."), StringLength(100)]
    public string Director { get; set; } = "";

    [Required(ErrorMessage = "Укажите длительность."), Range(30, 300, ErrorMessage = "Длительность — от 30 до 300 минут.")]
    public int? DurationMin { get; set; }

    [Required(ErrorMessage = "Укажите год выпуска.")]
    public int? Year { get; set; }

    [Required] public string AgeRating { get; set; } = "12+";
    public bool IsActive { get; set; } = true;

    [StringLength(1000, ErrorMessage = "Описание — не длиннее 1000 символов.")]
    public string? Description { get; set; }
}

public class MovieEditModel : AppPage
{
    readonly CinemaDb db;
    public MovieEditModel(CinemaDb db) => this.db = db;

    public int? Id { get; set; }
    [BindProperty] public MovieInput Input { get; set; } = new();
    public List<SelectListItem> GenreItems { get; set; } = new();

    async Task LoadGenresAsync() =>
        GenreItems = await db.Genres.OrderBy(g => g.Name).Select(g => new SelectListItem(g.Name, g.Id.ToString())).ToListAsync();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        await LoadGenresAsync();
        if (id == null) { Input.Year = DateTime.Today.Year; return Page(); }
        var m = await db.Movies.FindAsync(id);
        if (m == null) return NotFound();
        Input = new MovieInput
        {
            Title = m.Title, GenreId = m.GenreId, Director = m.Director, DurationMin = m.DurationMin, Year = m.Year,
            AgeRating = m.AgeRating, IsActive = m.IsActive, Description = m.Description,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        Input.Title = Input.Title?.Trim() ?? "";
        if (Input.Year is int y && (y < 1895 || y > DateTime.Today.Year + 2))
            ModelState.AddModelError("Input.Year", $"Год выпуска — от 1895 до {DateTime.Today.Year + 2}.");
        if (!Movie.AgeRatings.Contains(Input.AgeRating))
            ModelState.AddModelError("Input.AgeRating", "Выберите возрастное ограничение из списка.");
        if (Input.GenreId is int g && !await db.Genres.AnyAsync(x => x.Id == g))
            ModelState.AddModelError("Input.GenreId", "Такого жанра нет.");
        if (!ModelState.IsValid) { await LoadGenresAsync(); return Page(); }

        Movie m;
        if (id == null) { m = new Movie(); db.Movies.Add(m); }
        else
        {
            var found = await db.Movies.FindAsync(id);
            if (found == null) return NotFound();
            m = found;
        }
        m.Title = Input.Title;
        m.GenreId = Input.GenreId!.Value;
        m.Director = Input.Director.Trim();
        m.DurationMin = Input.DurationMin!.Value;
        m.Year = Input.Year!.Value;
        m.AgeRating = Input.AgeRating;
        m.IsActive = Input.IsActive;
        m.Description = Input.Description?.Trim() ?? "";
        await db.SaveChangesAsync();
        Success(id == null ? $"Фильм «{m.Title}» добавлен." : $"Изменения фильма «{m.Title}» сохранены.");
        return Redirect("/Admin/Movies");
    }
}
