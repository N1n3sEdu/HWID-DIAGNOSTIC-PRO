namespace HWIDDiagnosticPro.Core.Models;

public sealed class AntiCheatComponentStatus
{
    public string ComponentName { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string InstallationStatus => IsInstalled ? "Instalado" : "Não encontrado localmente";
    public string InstallationPath { get; set; } = "Não disponível";
    public string RegisteredServiceName { get; set; } = "Não disponível";
    public string RegisteredServiceStatus { get; set; } = "Não disponível";
    public string LocalVersion { get; set; } = "Não disponível";
    public string ExecutableDigitalSignature { get; set; } = "Não disponível";
    public List<string> PublicLogErrors { get; set; } = [];
    public string StartupFailures { get; set; } = "Nenhuma falha registrada";
    public string InstallationProblems { get; set; } = "Nenhum problema detectado";
    public string DocumentedIncompatibilities { get; set; } = "Nenhuma incompatibilidade detectada";
    public string OfficialUpdatesStatus { get; set; } = "Gerenciado automaticamente pelo inicializador do jogo";
    public bool RequiresOfficialRepairOrReinstall { get; set; }
    public string RepairRecommendation { get; set; } = "Nenhuma ação necessária.";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class AntiCheatDiagnosticResult
{
    public AntiCheatComponentStatus EasyAntiCheat { get; set; } = new() { ComponentName = "Easy Anti-Cheat" };
    public AntiCheatComponentStatus EasyAntiCheatEos { get; set; } = new() { ComponentName = "Easy Anti-Cheat EOS" };
    public AntiCheatComponentStatus BattlEye { get; set; } = new() { ComponentName = "BattlEye" };
    public AntiCheatComponentStatus EpicGamesLauncher { get; set; } = new() { ComponentName = "Epic Games Launcher" };
    public List<AntiCheatComponentStatus> AllComponents => [EasyAntiCheat, EasyAntiCheatEos, BattlEye, EpicGamesLauncher];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
