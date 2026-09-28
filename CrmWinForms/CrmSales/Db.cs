using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace CrmSales;

public record BackupInfo(string Path, DateTime Date, string Label, string By, int Clients, int Deals);

/// <summary>
/// Работа с базой данных SQLite (файл Data\crm.db): схема таблиц, загрузка и сохранение,
/// резервные копии и восстановление, журнал действий и права доступа.
/// </summary>
public static class Db
{
    public static CrmData Data { get; private set; } = new();
    public static User? Current { get; set; }

    public static string DataDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data");
    public static string DbPath => Path.Combine(DataDir, "crm.db");
    public static string BackupDir => Path.Combine(DataDir, "Backups");
    const int MaxBackups = 30;

    /// <summary>Схема базы данных. Внешние ключи связывают клиентов, заявки, историю и пользователей.</summary>
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS Users (
            Id           TEXT PRIMARY KEY,
            Login        TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            Name         TEXT NOT NULL,
            Role         TEXT NOT NULL,          -- Manager / Head / Admin
            Pilot        INTEGER NOT NULL DEFAULT 0,
            Active       INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS Clients (
            Id      TEXT PRIMARY KEY,
            Name    TEXT NOT NULL,
            Contact TEXT, Phone TEXT, Email TEXT, Inn TEXT, City TEXT, Source TEXT,
            OwnerId TEXT NOT NULL REFERENCES Users(Id),
            Created TEXT NOT NULL,
            Updated TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Deals (
            Id       TEXT PRIMARY KEY,
            ClientId TEXT NOT NULL REFERENCES Clients(Id) ON DELETE CASCADE,
            OwnerId  TEXT NOT NULL REFERENCES Users(Id),
            Title    TEXT NOT NULL,
            Amount   REAL NOT NULL DEFAULT 0,
            Status   TEXT NOT NULL,              -- New / InWork / Offer / Won / Lost
            Created  TEXT NOT NULL,
            Closed   TEXT
        );
        CREATE TABLE IF NOT EXISTS Interactions (
            Id       TEXT PRIMARY KEY,
            ClientId TEXT NOT NULL REFERENCES Clients(Id) ON DELETE CASCADE,
            UserId   TEXT,
            Type     TEXT NOT NULL,              -- Call / Mail / Meeting / Note / System
            Text     TEXT NOT NULL,
            Date     TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Stages (
            Id TEXT PRIMARY KEY, Num INTEGER NOT NULL, Title TEXT NOT NULL, Hint TEXT, Status TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Risks (
            Id TEXT PRIMARY KEY, Num INTEGER NOT NULL, Title TEXT NOT NULL, Probability TEXT, Measure TEXT, Status TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Feedback (
            Id TEXT PRIMARY KEY, UserId TEXT REFERENCES Users(Id), Date TEXT NOT NULL, Kind TEXT NOT NULL,
            Rating INTEGER NOT NULL, Text TEXT NOT NULL, Resolved INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS Imports (
            Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, FileName TEXT NOT NULL,
            Total INTEGER NOT NULL, Created INTEGER NOT NULL, Merged INTEGER NOT NULL, Empty INTEGER NOT NULL,
            Updated INTEGER NOT NULL DEFAULT 0, Unchanged INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS Searches (
            Id INTEGER PRIMARY KEY AUTOINCREMENT, Seconds REAL NOT NULL, Date TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);
        CREATE TABLE IF NOT EXISTS Log (
            Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, User TEXT NOT NULL, Action TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Meta (Key TEXT PRIMARY KEY, Value TEXT);
        CREATE TABLE IF NOT EXISTS Changes (      -- журнал изменений для отката действий сотрудника
            Id       INTEGER PRIMARY KEY,
            Date     TEXT NOT NULL,
            UserId   TEXT, UserName TEXT, Action TEXT,
            Entity   TEXT NOT NULL,                  -- Client / Deal / Interaction
            EntityId TEXT NOT NULL,
            Title    TEXT,
            Before   TEXT,                           -- состояние до изменения (JSON), NULL — объект создан
            After    TEXT,                           -- состояние после изменения (JSON), NULL — объект удалён
            Undone   INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS IX_Clients_Owner ON Clients(OwnerId);
        CREATE INDEX IF NOT EXISTS IX_Deals_Client ON Deals(ClientId);
        CREATE INDEX IF NOT EXISTS IX_Interactions_Client ON Interactions(ClientId);
        """;

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    public static string Hash(string login, string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.Trim().ToLowerInvariant() + ":" + password)));

    // ---------- Подключение ----------

    public static SqliteConnection Open(string path, bool readOnly = false)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false, // файл не остаётся занятым после закрытия (нужно для резервных копий)
        }.ToString();
        var c = new SqliteConnection(cs);
        c.Open();
        return c;
    }

    static void Exec(SqliteConnection c, string sql, SqliteTransaction? t = null)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = t;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Создаёт таблицы и добавляет столбцы, которых не было в базе прежней версии программы.</summary>
    static void EnsureSchema(SqliteConnection c)
    {
        Exec(c, Schema);
        var cols = Query(c, "SELECT name FROM pragma_table_info('Imports')", r => S(r, "name"));
        if (!cols.Contains("Updated")) Exec(c, "ALTER TABLE Imports ADD COLUMN Updated INTEGER NOT NULL DEFAULT 0");
        if (!cols.Contains("Unchanged")) Exec(c, "ALTER TABLE Imports ADD COLUMN Unchanged INTEGER NOT NULL DEFAULT 0");
    }

    static object Val(object? v) => v switch
    {
        null => DBNull.Value,
        DateTime d => d.ToString("o", CultureInfo.InvariantCulture),
        bool b => b ? 1 : 0,
        Enum e => e.ToString(),
        decimal m => (double)m,
        _ => v,
    };

    /// <summary>Вставка набора строк одним подготовленным запросом с параметрами.</summary>
    static void Insert(SqliteConnection c, SqliteTransaction t, string table, string[] cols, IEnumerable<object?[]> rows)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", cols.Select(x => "$" + x))})";
        var ps = cols.Select(x => cmd.Parameters.Add("$" + x, SqliteType.Text)).ToArray();
        foreach (var row in rows)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                ps[i].SqliteType = row[i] switch { int or bool => SqliteType.Integer, double or decimal => SqliteType.Real, _ => SqliteType.Text };
                ps[i].Value = Val(row[i]);
            }
            cmd.ExecuteNonQuery();
        }
    }

    static List<T> Query<T>(SqliteConnection c, string sql, Func<SqliteDataReader, T> map)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    static string S(SqliteDataReader r, string col) => r[col] is string s ? s : "";
    static DateTime D(SqliteDataReader r, string col) => DateTime.Parse(S(r, col), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    static DateTime? DN(SqliteDataReader r, string col) => r[col] is string s && s != "" ? DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;
    static T E<T>(SqliteDataReader r, string col) where T : struct, Enum => Enum.TryParse<T>(S(r, col), out var v) ? v : default;
    static int I(SqliteDataReader r, string col) => Convert.ToInt32(r[col]);
    static bool B(SqliteDataReader r, string col) => Convert.ToInt64(r[col]) != 0;

    // ---------- Загрузка и сохранение ----------

    public static void Load()
    {
        Directory.CreateDirectory(BackupDir);
        var oldJson = Path.Combine(DataDir, "crm.json");
        if (File.Exists(DbPath)) { Data = Read(DbPath); ChangeTracker.Reset(); }
        else if (File.Exists(oldJson))
        {
            // Перенос данных из прежней версии программы (JSON-файл) в базу SQLite
            Data = ReadJson(oldJson);
            ChangeTracker.Reset();
            Save();
            File.Move(oldJson, oldJson + ".old", true);
        }
        else
        {
            Data = SeedData.Create();
            ChangeTracker.Reset();
            Save();
        }
    }

    /// <summary>Читает все таблицы базы в объекты программы.</summary>
    public static CrmData Read(string path)
    {
        using var c = Open(path);
        EnsureSchema(c);
        var d = new CrmData
        {
            Users = Query(c, "SELECT * FROM Users ORDER BY rowid", r => new User
            {
                Id = S(r, "Id"), Login = S(r, "Login"), PasswordHash = S(r, "PasswordHash"), Name = S(r, "Name"),
                Role = E<Role>(r, "Role"), Pilot = B(r, "Pilot"), Active = B(r, "Active"),
            }),
            Clients = Query(c, "SELECT * FROM Clients ORDER BY rowid", r => new Client
            {
                Id = S(r, "Id"), Name = S(r, "Name"), Contact = S(r, "Contact"), Phone = S(r, "Phone"), Email = S(r, "Email"),
                Inn = S(r, "Inn"), City = S(r, "City"), Source = S(r, "Source"), OwnerId = S(r, "OwnerId"),
                Created = D(r, "Created"), Updated = D(r, "Updated"),
            }),
            Deals = Query(c, "SELECT * FROM Deals ORDER BY rowid", r => new Deal
            {
                Id = S(r, "Id"), ClientId = S(r, "ClientId"), OwnerId = S(r, "OwnerId"), Title = S(r, "Title"),
                Amount = Convert.ToDecimal(r["Amount"]), Status = E<DealStatus>(r, "Status"), Created = D(r, "Created"), Closed = DN(r, "Closed"),
            }),
            Interactions = Query(c, "SELECT * FROM Interactions ORDER BY rowid", r => new Interaction
            {
                Id = S(r, "Id"), ClientId = S(r, "ClientId"), UserId = S(r, "UserId"), Type = E<InteractionType>(r, "Type"),
                Text = S(r, "Text"), Date = D(r, "Date"),
            }),
            Plan = Query(c, "SELECT * FROM Stages ORDER BY Num", r => new Stage
            {
                Id = S(r, "Id"), Title = S(r, "Title"), Hint = S(r, "Hint"), Status = E<StageStatus>(r, "Status"),
            }),
            Risks = Query(c, "SELECT * FROM Risks ORDER BY Num", r => new Risk
            {
                Id = S(r, "Id"), Title = S(r, "Title"), Probability = S(r, "Probability"), Measure = S(r, "Measure"), Status = E<RiskStatus>(r, "Status"),
            }),
            Feedback = Query(c, "SELECT * FROM Feedback ORDER BY rowid", r => new Feedback
            {
                Id = S(r, "Id"), UserId = S(r, "UserId"), Date = D(r, "Date"), Kind = E<FeedbackKind>(r, "Kind"),
                Rating = I(r, "Rating"), Text = S(r, "Text"), Resolved = B(r, "Resolved"),
            }),
            Imports = Query(c, "SELECT * FROM Imports ORDER BY Id DESC", r => new ImportRecord
            {
                Date = D(r, "Date"), FileName = S(r, "FileName"), Total = I(r, "Total"), Created = I(r, "Created"), Merged = I(r, "Merged"), Empty = I(r, "Empty"),
                Updated = I(r, "Updated"), Unchanged = I(r, "Unchanged"),
            }),
            Searches = Query(c, "SELECT * FROM Searches ORDER BY Id", r => new SearchMeasure { Seconds = Convert.ToDouble(r["Seconds"]), Date = D(r, "Date") }),
            Log = Query(c, "SELECT * FROM Log ORDER BY Id DESC LIMIT 2000", r => new LogEntry { Date = D(r, "Date"), User = S(r, "User"), Action = S(r, "Action") }),
            Changes = Query(c, "SELECT * FROM Changes ORDER BY Id", r => new ChangeRecord
            {
                Id = Convert.ToInt64(r["Id"]), Date = D(r, "Date"), UserId = S(r, "UserId"), UserName = S(r, "UserName"), Action = S(r, "Action"),
                Entity = S(r, "Entity"), EntityId = S(r, "EntityId"), Title = S(r, "Title"),
                Before = r["Before"] as string, After = r["After"] as string, Undone = B(r, "Undone"),
            }),
        };
        var settings = Query(c, "SELECT Key, Value FROM Settings", r => (Key: S(r, "Key"), Value: S(r, "Value"))).ToDictionary(x => x.Key, x => x.Value);
        if (settings.TryGetValue("Company", out var company)) d.Settings.Company = company;
        if (settings.TryGetValue("BaselineSearchSec", out var bs) && int.TryParse(bs, out var bsi)) d.Settings.BaselineSearchSec = bsi;
        if (settings.TryGetValue("BaselineDealHours", out var bd) && int.TryParse(bd, out var bdi)) d.Settings.BaselineDealHours = bdi;
        if (d.Users.Count == 0) throw new InvalidDataException("в базе нет пользователей");
        return d;
    }

    /// <summary>Записывает все данные в базу в одной транзакции: при сбое база остаётся в прежнем состоянии.</summary>
    static void Write(string path, CrmData d, Dictionary<string, string>? meta = null)
    {
        using var c = Open(path);
        EnsureSchema(c);
        using var t = c.BeginTransaction();
        foreach (var table in new[] { "Changes", "Log", "Searches", "Imports", "Feedback", "Risks", "Stages", "Interactions", "Deals", "Clients", "Settings", "Users", "Meta" })
            Exec(c, "DELETE FROM " + table, t);

        Insert(c, t, "Users", new[] { "Id", "Login", "PasswordHash", "Name", "Role", "Pilot", "Active" },
            d.Users.Select(u => new object?[] { u.Id, u.Login, u.PasswordHash, u.Name, u.Role, u.Pilot, u.Active }));
        Insert(c, t, "Clients", new[] { "Id", "Name", "Contact", "Phone", "Email", "Inn", "City", "Source", "OwnerId", "Created", "Updated" },
            d.Clients.Select(x => new object?[] { x.Id, x.Name, x.Contact, x.Phone, x.Email, x.Inn, x.City, x.Source, x.OwnerId, x.Created, x.Updated }));
        var clientIds = d.Clients.Select(x => x.Id).ToHashSet();
        Insert(c, t, "Deals", new[] { "Id", "ClientId", "OwnerId", "Title", "Amount", "Status", "Created", "Closed" },
            d.Deals.Where(x => clientIds.Contains(x.ClientId))
                   .Select(x => new object?[] { x.Id, x.ClientId, x.OwnerId, x.Title, x.Amount, x.Status, x.Created, x.Closed }));
        Insert(c, t, "Interactions", new[] { "Id", "ClientId", "UserId", "Type", "Text", "Date" },
            d.Interactions.Where(x => clientIds.Contains(x.ClientId))
                          .Select(x => new object?[] { x.Id, x.ClientId, x.UserId, x.Type, x.Text, x.Date }));
        Insert(c, t, "Stages", new[] { "Id", "Num", "Title", "Hint", "Status" },
            d.Plan.Select((x, i) => new object?[] { x.Id, i + 1, x.Title, x.Hint, x.Status }));
        Insert(c, t, "Risks", new[] { "Id", "Num", "Title", "Probability", "Measure", "Status" },
            d.Risks.Select((x, i) => new object?[] { x.Id, i + 1, x.Title, x.Probability, x.Measure, x.Status }));
        Insert(c, t, "Feedback", new[] { "Id", "UserId", "Date", "Kind", "Rating", "Text", "Resolved" },
            d.Feedback.Select(x => new object?[] { x.Id, x.UserId, x.Date, x.Kind, x.Rating, x.Text, x.Resolved }));
        Insert(c, t, "Imports", new[] { "Date", "FileName", "Total", "Created", "Merged", "Empty", "Updated", "Unchanged" },
            Enumerable.Reverse(d.Imports).Select(x => new object?[] { x.Date, x.FileName, x.Total, x.Created, x.Merged, x.Empty, x.Updated, x.Unchanged }));
        Insert(c, t, "Changes", new[] { "Id", "Date", "UserId", "UserName", "Action", "Entity", "EntityId", "Title", "Before", "After", "Undone" },
            d.Changes.Select(x => new object?[] { (int)x.Id, x.Date, x.UserId, x.UserName, x.Action, x.Entity, x.EntityId, x.Title, x.Before, x.After, x.Undone }));
        Insert(c, t, "Searches", new[] { "Seconds", "Date" }, d.Searches.Select(x => new object?[] { x.Seconds, x.Date }));
        Insert(c, t, "Settings", new[] { "Key", "Value" }, new[]
        {
            new object?[] { "Company", d.Settings.Company },
            new object?[] { "BaselineSearchSec", d.Settings.BaselineSearchSec.ToString() },
            new object?[] { "BaselineDealHours", d.Settings.BaselineDealHours.ToString() },
        });
        Insert(c, t, "Log", new[] { "Date", "User", "Action" }, Enumerable.Reverse(d.Log).Select(x => new object?[] { x.Date, x.User, x.Action }));
        if (meta != null) Insert(c, t, "Meta", new[] { "Key", "Value" }, meta.Select(m => new object?[] { m.Key, m.Value }));
        t.Commit();
    }

    public static void Save()
    {
        Directory.CreateDirectory(DataDir);
        // Заявки и история без клиента в базу не попадают (внешний ключ), убираем их и из памяти
        var ids = Data.Clients.Select(x => x.Id).ToHashSet();
        Data.Deals.RemoveAll(x => !ids.Contains(x.ClientId));
        Data.Interactions.RemoveAll(x => !ids.Contains(x.ClientId));
        ChangeTracker.Track();
        Write(DbPath, Data);
    }

    public static void Log(string action)
    {
        Data.Log.Insert(0, new LogEntry { User = Current?.Name ?? "Система", Action = action });
        if (Data.Log.Count > 2000) Data.Log.RemoveRange(2000, Data.Log.Count - 2000);
    }

    // ---------- Резервные копии (отдельные файлы SQLite в папке Backups) ----------

    public static BackupInfo Backup(string label)
    {
        Directory.CreateDirectory(BackupDir);
        var date = DateTime.Now;
        var by = Current?.Name ?? "Система";
        var path = Path.Combine(BackupDir, $"crm_{date:yyyy-MM-dd_HH-mm-ss}_{NewId()[..4]}.db");
        Write(path, Data, new() { ["Label"] = label, ["By"] = by, ["Date"] = date.ToString("o") });
        foreach (var old in Backups().Skip(MaxBackups)) File.Delete(old.Path);
        return new BackupInfo(path, date, label, by, Data.Clients.Count, Data.Deals.Count);
    }

    public static List<BackupInfo> Backups()
    {
        if (!Directory.Exists(BackupDir)) return new();
        var list = new List<BackupInfo>();
        foreach (var f in Directory.GetFiles(BackupDir, "*.db"))
        {
            try
            {
                using var c = Open(f, readOnly: true);
                var meta = Query(c, "SELECT Key, Value FROM Meta", r => (K: S(r, "Key"), V: S(r, "Value"))).ToDictionary(x => x.K, x => x.V);
                var clients = Query(c, "SELECT COUNT(*) AS N FROM Clients", r => I(r, "N"))[0];
                var deals = Query(c, "SELECT COUNT(*) AS N FROM Deals", r => I(r, "N"))[0];
                var date = meta.TryGetValue("Date", out var ds) ? DateTime.Parse(ds, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : File.GetLastWriteTime(f);
                list.Add(new BackupInfo(f, date, meta.GetValueOrDefault("Label", Path.GetFileName(f)), meta.GetValueOrDefault("By", ""), clients, deals));
            }
            catch (SqliteException) { /* повреждённый файл пропускаем */ }
        }
        return list.OrderByDescending(b => b.Date).ToList();
    }

    public static void Restore(string path)
    {
        var restored = Read(path);
        Backup("Автокопия перед восстановлением");
        var me = Current?.Id;
        var info = Backups().FirstOrDefault(b => b.Path == path);
        Data = restored;
        ChangeTracker.Reset();
        Current = Data.Users.FirstOrDefault(u => u.Id == me && u.Active);
        Log($"Восстановление из резервной копии от {info?.Date ?? File.GetLastWriteTime(path):dd.MM.yyyy HH:mm} («{info?.Label ?? Path.GetFileName(path)}»)");
        Save();
    }

    /// <summary>Добавляет внешний файл базы (.db) в список копий после проверки, что это база CRM.</summary>
    public static void AddBackupFile(string file)
    {
        var d = Read(file);
        Directory.CreateDirectory(BackupDir);
        Write(Path.Combine(BackupDir, $"crm_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{NewId()[..4]}.db"), d,
            new() { ["Label"] = "Загружена из файла «" + Path.GetFileName(file) + "»", ["By"] = Current?.Name ?? "", ["Date"] = DateTime.Now.ToString("o") });
    }

    public static void ResetDemo()
    {
        Backup("Перед сбросом к демо-данным");
        var me = Current?.Login;
        Data = SeedData.Create();
        ChangeTracker.Reset();
        Current = Data.Users.FirstOrDefault(u => u.Login == me);
        Save();
    }

    static CrmData ReadJson(string path)
    {
        var opts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        return JsonSerializer.Deserialize<CrmData>(File.ReadAllText(path, Encoding.UTF8), opts) ?? throw new InvalidDataException("файл пуст");
    }

    // ---------- Сведения о базе и SQL-запросы (вкладка «База данных») ----------

    public static List<(string Table, long Rows)> TableStats()
    {
        using var c = Open(DbPath, readOnly: true);
        var tables = Query(c, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name", r => S(r, "name"));
        return tables.Select(t => (t, Query(c, $"SELECT COUNT(*) AS N FROM \"{t}\"", r => Convert.ToInt64(r["N"]))[0])).ToList();
    }

    /// <summary>Выполняет запрос только на чтение (SELECT). База открывается в режиме ReadOnly.</summary>
    public static DataTable RunQuery(string sql)
    {
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Разрешены только запросы на чтение (SELECT). Изменения данных выполняются через интерфейс программы.");
        using var c = Open(DbPath, readOnly: true);
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var table = new DataTable();
        for (int i = 0; i < r.FieldCount; i++) table.Columns.Add(r.GetName(i), typeof(string));
        while (r.Read() && table.Rows.Count < 1000)
            table.Rows.Add(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "" : (object)(Convert.ToString(r.GetValue(i), CultureInfo.CurrentCulture) ?? "")).ToArray());
        return table;
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
