using System.Reflection;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Состояние системы для сопровождения: подключение к БД, папки, место на диске, последние ошибки из журнала.</summary>
public class SystemModel : AppPage
{
    readonly AppDb db;
    readonly MaterialStore materials;
    readonly BackupService backups;
    readonly FileLoggerProvider logs;
    readonly IWebHostEnvironment env;
    public SystemModel(AppDb db, MaterialStore materials, BackupService backups, FileLoggerProvider logs, IWebHostEnvironment env)
    { this.db = db; this.materials = materials; this.backups = backups; this.logs = logs; this.env = env; }

    public bool DbOk { get; set; }
    public string? DbError { get; set; }
    public string Provider => db.Database.ProviderName ?? "";
    public int Materials { get; set; }
    public long MaterialsSize { get; set; }
    public FileInfo? LastBackup { get; set; }
    public long? DiskFree { get; set; }
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
    public string Environment => env.EnvironmentName;
    public string LogFolder => logs.Folder;
    public string LogTail { get; set; } = "";
    public int ErrorsToday { get; set; }
    public DateTime Started => System.Diagnostics.Process.GetCurrentProcess().StartTime;

    public async Task OnGetAsync()
    {
        try { DbOk = await db.Database.CanConnectAsync(); if (DbOk) await db.Users.CountAsync(); }
        catch (Exception ex) { DbOk = false; DbError = ex.Message; }
        var files = new DirectoryInfo(materials.Folder).GetFiles();
        Materials = files.Length; MaterialsSize = files.Sum(f => f.Length);
        LastBackup = backups.List().FirstOrDefault();
        try { DiskFree = new DriveInfo(Path.GetPathRoot(materials.Folder)!).AvailableFreeSpace; } catch { }
        var log = Path.Combine(logs.Folder, $"app-{DateTime.Now:yyyyMMdd}.log");
        if (System.IO.File.Exists(log))
        {
            string text;
            using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(fs)) text = await r.ReadToEndAsync();
            var lines = text.Split('\n');
            ErrorsToday = lines.Count(l => l.Contains("[Error]") || l.Contains("[Critical]"));
            LogTail = string.Join("\n", lines.TakeLast(60));
        }
    }
}
