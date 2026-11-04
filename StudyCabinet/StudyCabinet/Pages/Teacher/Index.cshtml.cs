using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Панель преподавателя: сводка, ответы на проверку, студенты с проблемами, последние попытки.</summary>
public class IndexModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public IndexModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    public int Students { get; set; }
    public int Disciplines { get; set; }
    public int Lectures { get; set; }
    public int PendingReviews { get; set; }
    public int AttemptsWeek { get; set; }
    public List<TestAttempt> Recent { get; set; } = new();
    public List<(User Student, LectureInfo Info)> Stuck { get; set; } = new();
    public List<AuditEntry> Journal { get; set; } = new();

    public async Task OnGetAsync()
    {
        Students = await db.Users.CountAsync(u => u.Role == Roles.Student);
        Disciplines = await db.Disciplines.CountAsync();
        Lectures = await db.Lectures.CountAsync();
        PendingReviews = await db.StudentAnswers.CountAsync(a => a.Status == AnswerStatus.Pending);
        var week = DateTime.Now.AddDays(-7);
        AttemptsWeek = await db.TestAttempts.CountAsync(a => a.FinishedAt >= week && a.Student.Role == Roles.Student);
        Recent = await db.TestAttempts.Include(a => a.Student).Include(a => a.Test).ThenInclude(t => t.Lecture)
            .Where(a => a.Status != AttemptStatus.InProgress && a.Student.Role == Roles.Student)
            .OrderByDescending(a => a.FinishedAt).Take(8).ToListAsync();
        Journal = await db.AuditLog.OrderByDescending(a => a.At).Take(6).ToListAsync();

        // Студенты, которым нужна помощь: тест не сдан и попытки закончились, или не сдан два раза и больше
        var students = await db.Users.Where(u => u.Role == Roles.Student && !u.IsBlocked).OrderBy(u => u.FullName).ToListAsync();
        var disciplineIds = await db.Disciplines.Select(d => d.Id).ToListAsync();
        foreach (var s in students)
            foreach (var d in disciplineIds)
                if (await study.PlanAsync(d, s.Id) is { } plan)
                    foreach (var l in plan.Lectures.Where(l => l.Open && l.State != LectureState.Completed && l.HasTest && !l.TestPassed && (l.AttemptsLeft == 0 || l.AttemptsUsed >= 2)))
                        Stuck.Add((s, l));
    }
}
