using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>Мои результаты: прогресс по дисциплинам, все попытки тестов, ответы на контрольные вопросы.</summary>
public class ResultsModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public ResultsModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    public List<DisciplinePlan> Plans { get; set; } = new();
    public List<TestAttempt> Attempts { get; set; } = new();
    public List<StudentAnswer> Answers { get; set; } = new();

    public async Task OnGetAsync()
    {
        int me = User.UserId();
        foreach (var id in await db.Disciplines.OrderBy(d => d.Title).Select(d => d.Id).ToListAsync())
            if (await study.PlanAsync(id, me) is { } p) Plans.Add(p);
        Attempts = await db.TestAttempts.Include(a => a.Test).ThenInclude(t => t.Lecture).ThenInclude(l => l.Topic).ThenInclude(t => t.Discipline)
            .Where(a => a.StudentId == me && a.Status != AttemptStatus.InProgress).OrderByDescending(a => a.FinishedAt).ToListAsync();
        Answers = await db.StudentAnswers.Include(a => a.ControlQuestion).ThenInclude(q => q.Lecture)
            .Where(a => a.StudentId == me).OrderByDescending(a => a.AnsweredAt).ToListAsync();
    }
}
