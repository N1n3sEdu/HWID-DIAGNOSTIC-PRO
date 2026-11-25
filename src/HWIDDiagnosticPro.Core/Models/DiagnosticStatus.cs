namespace HWIDDiagnosticPro.Core.Models;

/// <summary>
/// Sistema oficial de classificação dos resultados (Seção 15).
/// Nunca utiliza classificações especulativas como "HWID limpo" ou "HWID banido".
/// </summary>
public enum DiagnosticStatus
{
    NORMAL,
    ATENCAO,
    ERRO,
    CRITICO,
    INDISPONIVEL,
    INCONCLUSIVO
}

public static class DiagnosticStatusExtensions
{
    public static string ToDisplayText(this DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.NORMAL => "NORMAL",
        DiagnosticStatus.ATENCAO => "ATENÇÃO",
        DiagnosticStatus.ERRO => "ERRO",
        DiagnosticStatus.CRITICO => "CRÍTICO",
        DiagnosticStatus.INDISPONIVEL => "INDISPONÍVEL",
        DiagnosticStatus.INCONCLUSIVO => "INCONCLUSIVO",
        _ => "INCONCLUSIVO"
    };

    public static string ToDescription(this DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.NORMAL => "Nenhum problema encontrado na verificação.",
        DiagnosticStatus.ATENCAO => "Configuração que merece análise.",
        DiagnosticStatus.ERRO => "Falha local efetivamente identificada.",
        DiagnosticStatus.CRITICO => "Problema confirmado que compromete o funcionamento ou a segurança.",
        DiagnosticStatus.INDISPONIVEL => "Informação que não pôde ser consultada.",
        DiagnosticStatus.INCONCLUSIVO => "Não existem informações suficientes.",
        _ => "Não existem informações suficientes."
    };

    public static string ToColorHex(this DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.NORMAL => "#10B981",
        DiagnosticStatus.ATENCAO => "#F59E0B",
        DiagnosticStatus.ERRO => "#EF4444",
        DiagnosticStatus.CRITICO => "#DC2626",
        DiagnosticStatus.INDISPONIVEL => "#6B7280",
        DiagnosticStatus.INCONCLUSIVO => "#8B5CF6",
        _ => "#6B7280"
    };
}
