namespace HWIDDiagnosticPro.Core.Models;

/// <summary>
/// Representa um item individual de verificação técnica realizada localmente.
/// </summary>
public sealed class DiagnosticCheckItem
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string Purpose { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;
    public string OfficialRecommendation { get; set; } = string.Empty;
    public bool RequiresElevation { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
