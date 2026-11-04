using System.ComponentModel.DataAnnotations;

namespace StudyCabinet.Models;

public static class Roles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public static string Name(string role) => role == Teacher ? "Преподаватель" : "Студент";
}

/// <summary>Пользователь: преподаватель или студент. Пароль хранится только в виде хеша.</summary>
public class User
{
    public int Id { get; set; }
    [MaxLength(50)] public string Login { get; set; } = "";
    [MaxLength(200)] public string PasswordHash { get; set; } = "";
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(20)] public string Role { get; set; } = Roles.Student;
    [MaxLength(100)] public string? Email { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<GroupMembership> Memberships { get; set; } = new();
}

public class StudentGroup
{
    public int Id { get; set; }
    [MaxLength(50)] public string Name { get; set; } = "";
    public List<GroupMembership> Members { get; set; } = new();
}

/// <summary>Связь студента с учебной группой.</summary>
public class GroupMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int GroupId { get; set; }
    public StudentGroup Group { get; set; } = null!;
}

public class Discipline
{
    public int Id { get; set; }
    [MaxLength(150)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<Topic> Topics { get; set; } = new();
}

public class Topic
{
    public int Id { get; set; }
    public int DisciplineId { get; set; }
    public Discipline Discipline { get; set; } = null!;
    [MaxLength(150)] public string Title { get; set; } = "";
    /// <summary>Порядок темы в дисциплине.</summary>
    public int Order { get; set; }
    public List<Lecture> Lectures { get; set; } = new();
}

public class Lecture
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    /// <summary>Номер лекции, который видят студенты.</summary>
    public int Number { get; set; }
    [MaxLength(150)] public string Title { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    /// <summary>Теория, набранная прямо в приложении (простая разметка: # заголовок, - список, **жирный**).</summary>
    public string Content { get; set; } = "";
    /// <summary>Порядок лекции внутри темы.</summary>
    public int Order { get; set; }

    // Загруженный файл с материалом (PDF, DOCX и др.)
    [MaxLength(200)] public string? MaterialFile { get; set; }
    [MaxLength(200)] public string? MaterialName { get; set; }
    public DateTime? MaterialUpdatedAt { get; set; }
    public int MaterialVersion { get; set; }

    // Условия открытия следующей лекции
    /// <summary>Нужно ответить на все контрольные вопросы.</summary>
    public bool RequireControl { get; set; } = true;
    /// <summary>Ответы на контрольные вопросы с автопроверкой должны быть правильными.</summary>
    public bool RequireControlCorrect { get; set; }
    /// <summary>Развёрнутые ответы должны быть приняты преподавателем.</summary>
    public bool RequireReview { get; set; } = true;

    public Test? Test { get; set; }
    public List<ControlQuestion> ControlQuestions { get; set; } = new();
}

public enum QuestionType
{
    ShortText = 1,  // краткий текстовый ответ
    Single = 2,     // один правильный вариант
    Multiple = 3,   // несколько правильных вариантов
    Matching = 4,   // установление соответствия
    Gap = 5,        // пропущенное слово или термин
    Essay = 6,      // развёрнутый ответ, проверяет преподаватель
}

public static class QuestionTypes
{
    public static string Name(QuestionType t) => t switch
    {
        QuestionType.ShortText => "Краткий ответ",
        QuestionType.Single => "Один вариант",
        QuestionType.Multiple => "Несколько вариантов",
        QuestionType.Matching => "Соответствие",
        QuestionType.Gap => "Пропущенное слово",
        _ => "Развёрнутый ответ",
    };
    public static bool HasChoices(QuestionType t) => t is QuestionType.Single or QuestionType.Multiple;
}

/// <summary>Общие поля контрольного вопроса и вопроса теста.</summary>
public interface IQuestion
{
    int Id { get; }
    string Text { get; }
    QuestionType Type { get; }
    List<AnswerOption> Options { get; }
}

public class ControlQuestion : IQuestion
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;
    [MaxLength(1000)] public string Text { get; set; } = "";
    public QuestionType Type { get; set; }
    public int Order { get; set; }
    public List<AnswerOption> Options { get; set; } = new();
}

public class Test
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;
    /// <summary>Сколько вопросов выдавать из банка (0 — все).</summary>
    public int QuestionCount { get; set; }
    /// <summary>Число попыток (0 — без ограничения).</summary>
    public int MaxAttempts { get; set; } = 3;
    public int PassPercent { get; set; } = 70;
    /// <summary>Ограничение времени в минутах (0 — без ограничения).</summary>
    public int TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    /// <summary>После неудачной попытки можно пройти тест ещё раз (в пределах числа попыток).</summary>
    public bool AllowRetry { get; set; } = true;
    public List<TestQuestion> Questions { get; set; } = new();
}

public class TestQuestion : IQuestion
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    [MaxLength(1000)] public string Text { get; set; } = "";
    public QuestionType Type { get; set; }
    public int Order { get; set; }
    public List<AnswerOption> Options { get; set; } = new();
}

/// <summary>
/// Вариант ответа. Для выбора — текст и признак правильности, для краткого ответа и пропущенного слова —
/// допустимый правильный ответ, для соответствия — левая часть (Text) и правая (MatchText).
/// </summary>
public class AnswerOption
{
    public int Id { get; set; }
    public int? ControlQuestionId { get; set; }
    public ControlQuestion? ControlQuestion { get; set; }
    public int? TestQuestionId { get; set; }
    public TestQuestion? TestQuestion { get; set; }
    [MaxLength(500)] public string Text { get; set; } = "";
    [MaxLength(500)] public string? MatchText { get; set; }
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}

public enum AnswerStatus { Correct = 1, Wrong = 2, Pending = 3, Accepted = 4, Rejected = 5 }

/// <summary>Ответ студента на контрольный вопрос (хранится последний ответ).</summary>
public class StudentAnswer
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public User Student { get; set; } = null!;
    public int ControlQuestionId { get; set; }
    public ControlQuestion ControlQuestion { get; set; } = null!;
    [MaxLength(4000)] public string Answer { get; set; } = "";
    public AnswerStatus Status { get; set; }
    [MaxLength(1000)] public string? TeacherComment { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.Now;
    public DateTime? CheckedAt { get; set; }
    public int? CheckedById { get; set; }
}

public enum AttemptStatus { InProgress = 1, Passed = 2, Failed = 3, Expired = 4 }

public class TestAttempt
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public User Student { get; set; } = null!;
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? Deadline { get; set; }
    public DateTime? FinishedAt { get; set; }
    /// <summary>Выданные вопросы и порядок вариантов: JSON.</summary>
    public string QuestionsJson { get; set; } = "[]";
    /// <summary>Ответы студента: JSON.</summary>
    public string AnswersJson { get; set; } = "{}";
    public int Correct { get; set; }
    public int Total { get; set; }
    public int Percent { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
}

public enum ProgressStatus { Completed = 1, OpenedByTeacher = 2 }

/// <summary>Прогресс студента по лекции: пройдена или открыта преподавателем вручную, дополнительные попытки.</summary>
public class LectureProgress
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public User Student { get; set; } = null!;
    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;
    public ProgressStatus? Status { get; set; }
    public DateTime? FirstOpenedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Дополнительные попытки теста, выданные преподавателем.</summary>
    public int ExtraAttempts { get; set; }
}

/// <summary>Журнал событий: действия преподавателя и важные события системы.</summary>
public class AuditEntry
{
    public int Id { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
    public int? UserId { get; set; }
    [MaxLength(100)] public string UserName { get; set; } = "";
    [MaxLength(100)] public string Action { get; set; } = "";
    [MaxLength(2000)] public string Details { get; set; } = "";
}
