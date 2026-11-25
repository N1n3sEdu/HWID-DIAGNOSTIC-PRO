namespace HWIDDiagnosticPro.Core.Models;

/// <summary>
/// Representa uma recomendação oficial da Central de Soluções (Seção 14).
/// </summary>
public sealed class SolutionRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ProblemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PossibleCause { get; set; } = string.Empty;
    public string PriorityLevel { get; set; } = "Média"; // Baixa, Média, Alta, Crítica
    public string PriorityColorHex => PriorityLevel switch
    {
        "Crítica" => "#DC2626",
        "Alta" => "#EF4444",
        "Média" => "#F59E0B",
        _ => "#3B82F6"
    };
    public string RecommendedProcedure { get; set; } = string.Empty;
    public string InvolvedRisks { get; set; } = string.Empty;
    public string OfficialDocumentationUrl { get; set; } = string.Empty;
    public bool IsTriggeredByScan { get; set; }
    public string TriggerConditionSummary { get; set; } = "Catálogo oficial de soluções";
}
