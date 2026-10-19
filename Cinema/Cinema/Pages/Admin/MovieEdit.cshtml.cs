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
    static readonly string[] PosterTypes = { ".jpg", ".jpeg", ".png", ".webp" };
    const long MaxPosterBytes = 5 * 1024 * 1024;

    readonly CinemaDb db;
    readonly IWebHostEnvironment env;
    public MovieEditModel(CinemaDb db, IWebHostEnvironment env) { this.db = db; this.env = env; }

    public int? Id { get; set; }
    public string? CurrentPoster { get; set; }
    [BindProperty] public MovieInput Input { get; set; } = new();
    [BindProperty] public IFormFile? PosterFile { get; set; }
    [BindProperty] public bool RemovePoster { get; set; }
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
        CurrentPoster = m.Poster;
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
        string? ext = Path.GetExtension(PosterFile?.FileName ?? "").ToLowerInvariant();
        if (PosterFile != null && !PosterTypes.Contains(ext))
            ModelState.AddModelError(nameof(PosterFile), "Постер — картинка в формате JPG, PNG или WEBP.");
        else if (PosterFile != null && PosterFile.Length > MaxPosterBytes)
            ModelState.AddModelError(nameof(PosterFile), "Файл постера — не больше 5 МБ.");
        if (!ModelState.IsValid)
        {
            await LoadGenresAsync();
            if (id != null) CurrentPoster = (await db.Movies.FindAsync(id))?.Poster;
            return Page();
        }

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
        if (PosterFile != null)
        {
            // Загруженные постеры хранятся в wwwroot/posters/uploads под случайным именем
            var dir = Path.Combine(env.WebRootPath, "posters", "uploads");
            Directory.CreateDirectory(dir);
            var name = Guid.NewGuid().ToString("N") + ext;
            await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
                await PosterFile.CopyToAsync(fs);
            m.Poster = "/posters/uploads/" + name;
        }
        else if (RemovePoster) m.Poster = null;
        await db.SaveChangesAsync();
        Success(id == null ? $"Фильм «{m.Title}» добавлен." : $"Изменения фильма «{m.Title}» сохранены.");
        return Redirect("/Admin/Movies");
    }
}
