namespace StudyCabinet.Services;

/// <summary>
/// Автоматическое резервное копирование: раз в день после заданного времени (Backup:DailyAt, по умолчанию 21:00)
/// создаёт копию, если за сегодня её ещё нет, и удаляет самые старые, оставляя Backup:Keep штук.
/// </summary>
public class AutoBackupService : BackgroundService
{
    readonly IServiceProvider services;
    readonly ILogger<AutoBackupService> log;
    readonly TimeSpan dailyAt;
    readonly int keep;

    public AutoBackupService(IServiceProvider services, IConfiguration config, ILogger<AutoBackupService> log)
    {
        this.services = services; this.log = log;
        dailyAt = TimeSpan.TryParse(config["Backup:DailyAt"], out var t) ? t : new TimeSpan(21, 0, 0);
        keep = Math.Max(1, config.GetValue<int?>("Backup:Keep") ?? 14);
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (DateTime.Now.TimeOfDay >= dailyAt)
                {
                    using var scope = services.CreateScope();
                    var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
                    var today = $"backup-{DateTime.Now:yyyyMMdd}";
                    if (!backups.List().Any(f => f.Name.StartsWith(today)))
                    {
                        var f = await backups.CreateAsync();
                        await scope.ServiceProvider.GetRequiredService<Audit>().WriteAsync("Автоматическая резервная копия", $"{f.Name}, {Fmt.Size(f.Length)}");
                        foreach (var old in backups.List().Skip(keep)) old.Delete();
                    }
                }
            }
            catch (Exception ex) { log.LogError(ex, "Ошибка автоматического резервного копирования"); }
            try { await Task.Delay(TimeSpan.FromMinutes(10), stop); } catch (TaskCanceledException) { }
        }
    }
}
