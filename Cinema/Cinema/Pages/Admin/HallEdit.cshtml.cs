using System.ComponentModel.DataAnnotations;
using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Admin;

public class HallInput
{
    [Required(ErrorMessage = "Введите название зала."), StringLength(50, MinimumLength = 2, ErrorMessage = "Название — от 2 до 50 символов.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Укажите количество рядов."), Range(1, 30, ErrorMessage = "Рядов — от 1 до 30.")]
    public int? Rows { get; set; }

    [Required(ErrorMessage = "Укажите количество мест в ряду."), Range(1, 40, ErrorMessage = "Мест в ряду — от 1 до 40.")]
    public int? SeatsPerRow { get; set; }

    [StringLength(200)] public string? Description { get; set; }
}

public class HallEditModel : AppPage
{
    readonly CinemaDb db;
    public HallEditModel(CinemaDb db) => this.db = db;

    public int? Id { get; set; }
    [BindProperty] public HallInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        if (id == null) return Page();
        var h = await db.Halls.FindAsync(id);
        if (h == null) return NotFound();
        Input = new HallInput { Name = h.Name, Rows = h.Rows, SeatsPerRow = h.SeatsPerRow, Description = h.Description };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        Input.Name = Input.Name?.Trim() ?? "";
        if ((await db.Halls.ToListAsync()).Any(h => h.Id != id && string.Equals(h.Name, Input.Name, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError("Input.Name", "Зал с таким названием уже есть.");

        if (id != null && ModelState.IsValid)
        {
            // Нельзя уменьшить зал так, что проданные места окажутся за его пределами
            var outside = await db.Tickets.AnyAsync(t => t.Session.HallId == id && t.Status == TicketStatus.Paid
                && t.Session.StartTime > DateTime.Now && (t.Row > Input.Rows || t.Seat > Input.SeatsPerRow));
            if (outside) ModelState.AddModelError("", "Нельзя уменьшить зал: на предстоящие сеансы проданы билеты на места, которые исчезнут.");
        }
        if (!ModelState.IsValid) return Page();

        Hall h;
        if (id == null) { h = new Hall(); db.Halls.Add(h); }
        else
        {
            var found = await db.Halls.FindAsync(id);
            if (found == null) return NotFound();
            h = found;
        }
        h.Name = Input.Name;
        h.Rows = Input.Rows!.Value;
        h.SeatsPerRow = Input.SeatsPerRow!.Value;
        h.Description = Input.Description?.Trim() ?? "";
        await db.SaveChangesAsync();
        Success(id == null ? $"Зал «{h.Name}» добавлен." : $"Зал «{h.Name}» сохранён.");
        return Redirect("/Admin/Halls");
    }
}
