namespace HWIDDiagnosticPro.Core.Models;

public sealed class FortniteOfficialShortcut
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UrlOrUri { get; set; } = string.Empty;
}

public sealed class FortniteErrorInterpretation
{
    public string UserMessage { get; set; } = string.Empty;
    public string IdentifiedCodeOrPattern { get; set; } = string.Empty;
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INCONCLUSIVO;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string InterpretationSummary { get; set; } = string.Empty;
    public List<string> PossibleCauses { get; set; } = [];
    public List<string> OfficialRecommendations { get; set; } = [];
    public bool IsConclusive { get; set; }
    public string ConclusivenessNotice { get; set; } = string.Empty;
}

/// <summary>
/// Resultados possíveis estritos definidos na Seção 12 para Análise de Restrições de Acesso.
/// </summary>
public enum AccessRestrictionOutcome
{
    ProblemaLocalIdentificado,
    PossivelProblemaConectividade,
    PossivelFalhaAnticheat,
    SancaoInformadaPeloUsuario,
    PossivelRestricaoAcessoNaoConfirmada,
    DiagnosticoInconclusivo
}

public static class AccessRestrictionOutcomeExtensions
{
    public static string ToDisplayTitle(this AccessRestrictionOutcome outcome) => outcome switch
    {
        AccessRestrictionOutcome.ProblemaLocalIdentificado => "Problema local identificado",
        AccessRestrictionOutcome.PossivelProblemaConectividade => "Possível problema de conectividade",
        AccessRestrictionOutcome.PossivelFalhaAnticheat => "Possível falha no anticheat",
        AccessRestrictionOutcome.SancaoInformadaPeloUsuario => "Sanção informada pelo usuário",
        AccessRestrictionOutcome.PossivelRestricaoAcessoNaoConfirmada => "Possível restrição de acesso não confirmada",
        AccessRestrictionOutcome.DiagnosticoInconclusivo => "Diagnóstico inconclusivo",
        _ => "Diagnóstico inconclusivo"
    };
}

public sealed class AccessRestrictionQuestionnaire
{
    public string PunishmentDuration { get; set; } = "Nenhuma / Não informada";
    public string FortniteMessage { get; set; } = string.Empty;
    public bool CanAccessLobby { get; set; } = true;
    public bool KickedDuringMatch { get; set; }
    public bool WindowsReinstalled { get; set; }
    public bool UsesVpnOrProxy { get; set; }
    public bool ComputerPurchasedFromThirdParty { get; set; }
    public bool ConfirmedSanctionInEpicPortal { get; set; }
}

public sealed class AccessRestrictionAnalysisResult
{
    public AccessRestrictionOutcome Outcome { get; set; } = AccessRestrictionOutcome.DiagnosticoInconclusivo;
    public string OutcomeTitle => Outcome.ToDisplayTitle();
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INCONCLUSIVO;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string DetailedExplanation { get; set; } = string.Empty;
    public List<string> ContributingFactors { get; set; } = [];
    public List<string> OfficialNextSteps { get; set; } = [];
    public string MandatoryDisclaimer { get; set; } =
        "AVISO IMPORTANTE: Este software nunca afirma que uma placa-mãe, SSD, processador ou outro componente está banido sem confirmação oficial, nem calcula probabilidades de banimento HWID. Somente a Epic Games possui acesso aos registros internos de sanções através do Portal de Sanções e Recursos.";
}

public sealed class FortniteDiagnosticResult
{
    public bool IsInstalled { get; set; }
    public string InstallLocation { get; set; } = "Não disponível";
    public string MainFilesStatus { get; set; } = "Não disponível";
    public List<string> VerifiedFiles { get; set; } = [];
    public List<string> MissingFiles { get; set; } = [];
    public string EpicGamesLauncherStatus { get; set; } = "Não disponível";
    public string MinimumRequirementsStatus { get; set; } = "Não disponível";
    public bool MeetsMinimumRequirements { get; set; }
    public string GpuCompatibilityStatus { get; set; } = "Não disponível";
    public string DirectXVersion { get; set; } = "Não disponível";
    public string AvailableSpaceStatus { get; set; } = "Não disponível";
    public string VisualCppDependenciesStatus { get; set; } = "Não disponível";
    public List<string> StartupErrors { get; set; } = [];
    public List<string> UserLogFailures { get; set; } = [];
    public string ConnectivityIssuesStatus { get; set; } = "Não disponível";
    public string EpicPublicServicesStatus { get; set; } = "Não disponível";
    public List<string> WindowsExecutionIssues { get; set; } = [];
    public List<FortniteOfficialShortcut> OfficialShortcuts { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
