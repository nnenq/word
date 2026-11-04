using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Проверка развёрнутых ответов: принять или вернуть на доработку с комментарием.</summary>
public class ReviewsModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    readonly Audit audit;
    public ReviewsModel(AppDb db, StudyService study, Audit audit) { this.db = db; this.study = study; this.audit = audit; }

    public List<StudentAnswer> Pending { get; set; } = new();
    public List<StudentAnswer> Checked { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = db.StudentAnswers.Include(a => a.Student).Include(a => a.ControlQuestion).ThenInclude(q => q.Lecture).ThenInclude(l => l.Topic).ThenInclude(t => t.Discipline)
            .Where(a => a.ControlQuestion.Type == QuestionType.Essay);
        Pending = await q.Where(a => a.Status == AnswerStatus.Pending).OrderBy(a => a.AnsweredAt).ToListAsync();
        Checked = await q.Where(a => a.Status != AnswerStatus.Pending).OrderByDescending(a => a.CheckedAt).Take(15).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(int id, bool accept, string? comment)
    {
        var a = await db.StudentAnswers.Include(x => x.Student).Include(x => x.ControlQuestion).ThenInclude(q => q.Lecture).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (!accept && string.IsNullOrWhiteSpace(comment)) { Error("Чтобы вернуть ответ на доработку, напишите комментарий: что исправить."); return RedirectToPage(); }
        a.Status = accept ? AnswerStatus.Accepted : AnswerStatus.Rejected;
        a.TeacherComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()[..Math.Min(comment.Trim().Length, 1000)];
        a.CheckedAt = DateTime.Now;
        a.CheckedById = User.UserId();
        await db.SaveChangesAsync();
        bool completed = accept && await study.UpdateCompletionAsync(a.StudentId, a.ControlQuestion.LectureId);
        await audit.WriteAsync(accept ? "Ответ принят" : "Ответ возвращён на доработку",
            $"{a.Student.FullName}, лекция {a.ControlQuestion.Lecture.Number} «{a.ControlQuestion.Lecture.Title}»" + (a.TeacherComment != null ? $": {a.TeacherComment}" : ""));
        Success(accept ? $"Ответ принят.{(completed ? $" {a.Student.FullName} прошёл(ла) лекцию — открыта следующая." : "")}" : "Ответ возвращён на доработку.");
        return RedirectToPage();
    }
}
