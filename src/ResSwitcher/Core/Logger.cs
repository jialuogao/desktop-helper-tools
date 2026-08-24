using System.IO;

namespace ResSwitcher.Core;

/// <summary>
/// 轻量文件日志：记录所有 error/exception。
/// 位置：%APPDATA%\ResSwitcher\logs\reswitcher-SESSION.log，每次进程会话单独分文件，自动清理 3 天前日志。
/// 线程安全；任何日志失败静默（不影响主流程）。
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _sessionId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
    private static string? _logDir;

    public static string LogDir
    {
        get
        {
            if (_logDir == null)
            {
                _logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ResSwitcher", "logs");
                try { Directory.CreateDirectory(_logDir); } catch { /* 忽略 */ }
            }
            return _logDir;
        }
    }

    public static string LogFile =>
        Path.Combine(LogDir, $"reswitcher-{_sessionId}.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex)
        => Write("ERROR", $"{message}\n{FormatException(ex)}");

    private static string FormatException(Exception exception)
    {
        var lines = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            lines.Add($"  Exception: {current.GetType().Name}: {current.Message}");
            if (current.StackTrace is not null)
                lines.Add($"  Stack: {current.StackTrace}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}");
                CleanupOldLogs();
            }
        }
        catch { /* 日志失败不影响主流程 */ }
    }

    /// <summary>删除最后写入时间早于三天前的日志文件。</summary>
    private static void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-3);
            foreach (var f in Directory.GetFiles(LogDir, "reswitcher-*.log"))
            {
                if (File.GetLastWriteTimeUtc(f) < cutoff)
                    File.Delete(f);
            }
        }
        catch { /* 忽略 */ }
    }
}
