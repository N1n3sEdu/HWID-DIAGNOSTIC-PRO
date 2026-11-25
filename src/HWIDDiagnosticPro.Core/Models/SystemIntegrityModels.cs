namespace HWIDDiagnosticPro.Core.Models;

public sealed class SystemEventLogEntry
{
    public DateTime Timestamp { get; set; }
    public string TimestampDisplay => Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
    public string LogName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string Severity { get; set; } = "Informação";
    public string Category { get; set; } = "Sistema";
    public string Message { get; set; } = string.Empty;
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.NORMAL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class SystemIntegrityResult
{
    public string SystemFilesIntegrityStatus { get; set; } = "Não disponível";
    public string WindowsComponentCorruptionStatus { get; set; } = "Não disponível";
    public string StorageErrorsStatus { get; set; } = "Não disponível";
    public string UpdateFailuresStatus { get; set; } = "Não disponível";
    public string MissingDependenciesStatus { get; set; } = "Não disponível";
    public string CriticalSystemErrorsStatus { get; set; } = "Não disponível";
    public string DriverRelatedEventsStatus { get; set; } = "Não disponível";
    public string BootIssuesStatus { get; set; } = "Não disponível";
    public string SfcVerifySummary { get; set; } = "Não executado nesta sessão";
    public string DismCheckHealthSummary { get; set; } = "Não executado nesta sessão";
    public string DismScanHealthSummary { get; set; } = "Verificação profunda sob demanda";
    public bool IsRunningElevated { get; set; }
    public List<SystemEventLogEntry> RelevantEvents { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
