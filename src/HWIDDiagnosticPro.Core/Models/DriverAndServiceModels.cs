namespace HWIDDiagnosticPro.Core.Models;

public sealed class InstalledDriverInfo
{
    public string DeviceName { get; set; } = "Não disponível";
    public string ServiceName { get; set; } = "Não disponível";
    public string Manufacturer { get; set; } = "Não disponível";
    public string Version { get; set; } = "Não disponível";
    public string DriverDate { get; set; } = "Não disponível";
    public bool? IsDigitallySigned { get; set; }
    public string DigitalSignatureDisplay => IsDigitallySigned switch
    {
        true => $"Assinado ({Signer})",
        false => "Não assinado (requer verificação manual; não indica automaticamente malware/trapaça)",
        null => "Não disponível"
    };
    public string Signer { get; set; } = "Não disponível";
    public string DeviceClass { get; set; } = "Não disponível";
    public bool HasRegisteredFailure { get; set; }
    public bool HasKnownIncompatibility { get; set; }
    public string IncompatibilityDetails { get; set; } = string.Empty;
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.NORMAL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class SystemServiceInfo
{
    public string ServiceName { get; set; } = "Não disponível";
    public string DisplayName { get; set; } = "Não disponível";
    public string State { get; set; } = "Não disponível";
    public string StartMode { get; set; } = "Não disponível";
    public string Category { get; set; } = "Serviço do Windows"; // "Serviço do Windows" ou "Serviço de Terceiros"
    public bool IsThirdParty { get; set; }
    public string BinaryPath { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.NORMAL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class ProblematicDeviceInfo
{
    public string DeviceName { get; set; } = "Não disponível";
    public string PnpDeviceId { get; set; } = "Não disponível";
    public uint ErrorCode { get; set; }
    public string ErrorExplanation { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.ERRO;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class DriversAndServicesResult
{
    public List<InstalledDriverInfo> InstalledDrivers { get; set; } = [];
    public List<SystemServiceInfo> WindowsServices { get; set; } = [];
    public List<SystemServiceInfo> ThirdPartyServices { get; set; } = [];
    public List<ProblematicDeviceInfo> DevicesWithErrors { get; set; } = [];
    public List<string> RegisteredDriverFailures { get; set; } = [];
    public List<string> KnownIncompatibilities { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
