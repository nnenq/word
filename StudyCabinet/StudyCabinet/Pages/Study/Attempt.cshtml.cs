using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>
/// Прохождение теста. Вопросы и порядок вариантов выбраны при начале попытки и хранятся в ней,
/// поэтому обновление страницы их не меняет. Время проверяется на сервере, а не только таймером в браузере.
/// </summary>
public class AttemptModel : AppPage
{
    /// <summary>Запас на отправку формы после окончания времени.</summary>
    static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);

    readonly AppDb db;
    readonly StudyService study;
    public AttemptModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    public TestAttempt Attempt { get; set; } = null!;
    public List<(TestQuestion Q, int[] Order)> Questions { get; set; } = new();
    public int SecondsLeft { get; set; }

    async Task<IActionResult?> LoadAsync(int id)
    {
        var a = await db.TestAttempts.Include(x => x.Test).ThenInclude(t => t.Lecture).ThenInclude(l => l.Topic)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (a.StudentId != User.UserId()) return Forbid();
        if (!await study.CanOpenAsync(a.Test.LectureId, User.UserId(), User.IsTeacher())) return Forbid();
        Attempt = a;
        var picked = JsonSerializer.Deserialize<List<AttemptQuestion>>(a.QuestionsJson) ?? new();
        var ids = picked.Select(p => p.Q).ToList();
        var qs = await db.TestQuestions.Include(q => q.Options).Where(q => ids.Contains(q.Id)).ToListAsync();
        // Вопрос мог быть удалён преподавателем во время попытки — такие пропускаем
        Questions = picked.Where(p => qs.Any(q => q.Id == p.Q)).Select(p => (qs.First(q => q.Id == p.Q), p.O)).ToList();
        SecondsLeft = a.Deadline == null ? 0 : Math.Max(0, (int)(a.Deadline.Value - DateTime.Now).TotalSeconds);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (await LoadAsync(id) is { } r) return r;
        if (Attempt.Status != AttemptStatus.InProgress) return Redirect($"/Study/Result/{id}");
        // Время вышло, а ответы не отправлены — попытка завершается без ответов
        if (Attempt.Deadline != null && DateTime.Now > Attempt.Deadline.Value + Grace) return await FinishAsync(new Dictionary<string, string[]>(), expired: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (await LoadAsync(id) is { } r) return r;
        if (Attempt.Status != AttemptStatus.InProgress) return Redirect($"/Study/Result/{id}");
        var answers = Questions.ToDictionary(x => x.Q.Id.ToString(), x => Grading.FromForm(Request.Form, x.Q));
        bool late = Attempt.Deadline != null && DateTime.Now > Attempt.Deadline.Value + Grace;
        return await FinishAsync(answers, late);
    }

    async Task<IActionResult> FinishAsync(Dictionary<string, string[]> answers, bool expired)
    {
        var test = Attempt.Test;
        int correct = expired ? 0 : Questions.Count(x => answers.TryGetValue(x.Q.Id.ToString(), out var a) && Grading.Check(x.Q, a) == true);
        Attempt.AnswersJson = JsonSerializer.Serialize(answers);
        Attempt.Correct = correct;
        Attempt.Total = Questions.Count;
        Attempt.Percent = Questions.Count == 0 ? 0 : correct * 100 / Questions.Count;
        Attempt.FinishedAt = DateTime.Now;
        Attempt.Status = expired ? AttemptStatus.Expired : Attempt.Percent >= test.PassPercent ? AttemptStatus.Passed : AttemptStatus.Failed;
        await db.SaveChangesAsync();

        bool completed = !User.IsTeacher() && await study.UpdateCompletionAsync(Attempt.StudentId, test.LectureId);
        if (expired) Error("Время на тест вышло — попытка не засчитана.");
        else if (Attempt.Status == AttemptStatus.Passed)
            Success($"Тест сдан: {correct} из {Questions.Count} ({Attempt.Percent}%)." + (completed ? " Лекция пройдена — следующая открыта!" : ""));
        else Error($"Тест не сдан: {correct} из {Questions.Count} ({Attempt.Percent}%), нужно не меньше {test.PassPercent}%. Повторите материал и попробуйте ещё раз.");
        return Redirect($"/Study/Result/{Attempt.Id}");
    }
}
