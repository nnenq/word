using Microsoft.AspNetCore.Mvc;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Резервные копии: создание, скачивание, восстановление.</summary>
public class BackupModel : AppPage
{
    readonly BackupService backups;
    readonly Audit audit;
    readonly ILogger<BackupModel> log;
    public BackupModel(BackupService backups, Audit audit, ILogger<BackupModel> log) { this.backups = backups; this.audit = audit; this.log = log; }

    public List<FileInfo> Files { get; set; } = new();
    public string Folder => backups.Folder;
    public bool IsSqlite => backups.IsSqlite;

    public void OnGet() => Files = backups.List().ToList();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var f = await backups.CreateAsync();
        await audit.WriteAsync("Резервная копия создана", $"{f.Name}, {Fmt.Size(f.Length)}");
        Success($"Резервная копия {f.Name} создана ({Fmt.Size(f.Length)}).");
        return RedirectToPage();
    }

    public IActionResult OnGetDownload(string name)
    {
        var path = backups.Find(name);
        return path == null ? NotFound() : PhysicalFile(path, "application/zip", name);
    }

    public async Task<IActionResult> OnPostRestoreAsync(string name)
    {
        var path = backups.Find(name);
        if (path == null) return NotFound();
        try
        {
            if (await backups.RestoreAsync(path) is string err) { Error(err); return RedirectToPage(); }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Ошибка восстановления из {Name}", name);
            Error("Не удалось восстановить копию: " + ex.Message);
            return RedirectToPage();
        }
        await audit.WriteAsync("Данные восстановлены из копии", name);
        Success($"Данные восстановлены из копии {name}. Перед восстановлением автоматически сохранена копия текущего состояния.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string name)
    {
        var path = backups.Find(name);
        if (path == null) return NotFound();
        System.IO.File.Delete(path);
        await audit.WriteAsync("Резервная копия удалена", name);
        Success($"Копия {name} удалена.");
        return RedirectToPage();
    }
}
