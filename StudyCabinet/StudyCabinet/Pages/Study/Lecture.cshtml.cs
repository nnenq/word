using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>
/// Лекция: теория, файл материала, контрольные вопросы и тест. Сервер сначала проверяет, открыта ли лекция
/// студенту: закрытую лекцию нельзя открыть даже по прямой ссылке — сервер отвечает «доступ запрещён» (403).
/// </summary>
public class LectureModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    readonly MaterialStore materials;
    public LectureModel(AppDb db, StudyService study, MaterialStore materials) { this.db = db; this.study = study; this.materials = materials; }

    public Lecture Lecture { get; set; } = null!;
    public LectureInfo? Info { get; set; }
    public Lecture? Next { get; set; }
    public LectureInfo? Blocker { get; set; }
    public bool Locked { get; set; }
    public bool IsTeacher => User.IsTeacher();
    public Dictionary<int, StudentAnswer> Answers { get; set; } = new();
    public List<TestAttempt> Attempts { get; set; } = new();
    public TestAttempt? InProgress { get; set; }
    public string? DocxHtml { get; set; }
    public bool HasFile { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "theory";

    async Task<bool> LoadAsync(int id)
    {
        var l = await db.Lectures.Include(x => x.Topic).ThenInclude(t => t.Discipline)
            .Include(x => x.ControlQuestions).ThenInclude(q => q.Options)
            .Include(x => x.Test).ThenInclude(t => t!.Questions)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return false;
        Lecture = l;
        int me = User.UserId();
        if (IsTeacher) Locked = false;
        else
        {
            var plan = await study.PlanAsync(l.Topic.DisciplineId, me);
            Info = plan!.Lectures.First(x => x.Lecture.Id == id);
            Locked = !Info.Open;
            if (Locked)
            {
                int i = plan.Lectures.IndexOf(Info);
                Blocker = plan.Lectures.Take(i).LastOrDefault(x => x.State != LectureState.Completed);
            }
        }
        if (Locked) return true;

        var qIds = l.ControlQuestions.Select(q => q.Id).ToList();
        Answers = await db.StudentAnswers.Where(a => a.StudentId == me && qIds.Contains(a.ControlQuestionId)).ToDictionaryAsync(a => a.ControlQuestionId);
        if (l.Test != null)
        {
            Attempts = await db.TestAttempts.Where(a => a.StudentId == me && a.TestId == l.Test.Id).OrderBy(a => a.StartedAt).ToListAsync();
            InProgress = Attempts.FirstOrDefault(a => a.Status == AttemptStatus.InProgress);
            Attempts = Attempts.Where(a => a.Status != AttemptStatus.InProgress).ToList();
        }
        HasFile = materials.Exists(l);
        if (HasFile && MaterialStore.IsDocx(l.MaterialName))
        {
            try { DocxHtml = materials.DocxToHtml(l); } catch { DocxHtml = null; }
        }
        Next = await study.NextLectureAsync(l);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Locked) { Response.StatusCode = StatusCodes.Status403Forbidden; return Page(); }
        if (!IsTeacher)
        {
            bool wasCompleted = Info!.State == LectureState.Completed;
            await study.MarkOpenedAsync(User.UserId(), id);
            if (!wasCompleted && await IsCompletedNowAsync(id)) { await LoadAsync(id); }
        }
        return Page();
    }

    async Task<bool> IsCompletedNowAsync(int id) => (await study.InfoAsync(id, User.UserId()))?.State == LectureState.Completed;

    /// <summary>Ответы на контрольные вопросы: автопроверка, развёрнутые ответы — на проверку преподавателю.</summary>
    public async Task<IActionResult> OnPostControlAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Locked || IsTeacher) return Forbid();
        int me = User.UserId();
        int right = 0, wrong = 0, sent = 0, skipped = 0;
        foreach (var q in Lecture.ControlQuestions)
        {
            Answers.TryGetValue(q.Id, out var old);
            // Верные и принятые ответы уже засчитаны, ответ на проверке менять нельзя
            if (old != null && old.Status is AnswerStatus.Correct or AnswerStatus.Accepted or AnswerStatus.Pending) continue;
            var answer = Grading.FromForm(Request.Form, q);
            if (!Grading.IsAnswered(q, answer)) { skipped++; continue; }
            var ok = Grading.Check(q, answer);
            var status = ok == null ? AnswerStatus.Pending : ok.Value ? AnswerStatus.Correct : AnswerStatus.Wrong;
            if (old == null) { old = new StudentAnswer { StudentId = me, ControlQuestionId = q.Id }; db.StudentAnswers.Add(old); }
            old.Answer = Grading.ToJson(answer);
            old.Status = status;
            old.AnsweredAt = DateTime.Now;
            old.CheckedAt = null; old.CheckedById = null; old.TeacherComment = null;
            if (status == AnswerStatus.Correct) right++; else if (status == AnswerStatus.Wrong) wrong++; else sent++;
        }
        await db.SaveChangesAsync();
        bool completed = await study.UpdateCompletionAsync(me, id);

        var parts = new List<string>();
        if (right > 0) parts.Add($"верно: {right}");
        if (wrong > 0) parts.Add($"неверно: {wrong} — исправьте и отправьте ещё раз");
        if (sent > 0) parts.Add($"на проверку преподавателю: {sent}");
        if (skipped > 0) parts.Add($"без ответа: {skipped}");
        if (parts.Count == 0) Error("Ответы не изменились: заполните вопросы без ответа или исправьте неверные.");
        else if (wrong > 0 || skipped > 0) Error("Ответы сохранены — " + string.Join(", ", parts) + ".");
        else Success("Ответы сохранены — " + string.Join(", ", parts) + "." + (completed ? " Лекция пройдена, следующая открыта!" : ""));
        return RedirectToPage(new { id, tab = "control" });
    }

    /// <summary>Начало попытки теста: проверка доступа и числа попыток, выбор и перемешивание вопросов.</summary>
    public async Task<IActionResult> OnPostStartAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Locked) return Forbid();
        var test = Lecture.Test;
        if (test == null || test.Questions.Count == 0) return NotFound();
        if (InProgress != null) return Redirect($"/Study/Attempt/{InProgress.Id}");
        if (!IsTeacher && Info!.AttemptsLeft == 0)
        {
            Error("Попытки закончились. Обратитесь к преподавателю, чтобы получить дополнительную попытку.");
            return RedirectToPage(new { id, tab = "test" });
        }

        await db.Entry(test).Collection(t => t.Questions).Query().Include(q => q.Options).LoadAsync();
        var rnd = Random.Shared;
        var questions = test.Questions.OrderBy(q => q.Order).ToList();
        if (test.ShuffleQuestions) questions = questions.OrderBy(_ => rnd.Next()).ToList();
        if (test.QuestionCount > 0 && test.QuestionCount < questions.Count)
        {
            // Если вопросов в банке больше, чем нужно, выбираем случайные и, без перемешивания, сохраняем исходный порядок
            questions = questions.OrderBy(_ => rnd.Next()).Take(test.QuestionCount).ToList();
            if (!test.ShuffleQuestions) questions = questions.OrderBy(q => q.Order).ToList();
        }
        var picked = questions.Select(q => new AttemptQuestion(q.Id,
            (test.ShuffleOptions ? q.Options.OrderBy(_ => rnd.Next()) : q.Options.OrderBy(o => o.Order)).Select(o => o.Id).ToArray())).ToList();

        var attempt = new TestAttempt
        {
            StudentId = User.UserId(), TestId = test.Id, StartedAt = DateTime.Now,
            Deadline = test.TimeLimitMinutes > 0 ? DateTime.Now.AddMinutes(test.TimeLimitMinutes) : null,
            QuestionsJson = JsonSerializer.Serialize(picked), Total = picked.Count,
        };
        db.TestAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return Redirect($"/Study/Attempt/{attempt.Id}");
    }

    public string Theory => Markup.ToHtml(Lecture.Content);
}
