using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrmSales;

/// <summary>
/// Журнал изменений для отката действий сотрудника.
/// При каждом сохранении сравнивает клиентов, заявки и историю с состоянием после прошлого сохранения
/// и записывает, кто что создал, изменил или удалил (состояние «до» и «после»).
/// </summary>
public static class ChangeTracker
{
    const int MaxRecords = 5000;

    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    // Состояние после последнего сохранения: тип объекта → (Id → JSON)
    static Dictionary<string, Dictionary<string, string>>? snapshot;

    /// <summary>Запоминает текущее состояние без записи изменений (после загрузки, восстановления, сброса).</summary>
    public static void Reset() => snapshot = Capture(Db.Data);

    static Dictionary<string, Dictionary<string, string>> Capture(CrmData d) => new()
    {
        ["Client"] = d.Clients.ToDictionary(x => x.Id, x => JsonSerializer.Serialize(x, Json)),
        ["Deal"] = d.Deals.ToDictionary(x => x.Id, x => JsonSerializer.Serialize(x, Json)),
        ["Interaction"] = d.Interactions.ToDictionary(x => x.Id, x => JsonSerializer.Serialize(x, Json)),
    };

    /// <summary>Сравнивает данные с прошлым сохранением и добавляет записи в журнал изменений.</summary>
    public static void Track()
    {
        var now = Capture(Db.Data);
        if (snapshot == null) { snapshot = now; return; }

        var user = Db.Current;
        var action = Db.Data.Log.FirstOrDefault()?.Action ?? "Изменение данных";
        long nextId = Db.Data.Changes.Count == 0 ? 1 : Db.Data.Changes.Max(c => c.Id) + 1;
        var date = DateTime.Now;

        foreach (var (entity, current) in now)
        {
            var before = snapshot[entity];
            foreach (var (id, json) in current)
            {
                before.TryGetValue(id, out var old);
                if (old != null && Same(old, json)) continue;
                Add(entity, id, old, json);
            }
            foreach (var (id, old) in before)
                if (!current.ContainsKey(id)) Add(entity, id, old, null);
        }
        if (Db.Data.Changes.Count > MaxRecords) Db.Data.Changes.RemoveRange(0, Db.Data.Changes.Count - MaxRecords);
        snapshot = now;

        void Add(string entity, string id, string? before, string? after) => Db.Data.Changes.Add(new ChangeRecord
        {
            Id = nextId++, Date = date, UserId = user?.Id ?? "", UserName = user?.Name ?? "Система", Action = action,
            Entity = entity, EntityId = id, Title = TitleOf(entity, after ?? before!), Before = before, After = after,
        });
    }

    /// <summary>Поле Updated (время последнего изменения) не считаем изменением.</summary>
    static bool Same(string a, string b)
    {
        if (a == b) return true;
        var na = JsonNode.Parse(a)!.AsObject();
        var nb = JsonNode.Parse(b)!.AsObject();
        na.Remove("Updated");
        nb.Remove("Updated");
        return na.ToJsonString() == nb.ToJsonString();
    }

    static string TitleOf(string entity, string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        string P(string k) => n[k]?.ToString() ?? "";
        return entity switch
        {
            "Client" => P("Name"),
            "Deal" => $"{P("Title")} ({Db.ClientById(P("ClientId"))?.Name ?? "клиент удалён"})",
            _ => $"{Db.ClientById(P("ClientId"))?.Name ?? "клиент удалён"}: {Short(P("Text"))}",
        };
    }

    static string Short(string s) => s.Length > 60 ? s[..60] + "…" : s;

    public static string EntityName(string entity) => entity switch
    {
        "Client" => "Клиент",
        "Deal" => "Заявка",
        _ => "История",
    };

    static readonly Dictionary<string, string> FieldNames = new()
    {
        ["Name"] = "Название", ["Contact"] = "Контакт", ["Phone"] = "Телефон", ["Email"] = "Email", ["Inn"] = "ИНН",
        ["City"] = "Город", ["Source"] = "Источник", ["OwnerId"] = "Менеджер", ["Title"] = "Заявка", ["Amount"] = "Сумма",
        ["Status"] = "Статус", ["ClientId"] = "Клиент", ["Closed"] = "Закрыта", ["Text"] = "Текст", ["Type"] = "Тип",
    };

    /// <summary>Человекочитаемое описание: какие поля и как изменились.</summary>
    public static string Describe(ChangeRecord r)
    {
        if (r.Before == null) return "Создано";
        if (r.After == null) return "Удалено";
        var a = JsonNode.Parse(r.Before)!.AsObject();
        var b = JsonNode.Parse(r.After)!.AsObject();
        var parts = new List<string>();
        foreach (var (key, value) in b)
        {
            if (key is "Updated" or "Created" or "Id") continue;
            var oldV = a[key]?.ToJsonString();
            var newV = value?.ToJsonString();
            if (oldV == newV) continue;
            parts.Add($"{FieldNames.GetValueOrDefault(key, key)}: {Show(key, a[key])} → {Show(key, value)}");
        }
        return parts.Count == 0 ? "Изменено" : string.Join("; ", parts);
    }

    static string Show(string key, JsonNode? v)
    {
        var s = v?.ToString() ?? "";
        if (s == "") return "(пусто)";
        return key switch
        {
            "OwnerId" => Db.UserName(s),
            "ClientId" => Db.ClientById(s)?.Name ?? s,
            "Status" => Names.Of(Enum.TryParse<DealStatus>(s, out var st) ? st : DealStatus.New),
            "Closed" => DateTime.TryParse(s, out var d) ? d.ToString("dd.MM.yyyy") : s,
            _ => s,
        };
    }

    /// <summary>Изменения сотрудника за период, которые ещё можно отменить (сначала новые).</summary>
    public static List<ChangeRecord> ChangesOf(string userId, DateTime since) =>
        Db.Data.Changes.Where(c => c.UserId == userId && c.Date >= since && !c.Undone)
                       .OrderByDescending(c => c.Id).ToList();

    /// <summary>
    /// Отменяет изменения: возвращает объекты в состояние «до». Изменение пропускается, если тот же объект
    /// позже менял другой сотрудник (чтобы не затереть его работу).
    /// </summary>
    public static (int Undone, List<ChangeRecord> Skipped) Undo(IEnumerable<ChangeRecord> records)
    {
        int undone = 0;
        var skipped = new List<ChangeRecord>();
        foreach (var r in records.OrderByDescending(c => c.Id))
        {
            bool changedByOthers = Db.Data.Changes.Any(c => c.Id > r.Id && !c.Undone && c.Entity == r.Entity
                                                            && c.EntityId == r.EntityId && c.UserId != r.UserId);
            if (changedByOthers) { skipped.Add(r); continue; }
            Apply(r.Entity, r.EntityId, r.Before);
            r.Undone = true;
            undone++;
        }
        return (undone, skipped);
    }

    static void Apply(string entity, string id, string? json)
    {
        switch (entity)
        {
            case "Client": Replace(Db.Data.Clients, x => x.Id == id, json); break;
            case "Deal": Replace(Db.Data.Deals, x => x.Id == id, json); break;
            default: Replace(Db.Data.Interactions, x => x.Id == id, json); break;
        }
    }

    static void Replace<T>(List<T> list, Predicate<T> match, string? json)
    {
        int i = list.FindIndex(match);
        if (json == null) { if (i >= 0) list.RemoveAt(i); return; }
        var item = JsonSerializer.Deserialize<T>(json, Json)!;
        if (i >= 0) list[i] = item; else list.Add(item);
    }
}
