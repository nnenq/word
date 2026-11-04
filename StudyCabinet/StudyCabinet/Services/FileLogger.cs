using System.Collections.Concurrent;

namespace StudyCabinet.Services;

/// <summary>
/// Запись предупреждений и ошибок в файлы App_Data/logs/app-ГГГГММДД.log (по дню на файл).
/// Журнал ошибок смотрит преподаватель на странице «Состояние системы».
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public string Folder { get; }
    readonly object sync = new();
    readonly ConcurrentDictionary<string, FileLogger> loggers = new();

    public FileLoggerProvider(string folder) { Folder = folder; Directory.CreateDirectory(folder); }

    public ILogger CreateLogger(string categoryName) => loggers.GetOrAdd(categoryName, c => new FileLogger(this, c));
    public void Dispose() { }

    internal void Write(string line)
    {
        lock (sync) File.AppendAllText(Path.Combine(Folder, $"app-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
    }

    sealed class FileLogger : ILogger
    {
        readonly FileLoggerProvider owner;
        readonly string category;
        public FileLogger(FileLoggerProvider owner, string category) { this.owner = owner; this.category = category; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {category}: {formatter(state, ex)}";
            if (ex != null) text += Environment.NewLine + ex;
            try { owner.Write(text); } catch { /* журнал не должен ронять приложение */ }
        }
    }
}
