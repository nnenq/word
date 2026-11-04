using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>
/// Редактор вопроса — контрольного (kind=control) или вопроса теста (kind=test).
/// Варианты ответа хранятся в таблице AnswerOptions; при сохранении список вариантов заменяется целиком.
/// </summary>
public class QuestionModel : AppPage
{
    readonly AppDb db;
    public QuestionModel(AppDb db) => this.db = db;

    [BindProperty(SupportsGet = true)] public int Lecture { get; set; }
    [BindProperty(SupportsGet = true)] public string Kind { get; set; } = "control";
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }

    [BindProperty, Required(ErrorMessage = "Введите текст вопроса."), StringLength(1000, ErrorMessage = "Не длиннее 1000 символов.")]
    public string Text { get; set; } = "";
    [BindProperty] public QuestionType Type { get; set; } = QuestionType.Single;

    // Варианты выбора: текст, ключ строки и ключи отмеченных правильными
    [BindProperty] public List<string> ChoiceText { get; set; } = new();
    [BindProperty] public List<string> ChoiceKey { get; set; } = new();
    [BindProperty] public List<string> ChoiceCorrect { get; set; } = new();
    // Допустимые ответы для краткого ответа и пропущенного слова
    [BindProperty] public List<string> AcceptText { get; set; } = new();
    // Пары для соответствия
    [BindProperty] public List<string> MatchLeft { get; set; } = new();
    [BindProperty] public List<string> MatchRight { get; set; } = new();

    public Lecture LectureEntity { get; set; } = null!;
    public bool IsTest => Kind == "test";
    public IEnumerable<QuestionType> Types => Enum.GetValues<QuestionType>().Where(t => !IsTest || t != QuestionType.Essay);

    async Task<bool> LoadLectureAsync()
    {
        var l = await db.Lectures.Include(x => x.Topic).ThenInclude(t => t.Discipline).Include(x => x.Test).FirstOrDefaultAsync(x => x.Id == Lecture);
        if (l == null) return false;
        LectureEntity = l;
        Kind = Kind == "test" ? "test" : "control";
        return true;
    }

    async Task<IQuestion?> FindAsync() => IsTest
        ? await db.TestQuestions.Include(q => q.Options).FirstOrDefaultAsync(q => q.Id == Id && q.Test.LectureId == Lecture)
        : await db.ControlQuestions.Include(q => q.Options).FirstOrDefaultAsync(q => q.Id == Id && q.LectureId == Lecture);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadLectureAsync()) return NotFound();
        if (Id == null)
        {
            // Пустые строки для нового вопроса
            ChoiceText = new() { "", "", "" }; ChoiceKey = new() { "a", "b", "c" };
            AcceptText = new() { "" };
            MatchLeft = new() { "", "", "" }; MatchRight = new() { "", "", "" };
            return Page();
        }
        var q = await FindAsync();
        if (q == null) return NotFound();
        Text = q.Text; Type = q.Type;
        var opts = q.Options.OrderBy(o => o.Order).ToList();
        if (QuestionTypes.HasChoices(q.Type))
        {
            ChoiceText = opts.Select(o => o.Text).ToList();
            ChoiceKey = opts.Select(o => "k" + o.Id).ToList();
            ChoiceCorrect = opts.Where(o => o.IsCorrect).Select(o => "k" + o.Id).ToList();
        }
        else if (q.Type == QuestionType.Matching) { MatchLeft = opts.Select(o => o.Text).ToList(); MatchRight = opts.Select(o => o.MatchText ?? "").ToList(); }
        else AcceptText = opts.Select(o => o.Text).ToList();
        if (ChoiceText.Count == 0) { ChoiceText = new() { "", "" }; ChoiceKey = new() { "a", "b" }; }
        if (AcceptText.Count == 0) AcceptText = new() { "" };
        if (MatchLeft.Count == 0) { MatchLeft = new() { "", "" }; MatchRight = new() { "", "" }; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadLectureAsync()) return NotFound();
        if (IsTest && Type == QuestionType.Essay) ModelState.AddModelError(nameof(Type), "В тесте нужны вопросы с автоматической проверкой.");

        var options = new List<AnswerOption>();
        if (QuestionTypes.HasChoices(Type))
        {
            for (int i = 0; i < ChoiceText.Count; i++)
            {
                var t = ChoiceText[i]?.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                var key = i < ChoiceKey.Count ? ChoiceKey[i] : "";
                options.Add(new AnswerOption { Text = Cut(t), IsCorrect = ChoiceCorrect.Contains(key), Order = options.Count });
            }
            int correct = options.Count(o => o.IsCorrect);
            if (options.Count < 2) ModelState.AddModelError("", "Нужно хотя бы два варианта ответа.");
            else if (Type == QuestionType.Single && correct != 1) ModelState.AddModelError("", "Отметьте ровно один правильный вариант.");
            else if (Type == QuestionType.Multiple && correct < 1) ModelState.AddModelError("", "Отметьте хотя бы один правильный вариант.");
        }
        else if (Type == QuestionType.Matching)
        {
            for (int i = 0; i < Math.Max(MatchLeft.Count, MatchRight.Count); i++)
            {
                var left = i < MatchLeft.Count ? MatchLeft[i]?.Trim() : null;
                var right = i < MatchRight.Count ? MatchRight[i]?.Trim() : null;
                if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)) continue;
                if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) { ModelState.AddModelError("", "В каждой паре заполните обе части."); break; }
                options.Add(new AnswerOption { Text = Cut(left), MatchText = Cut(right), IsCorrect = true, Order = options.Count });
            }
            if (options.Count < 2) ModelState.AddModelError("", "Нужно хотя бы две пары для соответствия.");
            else if (options.Select(o => Grading.Normalize(o.MatchText)).Distinct().Count() != options.Count)
                ModelState.AddModelError("", "Правые части пар должны различаться, иначе ответ будет неоднозначным.");
        }
        else if (Type != QuestionType.Essay)
        {
            foreach (var a in AcceptText.Select(a => a?.Trim()).Where(a => !string.IsNullOrEmpty(a)).Distinct())
                options.Add(new AnswerOption { Text = Cut(a!), IsCorrect = true, Order = options.Count });
            if (options.Count == 0) ModelState.AddModelError("", "Укажите хотя бы один правильный ответ.");
        }

        if (!ModelState.IsValid)
        {
            if (ChoiceText.Count == 0) { ChoiceText = new() { "", "" }; ChoiceKey = new() { "a", "b" }; }
            if (AcceptText.Count == 0) AcceptText = new() { "" };
            if (MatchLeft.Count == 0) { MatchLeft = new() { "", "" }; MatchRight = new() { "", "" }; }
            return Page();
        }

        if (IsTest)
        {
            var test = LectureEntity.Test;
            if (test == null) { test = new Test { LectureId = Lecture }; db.Tests.Add(test); await db.SaveChangesAsync(); }
            TestQuestion q;
            if (Id == null)
            {
                q = new TestQuestion { TestId = test.Id, Order = (await db.TestQuestions.Where(x => x.TestId == test.Id).MaxAsync(x => (int?)x.Order) ?? 0) + 1 };
                db.TestQuestions.Add(q);
            }
            else
            {
                q = (TestQuestion?)await FindAsync() ?? throw new InvalidOperationException();
                db.AnswerOptions.RemoveRange(q.Options);
                q.Options.Clear();
            }
            q.Text = Text.Trim(); q.Type = Type; q.Options.AddRange(options);
        }
        else
        {
            ControlQuestion q;
            if (Id == null)
            {
                q = new ControlQuestion { LectureId = Lecture, Order = (await db.ControlQuestions.Where(x => x.LectureId == Lecture).MaxAsync(x => (int?)x.Order) ?? 0) + 1 };
                db.ControlQuestions.Add(q);
            }
            else
            {
                q = (ControlQuestion?)await FindAsync() ?? throw new InvalidOperationException();
                db.AnswerOptions.RemoveRange(q.Options);
                q.Options.Clear();
            }
            q.Text = Text.Trim(); q.Type = Type; q.Options.AddRange(options);
        }
        await db.SaveChangesAsync();
        Success(Id == null ? "Вопрос добавлен." : "Вопрос сохранён.");
        return Redirect($"/Teacher/Lecture/{Lecture}?tab={(IsTest ? "test" : "control")}");
    }

    static string Cut(string s) => s.Length > 500 ? s[..500] : s;
}
