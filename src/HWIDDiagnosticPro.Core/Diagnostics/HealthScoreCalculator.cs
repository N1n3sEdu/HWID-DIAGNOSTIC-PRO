using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.Core.Diagnostics;

/// <summary>
/// Calcula o indicador de integridade local do computador de 0 a 100 (Seção 5).
/// Baseado exclusivamente em verificações locais documentadas.
/// Nunca representa probabilidade de banimento.
/// </summary>
public static class HealthScoreCalculator
{
    public static (int Score, int Problems, int Alerts, int CompletedChecks) Calculate(IEnumerable<DiagnosticCheckItem> checks)
    {
        var list = checks?.ToList() ?? [];
        if (list.Count == 0)
            return (100, 0, 0, 0);

        int problems = 0;
        int alerts = 0;
        int completed = 0;
        int deduction = 0;

        foreach (var check in list)
        {
            if (check.Status != DiagnosticStatus.INDISPONIVEL)
            {
                completed++;
            }

            switch (check.Status)
            {
                case DiagnosticStatus.CRITICO:
                    problems++;
                    deduction += 18;
                    break;
                case DiagnosticStatus.ERRO:
                    problems++;
                    deduction += 10;
                    break;
                case DiagnosticStatus.ATENCAO:
                    alerts++;
                    deduction += 4;
                    break;
                case DiagnosticStatus.INCONCLUSIVO:
                    deduction += 1;
                    break;
            }
        }

        int score = Math.Clamp(100 - deduction, 0, 100);
        return (score, problems, alerts, completed);
    }

    public static DiagnosticStatus DetermineOverallStatus(IEnumerable<DiagnosticCheckItem> checks)
    {
        var list = checks?.ToList() ?? [];
        if (list.Count == 0)
            return DiagnosticStatus.INDISPONIVEL;

        if (list.Any(c => c.Status == DiagnosticStatus.CRITICO))
            return DiagnosticStatus.CRITICO;
        if (list.Any(c => c.Status == DiagnosticStatus.ERRO))
            return DiagnosticStatus.ERRO;
        if (list.Any(c => c.Status == DiagnosticStatus.ATENCAO))
            return DiagnosticStatus.ATENCAO;
        if (list.Any(c => c.Status == DiagnosticStatus.NORMAL))
            return DiagnosticStatus.NORMAL;
        if (list.Any(c => c.Status == DiagnosticStatus.INCONCLUSIVO))
            return DiagnosticStatus.INCONCLUSIVO;

        return DiagnosticStatus.INDISPONIVEL;
    }
}
