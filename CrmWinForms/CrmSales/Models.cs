namespace CrmSales;

public enum Role { Manager, Head, Admin }
public enum DealStatus { New, InWork, Offer, Won, Lost }
public enum InteractionType { Call, Mail, Meeting, Note, System }
public enum StageStatus { NotStarted, InProgress, Done }
public enum RiskStatus { Open, Controlled, Closed, Happened }
public enum FeedbackKind { Problem, Idea, Praise }

public class User
{
    public string Id { get; set; } = Db.NewId();
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Name { get; set; } = "";
    public Role Role { get; set; } = Role.Manager;
    public bool Pilot { get; set; }
    public bool Active { get; set; } = true;
}

public class Client
{
    public string Id { get; set; } = Db.NewId();
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Inn { get; set; } = "";
    public string City { get; set; } = "";
    public string Source { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Updated { get; set; } = DateTime.Now;
}

public class Deal
{
    public string Id { get; set; } = Db.NewId();
    public string ClientId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public DealStatus Status { get; set; } = DealStatus.New;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? Closed { get; set; }

    // Вычисляемые поля не сохраняются и не попадают в журнал изменений
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsClosed => Status is DealStatus.Won or DealStatus.Lost;
    [System.Text.Json.Serialization.JsonIgnore]
    public double Hours => ((Closed ?? DateTime.Now) - Created).TotalHours;
}

public class Interaction
{
    public string Id { get; set; } = Db.NewId();
    public string ClientId { get; set; } = "";
    public string UserId { get; set; } = "";
    public InteractionType Type { get; set; }
    public string Text { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
}

public class Stage
{
    public string Id { get; set; } = Db.NewId();
    public string Title { get; set; } = "";
    public string Hint { get; set; } = "";
    public StageStatus Status { get; set; }
}

public class Risk
{
    public string Id { get; set; } = Db.NewId();
    public string Title { get; set; } = "";
    public string Probability { get; set; } = "";
    public string Measure { get; set; } = "";
    public RiskStatus Status { get; set; }
}

public class Feedback
{
    public string Id { get; set; } = Db.NewId();
    public string UserId { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public FeedbackKind Kind { get; set; }
    public int Rating { get; set; } = 5;
    public string Text { get; set; } = "";
    public bool Resolved { get; set; }
}

public class ImportRecord
{
    public DateTime Date { get; set; } = DateTime.Now;
    public string FileName { get; set; } = "";
    public int Total { get; set; }
    public int Created { get; set; }
    public int Merged { get; set; }
    /// <summary>Строки с ID существующего клиента, в которых изменены данные (правка через Excel).</summary>
    public int Updated { get; set; }
    /// <summary>Строки с ID существующего клиента без изменений.</summary>
    public int Unchanged { get; set; }
    public int Empty { get; set; }
    public bool NoLoss => Created + Merged + Updated + Unchanged + Empty == Total;
}

public class SearchMeasure
{
    public double Seconds { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}

public class Settings
{
    public string Company { get; set; } = "ООО «Пример»";
    /// <summary>Время поиска информации о клиенте до внедрения (Excel и почта), секунд.</summary>
    public int BaselineSearchSec { get; set; } = 120;
    /// <summary>Время обработки заявки до внедрения, часов.</summary>
    public int BaselineDealHours { get; set; } = 48;
}

public class LogEntry
{
    public DateTime Date { get; set; } = DateTime.Now;
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
}

public class CrmData
{
    public List<User> Users { get; set; } = new();
    public List<Client> Clients { get; set; } = new();
    public List<Deal> Deals { get; set; } = new();
    public List<Interaction> Interactions { get; set; } = new();
    public List<Stage> Plan { get; set; } = new();
    public List<Risk> Risks { get; set; } = new();
    public List<Feedback> Feedback { get; set; } = new();
    public List<ImportRecord> Imports { get; set; } = new();
    public List<SearchMeasure> Searches { get; set; } = new();
    public Settings Settings { get; set; } = new();
    public List<LogEntry> Log { get; set; } = new();
    public List<ChangeRecord> Changes { get; set; } = new();
}

/// <summary>
/// Запись журнала изменений: что было (Before) и что стало (After) с клиентом, заявкой или записью истории.
/// По этим записям можно отменить действия конкретного сотрудника.
/// </summary>
public class ChangeRecord
{
    public long Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";      // Client / Deal / Interaction
    public string EntityId { get; set; } = "";
    public string Title { get; set; } = "";       // название объекта для отображения
    public string? Before { get; set; }            // null — объект был создан
    public string? After { get; set; }             // null — объект был удалён
    public bool Undone { get; set; }

    public string Kind => Before == null ? "Создание" : After == null ? "Удаление" : "Изменение";
}

/// <summary>Русские названия для перечислений.</summary>
public static class Names
{
    public static string Of(Role r) => r switch
    {
        Role.Admin => "Администратор",
        Role.Head => "Руководитель отдела",
        _ => "Менеджер",
    };

    public static string Of(DealStatus s) => s switch
    {
        DealStatus.New => "Новая",
        DealStatus.InWork => "В работе",
        DealStatus.Offer => "КП отправлено",
        DealStatus.Won => "Успешно",
        _ => "Отказ",
    };

    public static string Of(InteractionType t) => t switch
    {
        InteractionType.Call => "Звонок",
        InteractionType.Mail => "Письмо",
        InteractionType.Meeting => "Встреча",
        InteractionType.Note => "Заметка",
        _ => "Система",
    };

    public static string Of(StageStatus s) => s switch
    {
        StageStatus.Done => "Выполнен",
        StageStatus.InProgress => "В работе",
        _ => "Не начат",
    };

    public static string Of(RiskStatus s) => s switch
    {
        RiskStatus.Controlled => "Под контролем",
        RiskStatus.Closed => "Закрыт",
        RiskStatus.Happened => "Наступил",
        _ => "Открыт",
    };

    public static string Of(FeedbackKind k) => k switch
    {
        FeedbackKind.Problem => "Проблема",
        FeedbackKind.Idea => "Предложение",
        _ => "Нравится",
    };

    /// <summary>Находит значение перечисления по его русскому названию.</summary>
    public static T Parse<T>(string? text, Func<T, string> name) where T : struct, Enum
        => Enum.GetValues<T>().FirstOrDefault(v => name(v) == text);

    public static string[] All<T>(Func<T, string> name) where T : struct, Enum
        => Enum.GetValues<T>().Select(name).ToArray();
}
