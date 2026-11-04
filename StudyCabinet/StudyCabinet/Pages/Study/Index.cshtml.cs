using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>Личный кабинет студента: дисциплины с процентом освоения и текущей лекцией, последние результаты.</summary>
public class IndexModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public IndexModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    public List<DisciplinePlan> Plans { get; set; } = new();
    public List<TestAttempt> Recent { get; set; } = new();
    public string? Group { get; set; }
    public int Pending { get; set; }

    public async Task OnGetAsync()
    {
        int me = User.UserId();
        foreach (var id in await db.Disciplines.OrderBy(d => d.Title).Select(d => d.Id).ToListAsync())
            if (await study.PlanAsync(id, me) is { } plan) Plans.Add(plan);
        Recent = await db.TestAttempts.Include(a => a.Test).ThenInclude(t => t.Lecture)
            .Where(a => a.StudentId == me && a.Status != AttemptStatus.InProgress)
            .OrderByDescending(a => a.FinishedAt).Take(5).ToListAsync();
        Group = await db.GroupMemberships.Where(m => m.UserId == me).Select(m => m.Group.Name).FirstOrDefaultAsync();
        Pending = await db.StudentAnswers.CountAsync(a => a.StudentId == me && a.Status == AnswerStatus.Pending);
    }
}
