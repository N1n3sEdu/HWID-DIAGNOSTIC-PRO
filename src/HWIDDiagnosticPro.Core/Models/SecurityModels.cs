namespace HWIDDiagnosticPro.Core.Models;

public sealed class SecurityFeatureItem
{
    public string Name { get; set; } = string.Empty;
    public string CurrentState { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string PurposeExplanation { get; set; } = string.Empty;
    public string OfficialRecommendation { get; set; } = string.Empty;
}

public sealed class WindowsSecurityResult
{
    public string Tpm20Status { get; set; } = "Não disponível";
    public string SecureBootStatus { get; set; } = "Não disponível";
    public string MicrosoftDefenderStatus { get; set; } = "Não disponível";
    public string FirewallStatus { get; set; } = "Não disponível";
    public string MemoryIntegrityStatus { get; set; } = "Não disponível";
    public string CoreIsolationStatus { get; set; } = "Não disponível";
    public string WindowsUpdateStatus { get; set; } = "Não disponível";
    public string DriverSignatureEnforcementStatus { get; set; } = "Não disponível";
    public string UacStatus { get; set; } = "Não disponível";
    public string BootConfigurations { get; set; } = "Não disponível";
    public string AvailableSecurityProtections { get; set; } = "Não disponível";
    public string WindowsVersionAndBuild { get; set; } = "Não disponível";
    public bool RebootRequired { get; set; }
    public string RebootRequiredDisplay { get; set; } = "Não disponível";
    public List<SecurityFeatureItem> SecurityFeatures { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
