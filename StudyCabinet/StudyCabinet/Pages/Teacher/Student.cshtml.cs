using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>
/// Прогресс одного студента по всем дисциплинам. Действия преподавателя записываются в журнал:
/// открыть лекцию вручную, выдать дополнительную попытку, сбросить результаты лекции.
/// </summary>
public class StudentModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    readonly Audit audit;
    public StudentModel(AppDb db, StudyService study, Audit audit) { this.db = db; this.study = study; this.audit = audit; }

    public User Student { get; set; } = null!;
    public string? Group { get; set; }
    public List<DisciplinePlan> Plans { get; set; } = new();
    public List<TestAttempt> Attempts { get; set; } = new();

    async Task<bool> LoadAsync(int id)
    {
        var u = await db.Users.Include(x => x.Memberships).ThenInclude(m => m.Group).FirstOrDefaultAsync(x => x.Id == id && x.Role == Roles.Student);
        if (u == null) return false;
        Student = u;
        Group = string.Join(", ", u.Memberships.Select(m => m.Group.Name));
        foreach (var d in await db.Disciplines.OrderBy(d => d.Title).Select(d => d.Id).ToListAsync())
            Plans.Add((await study.PlanAsync(d, id))!);
        Attempts = await db.TestAttempts.Include(a => a.Test).ThenInclude(t => t.Lecture)
            .Where(a => a.StudentId == id && a.Status != AttemptStatus.InProgress).OrderByDescending(a => a.FinishedAt).ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    async Task<(User? s, Lecture? l)> FindAsync(int id, int lectureId) =>
        (await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Student), await db.Lectures.FindAsync(lectureId));

    public async Task<IActionResult> OnPostOpenAsync(int id, int lectureId)
    {
        var (s, l) = await FindAsync(id, lectureId);
        if (s == null || l == null) return NotFound();
        await study.OpenForStudentAsync(id, lectureId);
        await audit.WriteAsync("Лекция открыта вручную", $"{s.FullName}: лекция {l.Number} «{l.Title}»");
        Success($"Лекция {l.Number} открыта для {s.FullName}.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAttemptAsync(int id, int lectureId)
    {
        var (s, l) = await FindAsync(id, lectureId);
        if (s == null || l == null) return NotFound();
        await study.GrantAttemptAsync(id, lectureId);
        await audit.WriteAsync("Выдана дополнительная попытка", $"{s.FullName}: тест по лекции {l.Number} «{l.Title}»");
        Success($"{s.FullName} получил(а) ещё одну попытку теста по лекции {l.Number}.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResetAsync(int id, int lectureId)
    {
        var (s, l) = await FindAsync(id, lectureId);
        if (s == null || l == null) return NotFound();
        await study.ResetLectureAsync(id, lectureId);
        await audit.WriteAsync("Результаты лекции сброшены", $"{s.FullName}: лекция {l.Number} «{l.Title}» — ответы, попытки и отметка о прохождении удалены");
        Success($"Результаты лекции {l.Number} для {s.FullName} сброшены — лекцию нужно пройти заново.");
        return RedirectToPage(new { id });
    }
}
