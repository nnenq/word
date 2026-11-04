using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>Результат попытки. Студент видит свои попытки, преподаватель — любые (вместе с правильными ответами).</summary>
public class ResultModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public ResultModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    public TestAttempt Attempt { get; set; } = null!;
    public List<(TestQuestion Q, string[] Answer, bool Ok)> Rows { get; set; } = new();
    public LectureInfo? Info { get; set; }
    public Lecture? Next { get; set; }
    public bool ForTeacher => User.IsTeacher();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var a = await db.TestAttempts.Include(x => x.Student).Include(x => x.Test).ThenInclude(t => t.Lecture).ThenInclude(l => l.Topic).ThenInclude(t => t.Discipline)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (a.StudentId != User.UserId() && !User.IsTeacher()) return Forbid();
        if (a.Status == AttemptStatus.InProgress && a.StudentId == User.UserId()) return Redirect($"/Study/Attempt/{id}");
        Attempt = a;
        var picked = JsonSerializer.Deserialize<List<AttemptQuestion>>(a.QuestionsJson) ?? new();
        var answers = JsonSerializer.Deserialize<Dictionary<string, string[]>>(a.AnswersJson) ?? new();
        var ids = picked.Select(p => p.Q).ToList();
        var qs = await db.TestQuestions.Include(q => q.Options).Where(q => ids.Contains(q.Id)).ToListAsync();
        foreach (var p in picked)
        {
            var q = qs.FirstOrDefault(x => x.Id == p.Q);
            if (q == null) continue;
            var ans = answers.GetValueOrDefault(q.Id.ToString()) ?? Array.Empty<string>();
            Rows.Add((q, ans, Grading.Check(q, ans) == true && a.Status != AttemptStatus.Expired));
        }
        if (a.StudentId == User.UserId() && !User.IsTeacher())
        {
            Info = await study.InfoAsync(a.Test.LectureId, a.StudentId);
            if (Info?.State == LectureState.Completed) Next = await study.NextLectureAsync(a.Test.Lecture);
        }
        return Page();
    }
}
