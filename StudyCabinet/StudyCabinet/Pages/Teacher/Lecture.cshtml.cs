using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

public class LectureInput
{
    [Range(1, 999, ErrorMessage = "Номер — от 1 до 999.")] public int Number { get; set; }
    [Required(ErrorMessage = "Введите название."), StringLength(150, ErrorMessage = "Не длиннее 150 символов.")] public string Title { get; set; } = "";
    [StringLength(500, ErrorMessage = "Не длиннее 500 символов.")] public string? Description { get; set; }
    public string? Content { get; set; }
    public int TopicId { get; set; }
    public bool RequireControl { get; set; }
    public bool RequireControlCorrect { get; set; }
    public bool RequireReview { get; set; }
}

public class TestInput
{
    [Range(0, 200, ErrorMessage = "От 0 до 200.")] public int QuestionCount { get; set; }
    [Range(0, 50, ErrorMessage = "От 0 до 50.")] public int MaxAttempts { get; set; }
    [Range(1, 100, ErrorMessage = "От 1 до 100 %.")] public int PassPercent { get; set; } = 70;
    [Range(0, 300, ErrorMessage = "От 0 до 300 минут.")] public int TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public bool AllowRetry { get; set; }
}

/// <summary>Редактирование лекции: теория, файл, условия открытия следующей лекции, настройки теста, вопросы.</summary>
public class LectureModel : AppPage
{
    readonly AppDb db;
    readonly Audit audit;
    readonly MaterialStore materials;
    public LectureModel(AppDb db, Audit audit, MaterialStore materials) { this.db = db; this.audit = audit; this.materials = materials; }

    public Lecture Lecture { get; set; } = null!;
    public List<SelectListItem> TopicItems { get; set; } = new();
    public long? FileSize { get; set; }
    public int Answers { get; set; }
    public int Attempts { get; set; }
    [BindProperty] public LectureInput Input { get; set; } = new();
    [BindProperty] public TestInput TestSettings { get; set; } = new();
    [BindProperty] public IFormFile? Upload { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "main";

    async Task<bool> LoadAsync(int id)
    {
        var l = await db.Lectures.Include(x => x.Topic).ThenInclude(t => t.Discipline)
            .Include(x => x.ControlQuestions).ThenInclude(q => q.Options)
            .Include(x => x.Test).ThenInclude(t => t!.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return false;
        Lecture = l;
        TopicItems = await db.Topics.Where(t => t.DisciplineId == l.Topic.DisciplineId).OrderBy(t => t.Order)
            .Select(t => new SelectListItem(t.Title, t.Id.ToString())).ToListAsync();
        FileSize = materials.Exists(l) ? new FileInfo(materials.PathOf(l)).Length : null;
        Answers = await db.StudentAnswers.CountAsync(a => a.ControlQuestion.LectureId == id);
        Attempts = await db.TestAttempts.CountAsync(a => a.Test.LectureId == id);
        return true;
    }

    void Fill()
    {
        var l = Lecture;
        Input = new LectureInput
        {
            Number = l.Number, Title = l.Title, Description = l.Description, Content = l.Content, TopicId = l.TopicId,
            RequireControl = l.RequireControl, RequireControlCorrect = l.RequireControlCorrect, RequireReview = l.RequireReview,
        };
        var t = l.Test ?? new Test();
        TestSettings = new TestInput
        {
            QuestionCount = t.QuestionCount, MaxAttempts = t.MaxAttempts, PassPercent = t.PassPercent, TimeLimitMinutes = t.TimeLimitMinutes,
            ShuffleQuestions = t.ShuffleQuestions, ShuffleOptions = t.ShuffleOptions, AllowRetry = t.AllowRetry,
        };
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Fill();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        foreach (var k in ModelState.Keys.Where(k => k.StartsWith("TestSettings")).ToList()) ModelState.Remove(k);
        if (!TopicItems.Any(t => t.Value == Input.TopicId.ToString())) ModelState.AddModelError("Input.TopicId", "Выберите тему этой дисциплины.");
        if (!ModelState.IsValid) { Tab = "main"; var keep = Input; Fill(); Input = keep; return Page(); }
        var l = Lecture;
        bool movedTopic = l.TopicId != Input.TopicId;
        l.Number = Input.Number; l.Title = Input.Title.Trim(); l.Description = Input.Description?.Trim() ?? "";
        l.Content = Input.Content ?? "";
        l.RequireControl = Input.RequireControl; l.RequireControlCorrect = Input.RequireControlCorrect; l.RequireReview = Input.RequireReview;
        if (movedTopic)
        {
            l.TopicId = Input.TopicId;
            l.Order = (await db.Lectures.Where(x => x.TopicId == Input.TopicId).MaxAsync(x => (int?)x.Order) ?? 0) + 1;
        }
        await db.SaveChangesAsync();
        await audit.WriteAsync("Лекция изменена", $"Лекция {l.Number} «{l.Title}»: условия — контрольные вопросы {(l.RequireControl ? "обязательны" : "необязательны")}" +
            $"{(l.RequireControlCorrect ? ", только верные ответы" : "")}{(l.RequireReview ? ", ждать проверки развёрнутых ответов" : "")}");
        Success("Лекция сохранена.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTestAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        foreach (var k in ModelState.Keys.Where(k => k.StartsWith("Input")).ToList()) ModelState.Remove(k);
        if (!ModelState.IsValid) { Tab = "test"; var keep = TestSettings; Fill(); TestSettings = keep; return Page(); }
        var t = Lecture.Test;
        if (t == null) { t = new Test { LectureId = id }; db.Tests.Add(t); }
        t.QuestionCount = TestSettings.QuestionCount; t.MaxAttempts = TestSettings.MaxAttempts; t.PassPercent = TestSettings.PassPercent;
        t.TimeLimitMinutes = TestSettings.TimeLimitMinutes; t.ShuffleQuestions = TestSettings.ShuffleQuestions;
        t.ShuffleOptions = TestSettings.ShuffleOptions; t.AllowRetry = TestSettings.AllowRetry;
        await db.SaveChangesAsync();
        await audit.WriteAsync("Настройки теста изменены", $"Лекция {Lecture.Number} «{Lecture.Title}»: порог {t.PassPercent}%, попыток {(t.MaxAttempts == 0 ? "без ограничения" : t.MaxAttempts)}, время {(t.TimeLimitMinutes == 0 ? "без ограничения" : t.TimeLimitMinutes + " мин")}");
        Success("Настройки теста сохранены.");
        return RedirectToPage(new { id, tab = "test" });
    }

    /// <summary>Загрузка или замена файла лекции.</summary>
    public async Task<IActionResult> OnPostUploadAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Upload == null) { Error("Выберите файл."); return RedirectToPage(new { id, tab = "material" }); }
        if (materials.Validate(Upload) is string err) { Error(err); return RedirectToPage(new { id, tab = "material" }); }
        bool replace = Lecture.MaterialFile != null;
        var old = Lecture.MaterialName;
        await materials.SaveAsync(Lecture, Upload);
        await db.SaveChangesAsync();
        await audit.WriteAsync(replace ? "Материал заменён" : "Материал загружен",
            $"Лекция {Lecture.Number} «{Lecture.Title}»: {(replace ? $"{old} → " : "")}{Lecture.MaterialName}, версия {Lecture.MaterialVersion}");
        Success(replace ? $"Файл заменён. Студенты сразу видят новую версию ({Lecture.MaterialVersion})." : "Файл загружен.");
        return RedirectToPage(new { id, tab = "material" });
    }

    public async Task<IActionResult> OnPostDeleteFileAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var name = Lecture.MaterialName;
        materials.Delete(Lecture);
        Lecture.MaterialFile = null; Lecture.MaterialName = null; Lecture.MaterialUpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        await audit.WriteAsync("Материал удалён", $"Лекция {Lecture.Number} «{Lecture.Title}»: {name}");
        Success("Файл удалён.");
        return RedirectToPage(new { id, tab = "material" });
    }

    public async Task<IActionResult> OnPostDeleteQuestionAsync(int id, int questionId, string kind)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (kind == "test")
        {
            var q = Lecture.Test?.Questions.FirstOrDefault(x => x.Id == questionId);
            if (q == null) return NotFound();
            db.AnswerOptions.RemoveRange(q.Options);
            db.TestQuestions.Remove(q);
        }
        else
        {
            var q = Lecture.ControlQuestions.FirstOrDefault(x => x.Id == questionId);
            if (q == null) return NotFound();
            db.ControlQuestions.Remove(q);
        }
        await db.SaveChangesAsync();
        Success("Вопрос удалён.");
        return RedirectToPage(new { id, tab = kind == "test" ? "test" : "control" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        int disciplineId = Lecture.Topic.DisciplineId;
        materials.Delete(Lecture);
        // Вопросы теста с вариантами уже загружены — удаляем варианты явно (в базе для них нет каскада, см. AppDb)
        if (Lecture.Test != null) db.AnswerOptions.RemoveRange(Lecture.Test.Questions.SelectMany(q => q.Options));
        db.Lectures.Remove(Lecture);
        await db.SaveChangesAsync();
        await audit.WriteAsync("Лекция удалена", $"Лекция {Lecture.Number} «{Lecture.Title}» — вместе с вопросами и результатами");
        Success($"Лекция «{Lecture.Title}» удалена.");
        return Redirect($"/Teacher/Discipline/{disciplineId}");
    }
}
