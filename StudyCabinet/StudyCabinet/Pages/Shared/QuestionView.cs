using StudyCabinet.Models;

namespace StudyCabinet.Pages.Shared;

/// <summary>Данные для вывода одного вопроса: сам вопрос, ответ студента, порядок вариантов, можно ли менять ответ.</summary>
public class QuestionView
{
    public IQuestion Q { get; init; } = null!;
    public int Number { get; init; }
    public string[] Answer { get; init; } = Array.Empty<string>();
    /// <summary>Порядок вариантов (перемешанный для теста). null — как задал преподаватель.</summary>
    public int[]? Order { get; init; }
    public bool Disabled { get; init; }
    /// <summary>Результат проверки для подсветки: correct, wrong, pending или null.</summary>
    public string? Result { get; init; }
    public string? Note { get; init; }

    public IEnumerable<AnswerOption> Options =>
        Order == null ? Q.Options.OrderBy(o => o.Order) : Order.Select(id => Q.Options.FirstOrDefault(o => o.Id == id)).OfType<AnswerOption>();

    /// <summary>Правые части для соответствия — в перемешанном, но постоянном для вопроса порядке.</summary>
    public List<string> MatchChoices =>
        Q.Options.Select(o => o.MatchText ?? "").Distinct().OrderBy(x => new Random(Q.Id * 31 + x.Length).Next()).ThenBy(x => x).ToList();

    public string? MatchAnswer(int optionId) =>
        Answer.Select(a => a.Split('|', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == optionId.ToString())?[1];
}
