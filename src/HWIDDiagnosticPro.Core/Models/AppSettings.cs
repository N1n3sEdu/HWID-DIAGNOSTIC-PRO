namespace HWIDDiagnosticPro.Core.Models;

public sealed class AppSettings
{
    public bool MaskSensitiveIdentifiersInUi { get; set; } = true;
    public bool AlwaysMaskReports { get; set; } = true;
    public int QueryTimeoutSeconds { get; set; } = 15;
    public bool EnableOnlineEpicStatusCheck { get; set; } = true;
    public bool SaveLocalDiagnosticHistory { get; set; } = true;
    public string DefaultReportFormat { get; set; } = "HTML";
    public string ReportsOutputDirectory { get; set; } = string.Empty;
    public DateTime? LastDiagnosticTimestamp { get; set; }
}
