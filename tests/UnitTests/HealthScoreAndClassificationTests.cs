using Xunit;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.UnitTests;

public sealed class HealthScoreAndClassificationTests
{
    [Fact]
    public void DiagnosticStatus_DisplaysOfficialPortugueseClassificationsOnly()
    {
        var allStatuses = Enum.GetValues<DiagnosticStatus>();
        Assert.Equal(6, allStatuses.Length);

        foreach (var status in allStatuses)
        {
            var display = status.ToDisplayText();
            Assert.DoesNotContain("HWID limpo", display, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HWID banido", display, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("indetectável", display, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HealthScoreCalculator_CalculatesExpectedScoreAndCounts()
    {
        var checks = new List<DiagnosticCheckItem>
        {
            new() { Id = "1", Status = DiagnosticStatus.NORMAL },
            new() { Id = "2", Status = DiagnosticStatus.ATENCAO },      // -4
            new() { Id = "3", Status = DiagnosticStatus.ERRO },         // -10
            new() { Id = "4", Status = DiagnosticStatus.CRITICO },      // -18
            new() { Id = "5", Status = DiagnosticStatus.INDISPONIVEL }  // 0 (not counted as completed)
        };

        var (score, problems, alerts, completed) = HealthScoreCalculator.Calculate(checks);

        Assert.Equal(68, score); // 100 - (4 + 10 + 18)
        Assert.Equal(2, problems); // ERRO + CRITICO
        Assert.Equal(1, alerts);   // ATENCAO
        Assert.Equal(4, completed);
        Assert.Equal(DiagnosticStatus.CRITICO, HealthScoreCalculator.DetermineOverallStatus(checks));
    }
}
