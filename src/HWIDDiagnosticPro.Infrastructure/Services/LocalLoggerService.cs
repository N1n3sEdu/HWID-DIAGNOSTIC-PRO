using System.Collections.Concurrent;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Services;

public sealed class LocalLoggerService : ILocalLoggerService
{
    private readonly ConcurrentQueue<SystemEventLogEntry> _entries = new();
    private readonly string _logFilePath;
    private readonly object _fileLock = new();

    public LocalLoggerService(string? customDirectory = null)
    {
        var baseDir = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HWIDDiagnosticPro",
            "logs");

        try
        {
            Directory.CreateDirectory(baseDir);
        }
        catch
        {
            // Ignorar caso o diretório não possa ser criado em ambientes restritos
        }

        _logFilePath = Path.Combine(baseDir, $"diagnostic_{DateTime.Now:yyyyMMdd}.log");
    }

    public void LogInfo(string category, string message) =>
        Record(category, "Informação", message, DiagnosticStatus.NORMAL);

    public void LogWarning(string category, string message) =>
        Record(category, "Alerta", message, DiagnosticStatus.ATENCAO);

    public void LogError(string category, string message, Exception? exception = null)
    {
        var fullMsg = exception == null ? message : $"{message} | Exceção: {exception.GetType().Name}: {exception.Message}";
        Record(category, "Erro", fullMsg, DiagnosticStatus.ERRO);
    }

    public IReadOnlyList<SystemEventLogEntry> GetApplicationLogs() =>
        _entries.OrderByDescending(e => e.Timestamp).ToList();

    private void Record(string category, string severity, string message, DiagnosticStatus status)
    {
        var sanitized = PrivacyMasker.MaskUserPath(message);
        var entry = new SystemEventLogEntry
        {
            Timestamp = DateTime.Now,
            LogName = "HWID Diagnostic Pro",
            Source = category,
            EventId = status switch
            {
                DiagnosticStatus.ERRO => 500,
                DiagnosticStatus.ATENCAO => 300,
                _ => 100
            },
            Severity = severity,
            Category = category,
            Message = sanitized,
            Status = status
        };

        _entries.Enqueue(entry);
        while (_entries.Count > 500)
        {
            _entries.TryDequeue(out _);
        }

        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, $"[{entry.TimestampDisplay}] [{severity}] [{category}] {sanitized}{Environment.NewLine}");
            }
        }
        catch
        {
            // Nunca interromper a aplicação por erro de gravação de log local
        }
    }
}
