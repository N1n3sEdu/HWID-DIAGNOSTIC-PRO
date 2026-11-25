using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.Core.Services;

public sealed class DiagnosticProgressUpdate
{
    public int Percentage { get; init; }
    public string CurrentModule { get; init; } = string.Empty;
    public string CurrentOperation { get; init; } = string.Empty;
}

public interface ILocalLoggerService
{
    void LogInfo(string category, string message);
    void LogWarning(string category, string message);
    void LogError(string category, string message, Exception? exception = null);
    IReadOnlyList<SystemEventLogEntry> GetApplicationLogs();
}

public interface ISettingsStorageService
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task SaveLastReportAsync(FullDiagnosticReport report, CancellationToken cancellationToken = default);
    Task<FullDiagnosticReport?> LoadLastReportAsync(CancellationToken cancellationToken = default);
}

public interface IHardwareDiagnosticService
{
    Task<HardwareSnapshot> AnalyzeHardwareAsync(CancellationToken cancellationToken = default);
}

public interface INetworkDiagnosticService
{
    Task<NetworkDiagnosticResult> AnalyzeNetworkAsync(bool checkEpicOnline, CancellationToken cancellationToken = default);
}

public interface IWindowsSecurityDiagnosticService
{
    Task<WindowsSecurityResult> AnalyzeSecurityAsync(CancellationToken cancellationToken = default);
}

public interface IDriversAndServicesDiagnosticService
{
    Task<DriversAndServicesResult> AnalyzeDriversAndServicesAsync(CancellationToken cancellationToken = default);
}

public interface IAntiCheatDiagnosticService
{
    Task<AntiCheatDiagnosticResult> AnalyzeAntiCheatAsync(CancellationToken cancellationToken = default);
}

public interface IFortniteDiagnosticService
{
    Task<FortniteDiagnosticResult> AnalyzeFortniteAsync(
        HardwareSnapshot? hardware = null,
        NetworkDiagnosticResult? network = null,
        CancellationToken cancellationToken = default);
}

public interface ISystemIntegrityDiagnosticService
{
    Task<SystemIntegrityResult> AnalyzeSystemIntegrityAsync(
        bool runOfficialVerificationTools = false,
        CancellationToken cancellationToken = default);
}

public interface IDiagnosticOrchestratorService
{
    Task<FullDiagnosticReport> RunFullDiagnosticAsync(
        IProgress<DiagnosticProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);

    Task<FullDiagnosticReport> RunQuickDiagnosticAsync(
        IProgress<DiagnosticProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IReportExportService
{
    string GenerateJsonReport(FullDiagnosticReport report);
    string GenerateTxtReport(FullDiagnosticReport report);
    string GenerateHtmlReport(FullDiagnosticReport report);
    byte[] GeneratePdfReport(FullDiagnosticReport report);
    string GenerateExecutiveSupportSummary(FullDiagnosticReport report);
    Task<string> ExportReportToFileAsync(FullDiagnosticReport report, string format, string outputDirectory, CancellationToken cancellationToken = default);
}
