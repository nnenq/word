using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;

namespace StudyCabinet.Services;

/// <summary>
/// Резервное копирование: архив ZIP с копией базы данных (SQLite) и папкой загруженных лекций.
/// Для SQL Server база копируется средствами сервера (команда BACKUP DATABASE, см. README), а архив содержит материалы.
/// </summary>
public class BackupService
{
    readonly AppDb db;
    readonly MaterialStore materials;
    public string Folder { get; }
    public bool IsSqlite => db.Database.IsSqlite();

    public BackupService(AppDb db, MaterialStore materials, IConfiguration config, IWebHostEnvironment env)
    {
        this.db = db; this.materials = materials;
        Folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Backups"] ?? "App_Data/backups"));
        Directory.CreateDirectory(Folder);
    }

    public IEnumerable<FileInfo> List() =>
        new DirectoryInfo(Folder).GetFiles("backup-*.zip").OrderByDescending(f => f.CreationTimeUtc).ThenByDescending(f => f.Name);

    public string? Find(string name)
    {
        // Только имя файла из этой папки — без путей вида ..\
        if (name != Path.GetFileName(name) || !name.StartsWith("backup-") || !name.EndsWith(".zip")) return null;
        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    public async Task<FileInfo> CreateAsync()
    {
        var path = Path.Combine(Folder, $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var tmp = Path.Combine(Folder, $"tmp-{Guid.NewGuid():N}.db");
        try
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            if (IsSqlite)
            {
                // Копия «на лету»: SQLite сам делает согласованный снимок базы, даже если сайт работает
                var source = (SqliteConnection)db.Database.GetDbConnection();
                await source.OpenAsync();
                using (var target = new SqliteConnection($"Data Source={tmp};Pooling=False"))
                {
                    target.Open();
                    source.BackupDatabase(target);
                }
                await source.CloseAsync();
                zip.CreateEntryFromFile(tmp, "study.db");
            }
            foreach (var f in Directory.GetFiles(materials.Folder))
                zip.CreateEntryFromFile(f, "materials/" + Path.GetFileName(f));
            using (var info = new StreamWriter(zip.CreateEntry("backup-info.txt").Open()))
                info.Write($"Электронный учебный кабинет — резервная копия\nСоздана: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                           $"База данных: {(IsSqlite ? "SQLite, файл study.db" : "SQL Server — копируется отдельно командой BACKUP DATABASE")}\n" +
                           $"Файлов лекций: {Directory.GetFiles(materials.Folder).Length}\n");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        return new FileInfo(path);
    }

    /// <summary>
    /// Восстановление из архива: база данных и материалы заменяются копией. Перед восстановлением
    /// автоматически делается ещё одна копия текущего состояния, чтобы можно было вернуться.
    /// </summary>
    public async Task<string?> RestoreAsync(string path)
    {
        if (!IsSqlite) return "Для SQL Server база восстанавливается средствами сервера (RESTORE DATABASE), см. инструкцию.";
        using (var check = ZipFile.OpenRead(path))
            if (check.GetEntry("study.db") == null) return "В архиве нет файла базы данных study.db.";

        await CreateAsync();
        var dbPath = ((SqliteConnection)db.Database.GetDbConnection()).DataSource;
        await db.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        using var zip = ZipFile.OpenRead(path);
        var tmp = dbPath + ".restore";
        zip.GetEntry("study.db")!.ExtractToFile(tmp, true);
        // Копирование через SQLite, а не простая замена файла: так не мешают открытые соединения
        using (var from = new SqliteConnection($"Data Source={tmp};Pooling=False"))
        using (var to = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
        {
            from.Open(); to.Open();
            from.BackupDatabase(to);
        }
        File.Delete(tmp);
        SqliteConnection.ClearAllPools();

        foreach (var f in Directory.GetFiles(materials.Folder)) File.Delete(f);
        foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("materials/") && e.Name.Length > 0))
            e.ExtractToFile(Path.Combine(materials.Folder, e.Name), true);
        return null;
    }
}
