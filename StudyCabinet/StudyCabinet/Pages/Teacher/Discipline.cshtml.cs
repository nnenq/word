using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Содержание дисциплины: темы и лекции, их порядок, добавление и удаление.</summary>
public class DisciplineModel : AppPage
{
    readonly AppDb db;
    readonly Audit audit;
    readonly MaterialStore materials;
    public DisciplineModel(AppDb db, Audit audit, MaterialStore materials) { this.db = db; this.audit = audit; this.materials = materials; }

    public Discipline Discipline { get; set; } = null!;
    public List<Topic> Topics { get; set; } = new();

    [BindProperty, Required(ErrorMessage = "Введите название."), StringLength(150)] public string Title { get; set; } = "";
    [BindProperty, StringLength(1000)] public string? Description { get; set; }

    async Task<bool> LoadAsync(int id)
    {
        var d = await db.Disciplines.FindAsync(id);
        if (d == null) return false;
        Discipline = d;
        Topics = await db.Topics.Where(t => t.DisciplineId == id)
            .Include(t => t.Lectures).ThenInclude(l => l.ControlQuestions)
            .Include(t => t.Lectures).ThenInclude(l => l.Test).ThenInclude(t => t!.Questions)
            .OrderBy(t => t.Order).ThenBy(t => t.Id).AsSplitQuery().ToListAsync();
        foreach (var t in Topics) t.Lectures = t.Lectures.OrderBy(l => l.Order).ThenBy(l => l.Id).ToList();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Title = Discipline.Title; Description = Discipline.Description;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Title = Title?.Trim() ?? "";
        if (await db.Disciplines.AnyAsync(d => d.Title == Title && d.Id != id)) ModelState.AddModelError(nameof(Title), "Дисциплина с таким названием уже есть.");
        if (!ModelState.IsValid) return Page();
        Discipline.Title = Title; Discipline.Description = Description?.Trim() ?? "";
        await db.SaveChangesAsync();
        Success("Дисциплина сохранена.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddTopicAsync(int id, string? topicTitle)
    {
        if (!await LoadAsync(id)) return NotFound();
        topicTitle = topicTitle?.Trim();
        if (string.IsNullOrEmpty(topicTitle) || topicTitle.Length > 150) { Error("Введите название темы (до 150 символов)."); return RedirectToPage(new { id }); }
        db.Topics.Add(new Topic { DisciplineId = id, Title = topicTitle, Order = Topics.Count == 0 ? 1 : Topics.Max(t => t.Order) + 1 });
        await db.SaveChangesAsync();
        Success($"Тема «{topicTitle}» добавлена.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRenameTopicAsync(int id, int topicId, string? topicTitle)
    {
        var t = await db.Topics.FirstOrDefaultAsync(x => x.Id == topicId && x.DisciplineId == id);
        if (t == null) return NotFound();
        topicTitle = topicTitle?.Trim();
        if (string.IsNullOrEmpty(topicTitle) || topicTitle.Length > 150) { Error("Введите название темы (до 150 символов)."); return RedirectToPage(new { id }); }
        t.Title = topicTitle;
        await db.SaveChangesAsync();
        Success("Тема переименована.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteTopicAsync(int id, int topicId)
    {
        var t = await db.Topics.FirstOrDefaultAsync(x => x.Id == topicId && x.DisciplineId == id);
        if (t == null) return NotFound();
        foreach (var l in await db.Lectures.Where(l => l.TopicId == topicId).ToListAsync()) materials.Delete(l);
        await db.CleanupTestOptionsAsync(db.Tests.Where(x => x.Lecture.TopicId == topicId).Select(x => x.Id));
        db.Topics.Remove(t);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Тема удалена", $"{t.Title} — вместе с лекциями и результатами");
        Success($"Тема «{t.Title}» удалена.");
        return RedirectToPage(new { id });
    }

    /// <summary>Перемещение темы или лекции вверх/вниз: меняется местами с соседней.</summary>
    public async Task<IActionResult> OnPostMoveTopicAsync(int id, int topicId, int dir)
    {
        if (!await LoadAsync(id)) return NotFound();
        Swap(Topics, Topics.FindIndex(t => t.Id == topicId), dir, (t, o) => t.Order = o);
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMoveLectureAsync(int id, int lectureId, int dir)
    {
        if (!await LoadAsync(id)) return NotFound();
        var topic = Topics.FirstOrDefault(t => t.Lectures.Any(l => l.Id == lectureId));
        if (topic == null) return NotFound();
        Swap(topic.Lectures, topic.Lectures.FindIndex(l => l.Id == lectureId), dir, (l, o) => l.Order = o);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Порядок лекций изменён", $"{Discipline.Title}, тема «{topic.Title}»");
        return RedirectToPage(new { id });
    }

    static void Swap<T>(List<T> list, int i, int dir, Action<T, int> setOrder)
    {
        int j = i + (dir < 0 ? -1 : 1);
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        for (int k = 0; k < list.Count; k++) setOrder(list[k], k + 1);
    }

    public async Task<IActionResult> OnPostAddLectureAsync(int id, int topicId, string? lectureTitle)
    {
        if (!await LoadAsync(id)) return NotFound();
        var topic = Topics.FirstOrDefault(t => t.Id == topicId);
        if (topic == null) return NotFound();
        lectureTitle = lectureTitle?.Trim();
        if (string.IsNullOrEmpty(lectureTitle) || lectureTitle.Length > 150) { Error("Введите название лекции (до 150 символов)."); return RedirectToPage(new { id }); }
        int number = Topics.SelectMany(t => t.Lectures).Select(l => l.Number).DefaultIfEmpty(0).Max() + 1;
        var l = new Lecture
        {
            TopicId = topicId, Title = lectureTitle, Number = number,
            Order = topic.Lectures.Count == 0 ? 1 : topic.Lectures.Max(x => x.Order) + 1,
            Test = new Test(),
        };
        db.Lectures.Add(l);
        await db.SaveChangesAsync();
        Success($"Лекция «{l.Title}» добавлена. Заполните теорию, загрузите материал и добавьте вопросы.");
        return Redirect($"/Teacher/Lecture/{l.Id}");
    }
}
