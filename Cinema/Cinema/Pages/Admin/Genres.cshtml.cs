using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class GenresModel : AppPage
{
    readonly CinemaDb db;
    public GenresModel(CinemaDb db) => this.db = db;

    public List<Genre> Genres { get; set; } = new();

    public async Task OnGetAsync() =>
        Genres = await db.Genres.Include(g => g.Movies).OrderBy(g => g.Name).ToListAsync();

    string? Validate(string? name, int? exceptId)
    {
        name = name?.Trim() ?? "";
        if (name.Length < 2 || name.Length > 50) return "Название жанра — от 2 до 50 символов.";
        if (db.Genres.AsEnumerable().Any(g => g.Id != exceptId && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            return $"Жанр «{name}» уже есть.";
        return null;
    }

    public async Task<IActionResult> OnPostAddAsync(string? name)
    {
        if (Validate(name, null) is string err) Error(err);
        else
        {
            db.Genres.Add(new Genre { Name = name!.Trim() });
            await db.SaveChangesAsync();
            Success($"Жанр «{name!.Trim()}» добавлен.");
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string? name)
    {
        var g = await db.Genres.FindAsync(id);
        if (g == null) Error("Жанр не найден.");
        else if (Validate(name, id) is string err) Error(err);
        else
        {
            g.Name = name!.Trim();
            await db.SaveChangesAsync();
            Success($"Жанр переименован в «{g.Name}».");
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var g = await db.Genres.Include(x => x.Movies).FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) Error("Жанр не найден.");
        else if (g.Movies.Count > 0) Error($"Нельзя удалить жанр «{g.Name}»: к нему относятся фильмы ({g.Movies.Count}).");
        else
        {
            db.Genres.Remove(g);
            await db.SaveChangesAsync();
            Success($"Жанр «{g.Name}» удалён.");
        }
        return RedirectToPage();
    }
}
