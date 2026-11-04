using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;

namespace StudyCabinet.Services;

public enum LectureState { Locked, Available, Pending, Completed }

/// <summary>Состояние одной лекции для конкретного студента.</summary>
public class LectureInfo
{
    public Lecture Lecture { get; init; } = null!;
    public Topic Topic => Lecture.Topic;
    public LectureState State { get; set; }
    public LectureProgress? Progress { get; init; }
    public bool OpenedByTeacher => Progress?.Status == ProgressStatus.OpenedByTeacher;

    public int ControlTotal { get; set; }
    public int ControlAnswered { get; set; }
    public int ControlCorrect { get; set; }
    public int ControlWrong { get; set; }
    public int ControlPending { get; set; }
    public int ControlRejected { get; set; }

    public bool HasTest { get; set; }
    public bool TestPassed { get; set; }
    public int? BestPercent { get; set; }
    public int AttemptsUsed { get; set; }
    /// <summary>Сколько попыток ещё можно сделать (null — без ограничения).</summary>
    public int? AttemptsLeft { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Невыполненные условия открытия следующей лекции.</summary>
    public List<string> Missing { get; } = new();
    public bool Open => State != LectureState.Locked;
}

public class DisciplinePlan
{
    public Discipline Discipline { get; init; } = null!;
    public List<LectureInfo> Lectures { get; init; } = new();
    public int Completed => Lectures.Count(l => l.State == LectureState.Completed);
    /// <summary>Общий процент освоения дисциплины — доля пройденных лекций.</summary>
    public int Percent => Lectures.Count == 0 ? 0 : Completed * 100 / Lectures.Count;
    public LectureInfo? Current => Lectures.FirstOrDefault(l => l.State is LectureState.Available or LectureState.Pending);
}

/// <summary>
/// Последовательное открытие лекций. Все проверки выполняются на сервере: страница лекции, файл материала,
/// ответы на контрольные вопросы и тест сначала спрашивают у этого сервиса, открыта ли лекция студенту.
/// Правило: первая лекция дисциплины открыта сразу, каждая следующая — когда пройдена предыдущая
/// (или преподаватель открыл её вручную).
/// </summary>
public class StudyService
{
    readonly AppDb db;
    public StudyService(AppDb db) => this.db = db;

    /// <summary>Лекции дисциплины по порядку изучения: темы по порядку, внутри темы — лекции по порядку.</summary>
    public async Task<List<Lecture>> OrderedLecturesAsync(int disciplineId) =>
        (await db.Lectures.Include(l => l.Topic).ThenInclude(t => t.Discipline)
            .Include(l => l.ControlQuestions)
            .Include(l => l.Test).ThenInclude(t => t!.Questions)
            .Where(l => l.Topic.DisciplineId == disciplineId).AsSplitQuery().ToListAsync())
        .OrderBy(l => l.Topic.Order).ThenBy(l => l.Topic.Id).ThenBy(l => l.Order).ThenBy(l => l.Id).ToList();

    public async Task<DisciplinePlan?> PlanAsync(int disciplineId, int studentId)
    {
        var discipline = await db.Disciplines.FindAsync(disciplineId);
        if (discipline == null) return null;
        var lectures = await OrderedLecturesAsync(disciplineId);
        var lectureIds = lectures.Select(l => l.Id).ToList();
        var questionIds = lectures.SelectMany(l => l.ControlQuestions).Select(q => q.Id).ToList();
        var testIds = lectures.Where(l => l.Test != null).Select(l => l.Test!.Id).ToList();

        var answers = await db.StudentAnswers.Where(a => a.StudentId == studentId && questionIds.Contains(a.ControlQuestionId)).ToListAsync();
        var attempts = await db.TestAttempts.Where(a => a.StudentId == studentId && testIds.Contains(a.TestId) && a.Status != AttemptStatus.InProgress).ToListAsync();
        var progress = await db.LectureProgress.Where(p => p.StudentId == studentId && lectureIds.Contains(p.LectureId)).ToListAsync();

        var plan = new DisciplinePlan { Discipline = discipline };
        bool prevCompleted = true; // первая лекция открыта всегда
        foreach (var l in lectures)
        {
            var info = Evaluate(l, answers, attempts, progress.FirstOrDefault(p => p.LectureId == l.Id));
            bool completed = info.Progress?.Status == ProgressStatus.Completed;
            bool open = completed || prevCompleted || info.OpenedByTeacher;
            info.State = completed ? LectureState.Completed
                : !open ? LectureState.Locked
                : info.Missing.Count > 0 && info.Missing.All(m => m.StartsWith(PendingPrefix)) ? LectureState.Pending
                : LectureState.Available;
            plan.Lectures.Add(info);
            prevCompleted = completed;
        }
        return plan;
    }

    const string PendingPrefix = "Дождитесь";

    /// <summary>Проверяет условия открытия следующей лекции и считает результаты по лекции.</summary>
    static LectureInfo Evaluate(Lecture l, List<StudentAnswer> allAnswers, List<TestAttempt> allAttempts, LectureProgress? progress)
    {
        var info = new LectureInfo { Lecture = l, Progress = progress };
        var answers = allAnswers.Where(a => l.ControlQuestions.Any(q => q.Id == a.ControlQuestionId)).ToList();
        info.ControlTotal = l.ControlQuestions.Count;
        info.ControlAnswered = answers.Count;
        info.ControlCorrect = answers.Count(a => a.Status is AnswerStatus.Correct or AnswerStatus.Accepted);
        info.ControlWrong = answers.Count(a => a.Status == AnswerStatus.Wrong);
        info.ControlPending = answers.Count(a => a.Status == AnswerStatus.Pending);
        info.ControlRejected = answers.Count(a => a.Status == AnswerStatus.Rejected);

        if (l.RequireControl && info.ControlTotal > 0)
        {
            int left = info.ControlTotal - info.ControlAnswered;
            if (left > 0) info.Missing.Add($"Ответьте на все контрольные вопросы (осталось {left})");
            if (l.RequireControlCorrect && info.ControlWrong > 0) info.Missing.Add($"Исправьте неверные ответы на контрольные вопросы ({info.ControlWrong})");
            if (l.RequireReview && info.ControlRejected > 0) info.Missing.Add($"Переделайте ответ, который преподаватель вернул на доработку ({info.ControlRejected})");
            if (l.RequireReview && info.ControlPending > 0) info.Missing.Add($"{PendingPrefix} проверки развёрнутого ответа преподавателем");
        }

        var test = l.Test;
        if (test != null && test.Questions.Count > 0)
        {
            info.HasTest = true;
            var attempts = allAttempts.Where(a => a.TestId == test.Id).ToList();
            info.AttemptsUsed = attempts.Count;
            info.TestPassed = attempts.Any(a => a.Status == AttemptStatus.Passed);
            info.BestPercent = attempts.Count == 0 ? null : attempts.Max(a => a.Percent);
            info.LastAttemptAt = attempts.Count == 0 ? null : attempts.Max(a => a.FinishedAt);
            int? limit = AttemptLimit(test, progress);
            info.AttemptsLeft = limit == null ? null : Math.Max(0, limit.Value - info.AttemptsUsed);
            if (!info.TestPassed) info.Missing.Add($"Пройдите тест с результатом не ниже {test.PassPercent}%");
        }

        if (info.Missing.Count == 0 && !info.HasTest && !(l.RequireControl && info.ControlTotal > 0) && progress?.FirstOpenedAt == null)
            info.Missing.Add("Откройте и изучите лекцию");
        return info;
    }

    /// <summary>Сколько всего попыток разрешено (null — без ограничения). Без права пересдачи — одна попытка плюс выданные преподавателем.</summary>
    public static int? AttemptLimit(Test test, LectureProgress? progress)
    {
        int extra = progress?.ExtraAttempts ?? 0;
        if (!test.AllowRetry) return 1 + extra;
        return test.MaxAttempts == 0 ? null : test.MaxAttempts + extra;
    }

    /// <summary>Состояние лекции для студента (null — лекции нет).</summary>
    public async Task<LectureInfo?> InfoAsync(int lectureId, int studentId)
    {
        var disciplineId = await db.Lectures.Where(l => l.Id == lectureId).Select(l => (int?)l.Topic.DisciplineId).FirstOrDefaultAsync();
        if (disciplineId == null) return null;
        var plan = await PlanAsync(disciplineId.Value, studentId);
        return plan?.Lectures.FirstOrDefault(l => l.Lecture.Id == lectureId);
    }

    /// <summary>Может ли пользователь открыть лекцию. Преподаватель — всегда, студент — только открытую.</summary>
    public async Task<bool> CanOpenAsync(int lectureId, int userId, bool isTeacher)
    {
        if (isTeacher) return await db.Lectures.AnyAsync(l => l.Id == lectureId);
        var info = await InfoAsync(lectureId, userId);
        return info != null && info.Open;
    }

    async Task<LectureProgress> ProgressRowAsync(int studentId, int lectureId)
    {
        var p = await db.LectureProgress.FirstOrDefaultAsync(x => x.StudentId == studentId && x.LectureId == lectureId);
        if (p == null) { p = new LectureProgress { StudentId = studentId, LectureId = lectureId }; db.LectureProgress.Add(p); }
        return p;
    }

    /// <summary>Отмечает, что студент открыл лекцию.</summary>
    public async Task MarkOpenedAsync(int studentId, int lectureId)
    {
        var p = await ProgressRowAsync(studentId, lectureId);
        if (p.FirstOpenedAt == null) { p.FirstOpenedAt = DateTime.Now; await db.SaveChangesAsync(); }
        await UpdateCompletionAsync(studentId, lectureId);
    }

    /// <summary>
    /// Если все условия выполнены, отмечает лекцию пройденной — после этого открывается следующая.
    /// Возвращает true, если лекция пройдена именно сейчас.
    /// </summary>
    public async Task<bool> UpdateCompletionAsync(int studentId, int lectureId)
    {
        var info = await InfoAsync(lectureId, studentId);
        if (info == null || !info.Open || info.State == LectureState.Completed || info.Missing.Count > 0) return false;
        var p = await ProgressRowAsync(studentId, lectureId);
        p.Status = ProgressStatus.Completed;
        p.CompletedAt = DateTime.Now;
        p.FirstOpenedAt ??= DateTime.Now;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Следующая лекция дисциплины после указанной (для перехода после прохождения).</summary>
    public async Task<Lecture?> NextLectureAsync(Lecture lecture)
    {
        var list = await OrderedLecturesAsync(lecture.Topic.DisciplineId);
        int i = list.FindIndex(l => l.Id == lecture.Id);
        return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
    }

    // ---------- Действия преподавателя (записываются в журнал событий) ----------

    public async Task OpenForStudentAsync(int studentId, int lectureId)
    {
        var p = await ProgressRowAsync(studentId, lectureId);
        if (p.Status != ProgressStatus.Completed) p.Status = ProgressStatus.OpenedByTeacher;
        await db.SaveChangesAsync();
    }

    public async Task GrantAttemptAsync(int studentId, int lectureId)
    {
        var p = await ProgressRowAsync(studentId, lectureId);
        p.ExtraAttempts++;
        await db.SaveChangesAsync();
    }

    /// <summary>Сбрасывает результаты лекции: ответы на контрольные вопросы, попытки теста и отметку о прохождении.</summary>
    public async Task ResetLectureAsync(int studentId, int lectureId)
    {
        await db.StudentAnswers.Where(a => a.StudentId == studentId && a.ControlQuestion.LectureId == lectureId).ExecuteDeleteAsync();
        await db.TestAttempts.Where(a => a.StudentId == studentId && a.Test.LectureId == lectureId).ExecuteDeleteAsync();
        await db.LectureProgress.Where(p => p.StudentId == studentId && p.LectureId == lectureId).ExecuteDeleteAsync();
    }
}
