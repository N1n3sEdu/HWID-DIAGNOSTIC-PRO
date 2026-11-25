using Xunit;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.UnitTests;

public sealed class FortniteAndRestrictionAnalyzerTests
{
    [Fact]
    public void FortniteErrorInterpreter_RecognizesMatchKickMessageAsNonConclusive()
    {
        var msg = "Você foi removido da partida devido a lag de internet, seu IP ou máquina, uso de VPN, ou por trapaça.";
        var result = FortniteErrorInterpreter.Interpret(msg);

        Assert.Equal(DiagnosticStatus.ATENCAO, result.Status);
        Assert.False(result.IsConclusive);
        Assert.Contains("NÃO CONCLUSIVO", result.ConclusivenessNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FortniteErrorInterpreter_RecognizesEac30005Error()
    {
        var result = FortniteErrorInterpreter.Interpret("Easy Anti-Cheat Error 30005: CreateFile failed with 32");

        Assert.Equal(DiagnosticStatus.ERRO, result.Status);
        Assert.True(result.IsConclusive);
        Assert.Contains("30005", result.IdentifiedCodeOrPattern);
    }

    [Fact]
    public void AccessRestrictionAnalyzer_CoversAllSixOfficialOutcomes()
    {
        // 1. Sanção informada pelo usuário
        var r1 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire
        {
            ConfirmedSanctionInEpicPortal = true,
            PunishmentDuration = "Permanente / Indefinida"
        });
        Assert.Equal(AccessRestrictionOutcome.SancaoInformadaPeloUsuario, r1.Outcome);
        Assert.Equal("Sanção informada pelo usuário", r1.OutcomeTitle);

        // 2. Possível problema de conectividade
        var r2 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire
        {
            UsesVpnOrProxy = true
        });
        Assert.Equal(AccessRestrictionOutcome.PossivelProblemaConectividade, r2.Outcome);
        Assert.Equal("Possível problema de conectividade", r2.OutcomeTitle);

        // 3. Possível falha no anticheat
        var r3 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire
        {
            FortniteMessage = "Erro 30005 ao iniciar o Easy Anti-Cheat"
        });
        Assert.Equal(AccessRestrictionOutcome.PossivelFalhaAnticheat, r3.Outcome);
        Assert.Equal("Possível falha no anticheat", r3.OutcomeTitle);

        // 4. Problema local identificado
        var reportWithLocalError = new FullDiagnosticReport
        {
            Security = new WindowsSecurityResult { OverallStatus = DiagnosticStatus.CRITICO }
        };
        var r4 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire { CanAccessLobby = false }, reportWithLocalError);
        Assert.Equal(AccessRestrictionOutcome.ProblemaLocalIdentificado, r4.Outcome);
        Assert.Equal("Problema local identificado", r4.OutcomeTitle);

        // 5. Possível restrição de acesso não confirmada
        var r5 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire
        {
            CanAccessLobby = true,
            KickedDuringMatch = true,
            WindowsReinstalled = true,
            ComputerPurchasedFromThirdParty = true
        });
        Assert.Equal(AccessRestrictionOutcome.PossivelRestricaoAcessoNaoConfirmada, r5.Outcome);
        Assert.Equal("Possível restrição de acesso não confirmada", r5.OutcomeTitle);
        Assert.Contains("Somente a Epic Games", r5.MandatoryDisclaimer);

        // 6. Diagnóstico inconclusivo
        var r6 = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire());
        Assert.Equal(AccessRestrictionOutcome.DiagnosticoInconclusivo, r6.Outcome);
        Assert.Equal("Diagnóstico inconclusivo", r6.OutcomeTitle);
    }
}
