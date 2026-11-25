using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.Core.Diagnostics;

/// <summary>
/// Módulo de análise de possíveis restrições de acesso (Seção 12).
/// Classifica estritamente nos 6 resultados permitidos, sem nunca afirmar que componentes
/// estão banidos sem confirmação oficial e sem inventar porcentagens de banimento HWID.
/// </summary>
public static class AccessRestrictionAnalyzer
{
    public static AccessRestrictionAnalysisResult Analyze(
        AccessRestrictionQuestionnaire input,
        FullDiagnosticReport? currentReport = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var factors = new List<string>();
        var steps = new List<string>();

        bool hasLocalAnticheatFailure = currentReport?.AntiCheat.OverallStatus is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO;
        bool hasLocalNetworkIssue = currentReport?.Network.OverallStatus is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO ||
                                    (currentReport?.Network.HasVpnAdapters ?? false) ||
                                    (currentReport?.Network.IsProxyEnabled ?? false);
        bool hasLocalSystemOrDriverIssue = currentReport?.Security.OverallStatus is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO ||
                                           currentReport?.SystemIntegrity.OverallStatus is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO ||
                                           currentReport?.Fortnite.OverallStatus is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO;

        // 1. Se o usuário confirmou sanção no portal da Epic Games ou informou punição explícita confirmada
        if (input.ConfirmedSanctionInEpicPortal)
        {
            factors.Add("O usuário informou que existe uma sanção confirmada no Portal de Sanções e Recursos da Epic Games.");
            if (!string.IsNullOrWhiteSpace(input.PunishmentDuration) &&
                !input.PunishmentDuration.Contains("Nenhuma", StringComparison.OrdinalIgnoreCase))
            {
                factors.Add($"Duração da punição informada: {input.PunishmentDuration}.");
            }

            steps.Add("Consultar os detalhes e a possibilidade de apelação exclusivamente no Portal de Sanções e Recursos da Epic Games.");
            steps.Add("Aguardar o término do período caso seja uma suspensão temporária ou aguardar a resposta oficial do suporte.");
            steps.Add("Não utilizar ferramentas de terceiros que prometam modificar identificadores de hardware (spoofers).");

            return new AccessRestrictionAnalysisResult
            {
                Outcome = AccessRestrictionOutcome.SancaoInformadaPeloUsuario,
                Status = DiagnosticStatus.CRITICO,
                DetailedExplanation = "Foi identificada uma sanção confirmada pelo próprio usuário junto ao Portal de Sanções e Recursos da Epic Games. Nestes casos, a restrição é aplicada e gerenciada nos servidores oficiais da desenvolvedora.",
                ContributingFactors = factors,
                OfficialNextSteps = steps
            };
        }

        // 2. Se utiliza VPN ou Proxy (informado pelo usuário ou detectado localmente) ou problema de conectividade
        if (input.UsesVpnOrProxy || hasLocalNetworkIssue)
        {
            if (input.UsesVpnOrProxy)
                factors.Add("O usuário informou utilização de VPN ou proxy.");
            if (currentReport?.Network.HasVpnAdapters == true)
                factors.Add($"Adaptador virtual/VPN detectado localmente: {currentReport.Network.VpnPresenceDescription}.");
            if (currentReport?.Network.IsProxyEnabled == true)
                factors.Add($"Configuração de proxy ativa detectada: {currentReport.Network.ProxyConfiguration}.");
            if (currentReport?.Network.PacketLossPercent > 2.0)
                factors.Add($"Perda de pacotes elevada detectada: {currentReport.Network.PacketLossDisplay}.");

            steps.Add("Desativar completamente qualquer VPN, proxy ou rede virtual (Radmin, Hamachi, ZeroTier, Cloudflare WARP) antes de iniciar o Epic Games Launcher.");
            steps.Add("Sincronizar o relógio do Windows e verificar a estabilidade da conexão e resolução DNS.");
            steps.Add("Testar novamente o acesso à partida após reiniciar a pilha de rede.");

            return new AccessRestrictionAnalysisResult
            {
                Outcome = AccessRestrictionOutcome.PossivelProblemaConectividade,
                Status = DiagnosticStatus.ATENCAO,
                DetailedExplanation = "Foram identificados fatores de rede (como uso de VPN, proxy, adaptadores virtuais ou instabilidade de conexão) que frequentemente causam desconexão ou expulsão de partidas pelos sistemas anticheat sem significar banimento.",
                ContributingFactors = factors,
                OfficialNextSteps = steps
            };
        }

        // 3. Se há falha local confirmada nos componentes Anticheat (EAC / EOS / BattlEye)
        var msgLower = (input.FortniteMessage ?? string.Empty).ToLowerInvariant();
        if (hasLocalAnticheatFailure ||
            msgLower.Contains("easy anti-cheat") ||
            msgLower.Contains("eac") ||
            msgLower.Contains("battleye") ||
            msgLower.Contains("30005") ||
            msgLower.Contains("10011") ||
            msgLower.Contains("20006"))
        {
            if (hasLocalAnticheatFailure)
                factors.Add("O diagnóstico local encontrou falhas ou ausência em serviços/componentes do Easy Anti-Cheat ou BattlEye.");
            if (!string.IsNullOrWhiteSpace(input.FortniteMessage))
                factors.Add($"Mensagem reportada relacionada à inicialização/execução do anticheat: \"{input.FortniteMessage}\".");

            steps.Add("Executar a verificação oficial dos arquivos do Fortnite no Epic Games Launcher.");
            steps.Add("Executar a reparação oficial do Easy Anti-Cheat e verificar se o serviço BEService não está bloqueado.");
            steps.Add("Certificar-se de que o Modo de Teste (TestSigning) do Windows está desativado e o Windows está atualizado.");

            return new AccessRestrictionAnalysisResult
            {
                Outcome = AccessRestrictionOutcome.PossivelFalhaAnticheat,
                Status = DiagnosticStatus.ERRO,
                DetailedExplanation = "Os sintomas informados e/ou os testes locais apontam para uma falha técnica de inicialização ou comunicação dos componentes Easy Anti-Cheat ou BattlEye no computador.",
                ContributingFactors = factors,
                OfficialNextSteps = steps
            };
        }

        // 4. Se há problema local identificado no Windows, Segurança, Drivers ou Instalação do Fortnite
        if (hasLocalSystemOrDriverIssue)
        {
            factors.Add("Foram identificadas inconsistências técnicas locais nos módulos de Segurança do Windows, Integridade do Sistema ou Instalação do Fortnite.");
            if (!input.CanAccessLobby)
                factors.Add("O usuário relatou não conseguir acessar o lobby do jogo.");

            steps.Add("Consultar a Central de Soluções para corrigir os erros locais apontados nos módulos de Segurança, Drivers e Fortnite.");
            steps.Add("Verificar a integridade dos arquivos do Windows (SFC / DISM) e atualizar os drivers de vídeo e dependências Visual C++.");

            return new AccessRestrictionAnalysisResult
            {
                Outcome = AccessRestrictionOutcome.ProblemaLocalIdentificado,
                Status = DiagnosticStatus.ERRO,
                DetailedExplanation = "O diagnóstico local identificou falhas técnicas no ambiente do Windows ou na instalação do jogo que explicam erros de execução ou desconexão.",
                ContributingFactors = factors,
                OfficialNextSteps = steps
            };
        }

        // 5. Se consegue acessar o lobby, mas sofre expulsão sistemática durante a partida, mesmo sem VPN e com Windows reinstalado ou PC de terceiros
        if (input.CanAccessLobby && input.KickedDuringMatch &&
            (input.WindowsReinstalled || input.ComputerPurchasedFromThirdParty ||
             !input.PunishmentDuration.Contains("Nenhuma", StringComparison.OrdinalIgnoreCase) ||
             msgLower.Contains("removido da partida") || msgLower.Contains("removed from the match")))
        {
            factors.Add("O usuário consegue acessar o lobby, porém ocorre expulsão ao entrar ou durante a partida.");
            if (input.WindowsReinstalled)
                factors.Add("O problema persiste mesmo após a reinstalação do Windows.");
            if (input.ComputerPurchasedFromThirdParty)
                factors.Add("O computador ou componentes foram adquiridos de terceiros (usados).");
            if (!string.IsNullOrWhiteSpace(input.FortniteMessage))
                factors.Add($"Mensagem apresentada pelo jogo: \"{input.FortniteMessage}\".");

            steps.Add("Consultar imediatamente o Portal de Sanções e Recursos da Epic Games para verificar a existência de sanções registradas.");
            steps.Add("Abrir um chamado oficial no Suporte da Epic Games anexando o relatório técnico deste diagnóstico.");
            steps.Add("Verificar na BIOS se o TPM 2.0 e o Secure Boot estão devidamente habilitados, pois torneios e modos competitivos exigem esses recursos.");

            return new AccessRestrictionAnalysisResult
            {
                Outcome = AccessRestrictionOutcome.PossivelRestricaoAcessoNaoConfirmada,
                Status = DiagnosticStatus.ATENCAO,
                DetailedExplanation = "Os sintomas relatados (acesso normal ao lobby seguido de expulsão da partida mesmo sem uso de VPN e após reinstalação do sistema) podem ocorrer tanto por exigências de segurança (TPM/Secure Boot/Anticheat) quanto por uma possível restrição de acesso não confirmada localmente. Nenhuma ferramenta local pode afirmar que um componente específico está banido.",
                ContributingFactors = factors,
                OfficialNextSteps = steps
            };
        }

        // 6. Caso padrão: Diagnóstico inconclusivo
        factors.Add("As informações fornecidas não apontam de maneira definitiva para uma falha local específica nem possuem confirmação oficial de sanção.");
        steps.Add("Executar o Diagnóstico Completo para coletar todos os indicadores locais de hardware, rede, segurança e anticheat.");
        steps.Add("Verificar o Portal de Sanções e Recursos da Epic Games e a página oficial de status dos servidores.");

        return new AccessRestrictionAnalysisResult
        {
            Outcome = AccessRestrictionOutcome.DiagnosticoInconclusivo,
            Status = DiagnosticStatus.INCONCLUSIVO,
            DetailedExplanation = "Não existem evidências locais suficientes para determinar de forma conclusiva a origem do problema com base apenas nos dados atuais.",
            ContributingFactors = factors,
            OfficialNextSteps = steps
        };
    }
}
