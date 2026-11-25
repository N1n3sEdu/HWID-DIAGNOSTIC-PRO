namespace HWIDDiagnosticPro.Core.Models;

/// <summary>
/// Relatório técnico completo consolidado de todos os módulos de diagnóstico.
/// </summary>
public sealed class FullDiagnosticReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string GeneratedAtDisplay => GeneratedAt.ToString("dd/MM/yyyy HH:mm:ss");
    public string ApplicationVersion { get; set; } = "1.0.0";
    public string ScanMode { get; set; } = "Diagnóstico Completo";

    /// <summary>
    /// Indicador de integridade local de 0 a 100 (Seção 5).
    /// Calculado exclusivamente com base nas verificações técnicas locais documentadas.
    /// Nunca representa probabilidade de banimento.
    /// </summary>
    public int SystemHealthScore { get; set; } = 100;
    public string SystemHealthScoreDisclaimer { get; set; } =
        "Índice de integridade técnica local (0 a 100), calculado exclusivamente com base nas verificações locais do sistema. Este valor NÃO representa probabilidade de banimento ou status de sanção HWID.";

    public int ProblemsFoundCount { get; set; }
    public int AlertsFoundCount { get; set; }
    public int CompletedChecksCount { get; set; }

    public HardwareSnapshot Hardware { get; set; } = new();
    public NetworkDiagnosticResult Network { get; set; } = new();
    public WindowsSecurityResult Security { get; set; } = new();
    public DriversAndServicesResult DriversAndServices { get; set; } = new();
    public AntiCheatDiagnosticResult AntiCheat { get; set; } = new();
    public FortniteDiagnosticResult Fortnite { get; set; } = new();
    public AccessRestrictionAnalysisResult AccessRestrictionAnalysis { get; set; } = new();
    public SystemIntegrityResult SystemIntegrity { get; set; } = new();

    public List<DiagnosticCheckItem> AllChecks { get; set; } = [];
    public List<SolutionRecommendation> RecommendedSolutions { get; set; } = [];
    public List<string> DiagnosticLimitations { get; set; } =
    [
        "Este software funciona exclusivamente como ferramenta de diagnóstico local em modo de leitura.",
        "Não modifica identificadores de hardware (HWID), não altera configurações protegidas e não interfere em processos de jogos ou sistemas anticheat.",
        "Somente a Epic Games e os fornecedores oficiais de anticheat (Easy Anti-Cheat / BattlEye) possuem acesso aos registros internos de sanções nos servidores.",
        "Algumas verificações profundas do Windows (como SFC, DISM e leitura completa de sensores SMART/TPM) podem exigir execução com privilégios de Administrador."
    ];
}
