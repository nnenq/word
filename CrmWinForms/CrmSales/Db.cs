using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace CrmSales;

public class BackupFile
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = "";
    public string By { get; set; } = "";
    public CrmData Data { get; set; } = new();
}

public record BackupInfo(string Path, DateTime Date, string Label, string By, int Clients, int Deals);

/// <summary>Хранение данных CRM в JSON-файле, резервные копии, журнал и права доступа.</summary>
public static class Db
{
    public static CrmData Data { get; private set; } = new();
    public static User? Current { get; set; }

    public static string DataDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data");
    public static string DbPath => Path.Combine(DataDir, "crm.json");
    public static string BackupDir => Path.Combine(DataDir, "Backups");
    const int MaxBackups = 30;

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() },
    };

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    public static string Hash(string login, string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.Trim().ToLowerInvariant() + ":" + password)));

    public static void Load()
    {
        Directory.CreateDirectory(BackupDir);
        if (File.Exists(DbPath))
        {
            Data = JsonSerializer.Deserialize<CrmData>(File.ReadAllText(DbPath, Encoding.UTF8), Json)
                   ?? throw new InvalidDataException("файл базы пуст");
        }
        else
        {
            Data = SeedData.Create();
            Save();
        }
    }

    /// <summary>Сохраняет базу через временный файл, чтобы сбой при записи не повредил данные.</summary>
    public static void Save()
    {
        Directory.CreateDirectory(DataDir);
        var tmp = DbPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Json), Encoding.UTF8);
        File.Move(tmp, DbPath, true);
    }

    public static void Log(string action)
    {
        Data.Log.Insert(0, new LogEntry { User = Current?.Name ?? "Система", Action = action });
        if (Data.Log.Count > 2000) Data.Log.RemoveRange(2000, Data.Log.Count - 2000);
    }

    // ---------- Резервные копии ----------

    public static BackupInfo Backup(string label)
    {
        Directory.CreateDirectory(BackupDir);
        var b = new BackupFile { Date = DateTime.Now, Label = label, By = Current?.Name ?? "Система", Data = Data };
        var path = Path.Combine(BackupDir, $"crm_{b.Date:yyyy-MM-dd_HH-mm-ss}_{NewId()[..4]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(b, Json), Encoding.UTF8);
        foreach (var old in Backups().Skip(MaxBackups)) File.Delete(old.Path);
        return new BackupInfo(path, b.Date, b.Label, b.By, Data.Clients.Count, Data.Deals.Count);
    }

    public static List<BackupInfo> Backups()
    {
        if (!Directory.Exists(BackupDir)) return new();
        var list = new List<BackupInfo>();
        foreach (var f in Directory.GetFiles(BackupDir, "*.json"))
        {
            try
            {
                var b = ReadBackup(f);
                list.Add(new BackupInfo(f, b.Date, b.Label, b.By, b.Data.Clients.Count, b.Data.Deals.Count));
            }
            catch { /* повреждённый файл пропускаем */ }
        }
        return list.OrderByDescending(b => b.Date).ToList();
    }

    static BackupFile ReadBackup(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        using var doc = JsonDocument.Parse(text);
        // Поддерживаем и файл копии, и «сырой» файл базы crm.json
        if (doc.RootElement.TryGetProperty("Data", out _))
            return JsonSerializer.Deserialize<BackupFile>(text, Json) ?? throw new InvalidDataException("пустой файл");
        var data = JsonSerializer.Deserialize<CrmData>(text, Json) ?? throw new InvalidDataException("пустой файл");
        return new BackupFile { Date = File.GetLastWriteTime(path), Label = "Файл базы " + Path.GetFileName(path), Data = data };
    }

    public static void Restore(string path)
    {
        var b = ReadBackup(path);
        if (b.Data.Users.Count == 0) throw new InvalidDataException("в копии нет пользователей");
        Backup("Автокопия перед восстановлением");
        var me = Current?.Id;
        Data = b.Data;
        Current = Data.Users.FirstOrDefault(u => u.Id == me && u.Active);
        Log($"Восстановление из резервной копии от {b.Date:dd.MM.yyyy HH:mm} («{b.Label}»)");
        Save();
    }

    /// <summary>Копирует внешний файл копии в папку Backups после проверки, что это копия CRM.</summary>
    public static void AddBackupFile(string file)
    {
        var b = ReadBackup(file);
        if (b.Data.Users.Count == 0 || b.Data.Clients is null) throw new InvalidDataException("это не файл резервной копии CRM");
        Directory.CreateDirectory(BackupDir);
        b.Label = "Загружена из файла «" + Path.GetFileName(file) + "»";
        File.WriteAllText(Path.Combine(BackupDir, $"crm_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{NewId()[..4]}.json"),
            JsonSerializer.Serialize(b, Json), Encoding.UTF8);
    }

    public static void ResetDemo()
    {
        Backup("Перед сбросом к демо-данным");
        var me = Current?.Login;
        Data = SeedData.Create();
        Current = Data.Users.FirstOrDefault(u => u.Login == me);
        Save();
    }

    // ---------- Права доступа ----------

    public static bool SeeAll => Current is { Role: not Role.Manager };
    public static bool IsAdmin => Current is { Role: Role.Admin };
    public static bool CanManage => SeeAll;
    public static bool CanEdit(Client c) => SeeAll || c.OwnerId == Current?.Id;

    public static IEnumerable<Client> VisibleClients() =>
        SeeAll ? Data.Clients : Data.Clients.Where(c => c.OwnerId == Current?.Id);

    public static IEnumerable<Deal> VisibleDeals() =>
        SeeAll ? Data.Deals : Data.Deals.Where(d => d.OwnerId == Current?.Id);

    public static IEnumerable<User> Managers => Data.Users.Where(u => u.Role == Role.Manager);

    public static string UserName(string? id) => Data.Users.FirstOrDefault(u => u.Id == id)?.Name ?? "—";
    public static Client? ClientById(string? id) => Data.Clients.FirstOrDefault(c => c.Id == id);

    public static void AddHistory(string clientId, InteractionType type, string text) =>
        Data.Interactions.Add(new Interaction { ClientId = clientId, UserId = Current?.Id ?? "", Type = type, Text = text });

    public static void SetDealStatus(Deal d, DealStatus status)
    {
        var old = d.Status;
        if (old == status) return;
        d.Status = status;
        d.Closed = d.IsClosed ? d.Closed ?? DateTime.Now : null;
        AddHistory(d.ClientId, InteractionType.System, $"Заявка «{d.Title}»: {Names.Of(old)} → {Names.Of(status)}");
        var c = ClientById(d.ClientId);
        if (c != null) c.Updated = DateTime.Now;
        Log($"Статус заявки «{d.Title}» изменён на «{Names.Of(status)}»");
        Save();
    }

    public static void DeleteClient(Client c)
    {
        Backup($"Перед удалением «{c.Name}»");
        Data.Clients.Remove(c);
        Data.Deals.RemoveAll(d => d.ClientId == c.Id);
        Data.Interactions.RemoveAll(i => i.ClientId == c.Id);
        Log($"Удалён клиент «{c.Name}»");
        Save();
    }
}
