using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Дисциплины: список, добавление, удаление.</summary>
public class DisciplinesModel : AppPage
{
    readonly AppDb db;
    readonly Audit audit;
    readonly MaterialStore materials;
    public DisciplinesModel(AppDb db, Audit audit, MaterialStore materials) { this.db = db; this.audit = audit; this.materials = materials; }

    public record Row(Discipline D, int Topics, int Lectures, int Questions, int Students);
    public List<Row> Rows { get; set; } = new();

    [BindProperty, Required(ErrorMessage = "Введите название."), StringLength(150, ErrorMessage = "Не длиннее 150 символов.")]
    public string Title { get; set; } = "";
    [BindProperty, StringLength(1000)] public string? Description { get; set; }

    async Task LoadAsync()
    {
        Rows = await db.Disciplines.OrderBy(d => d.Title).Select(d => new Row(d,
            d.Topics.Count, d.Topics.SelectMany(t => t.Lectures).Count(),
            d.Topics.SelectMany(t => t.Lectures).SelectMany(l => l.ControlQuestions).Count() + d.Topics.SelectMany(t => t.Lectures).Where(l => l.Test != null).SelectMany(l => l.Test!.Questions).Count(),
            db.LectureProgress.Where(p => p.Lecture.Topic.DisciplineId == d.Id).Select(p => p.StudentId).Distinct().Count())).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Title = Title?.Trim() ?? "";
        if (await db.Disciplines.AnyAsync(d => d.Title == Title)) ModelState.AddModelError(nameof(Title), "Дисциплина с таким названием уже есть.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var d = new Discipline { Title = Title, Description = Description?.Trim() ?? "" };
        db.Disciplines.Add(d);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Дисциплина добавлена", d.Title);
        Success($"Дисциплина «{d.Title}» добавлена. Добавьте темы и лекции.");
        return Redirect($"/Teacher/Discipline/{d.Id}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var d = await db.Disciplines.FindAsync(id);
        if (d == null) return NotFound();
        // Файлы лекций удаляются с диска, варианты ответов теста — до каскадного удаления
        foreach (var l in await db.Lectures.Where(l => l.Topic.DisciplineId == id).ToListAsync()) materials.Delete(l);
        await db.CleanupTestOptionsAsync(db.Tests.Where(t => t.Lecture.Topic.DisciplineId == id).Select(t => t.Id));
        db.Disciplines.Remove(d);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Дисциплина удалена", $"{d.Title} — вместе с темами, лекциями, вопросами и результатами студентов");
        Success($"Дисциплина «{d.Title}» удалена.");
        return RedirectToPage();
    }
}
