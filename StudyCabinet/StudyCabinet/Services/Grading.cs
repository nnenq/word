using System.Text.Json;
using System.Text.RegularExpressions;
using StudyCabinet.Models;

namespace StudyCabinet.Services;

/// <summary>
/// Автоматическая проверка ответов. Ответ на вопрос — массив строк:
/// краткий ответ и пропущенное слово — один текст; один вариант — номер варианта; несколько — номера вариантов;
/// соответствие — пары «номер левой части|выбранная правая часть». Развёрнутый ответ проверяет преподаватель.
/// </summary>
public static class Grading
{
    /// <summary>Приводит текстовый ответ к общему виду: регистр, «ё», лишние пробелы и знаки препинания на концах.</summary>
    public static string Normalize(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim(' ', '.', ',', ';', '!', '?', '«', '»', '"', '\'');
    }

    /// <summary>true — верно, false — неверно, null — нужна проверка преподавателем.</summary>
    public static bool? Check(IQuestion q, string[] answer)
    {
        var opts = q.Options;
        switch (q.Type)
        {
            case QuestionType.ShortText:
            case QuestionType.Gap:
                var given = Normalize(answer.FirstOrDefault());
                return given.Length > 0 && opts.Any(o => Normalize(o.Text) == given);
            case QuestionType.Single:
                var right = opts.FirstOrDefault(o => o.IsCorrect);
                return right != null && answer.Length == 1 && answer[0] == right.Id.ToString();
            case QuestionType.Multiple:
                var correct = opts.Where(o => o.IsCorrect).Select(o => o.Id.ToString()).ToHashSet();
                return correct.SetEquals(answer.Where(a => a.Length > 0));
            case QuestionType.Matching:
                var pairs = answer.Select(a => a.Split('|', 2)).Where(p => p.Length == 2)
                                  .ToDictionary(p => p[0], p => p[1]);
                return opts.Count > 0 && opts.All(o => pairs.TryGetValue(o.Id.ToString(), out var v) && Normalize(v) == Normalize(o.MatchText));
            default:
                return null;
        }
    }

    /// <summary>Есть ли в ответе хоть что-то (пустой ответ не засчитывается как «ответил»).</summary>
    public static bool IsAnswered(IQuestion q, string[] answer) => q.Type switch
    {
        QuestionType.Matching => answer.Count(a => a.Split('|', 2) is [_, var v] && v.Length > 0) == q.Options.Count,
        _ => answer.Any(a => !string.IsNullOrWhiteSpace(a)),
    };

    /// <summary>Читает ответ на вопрос из формы: поля q{номер} и q{номер}_{вариант} для соответствия.</summary>
    public static string[] FromForm(IFormCollection form, IQuestion q)
    {
        if (q.Type == QuestionType.Matching)
            return q.Options.Select(o => $"{o.Id}|{form[$"q{q.Id}_{o.Id}"].ToString()}").ToArray();
        return form[$"q{q.Id}"].Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).ToArray();
    }

    public static string ToJson(string[] answer) => JsonSerializer.Serialize(answer);
    public static string[] FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return new[] { json }; }
    }

    /// <summary>Ответ в читаемом виде для преподавателя и истории.</summary>
    public static string Describe(IQuestion q, string[] answer)
    {
        string Opt(string id) => q.Options.FirstOrDefault(o => o.Id.ToString() == id)?.Text ?? id;
        return q.Type switch
        {
            QuestionType.Single or QuestionType.Multiple => string.Join("; ", answer.Select(Opt)),
            QuestionType.Matching => string.Join("; ", answer.Select(a => a.Split('|', 2)).Where(p => p.Length == 2).Select(p => $"{Opt(p[0])} → {p[1]}")),
            _ => string.Join(" ", answer),
        };
    }

    /// <summary>Правильный ответ в читаемом виде (для преподавателя).</summary>
    public static string CorrectText(IQuestion q) => q.Type switch
    {
        QuestionType.Single or QuestionType.Multiple => string.Join("; ", q.Options.Where(o => o.IsCorrect).Select(o => o.Text)),
        QuestionType.Matching => string.Join("; ", q.Options.Select(o => $"{o.Text} → {o.MatchText}")),
        QuestionType.Essay => "проверяет преподаватель",
        _ => string.Join(" / ", q.Options.Select(o => o.Text)),
    };
}
