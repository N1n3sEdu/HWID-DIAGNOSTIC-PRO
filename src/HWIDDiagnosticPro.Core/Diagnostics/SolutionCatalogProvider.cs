using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.Core.Diagnostics;

/// <summary>
/// Central de Soluções (Seção 14) contendo recomendações legítimas e documentadas,
/// organizadas por problema encontrado.
/// </summary>
public static class SolutionCatalogProvider
{
    public static List<SolutionRecommendation> BuildSolutionsForReport(FullDiagnosticReport? report = null)
    {
        var catalog = GetOfficialCatalog();

        if (report == null)
            return catalog;

        foreach (var item in catalog)
        {
            item.IsTriggeredByScan = ShouldTrigger(item.Id, report, out var reason);
            if (item.IsTriggeredByScan && !string.IsNullOrWhiteSpace(reason))
            {
                item.TriggerConditionSummary = reason;
            }
        }

        return catalog
            .OrderByDescending(s => s.IsTriggeredByScan)
            .ThenBy(s => s.PriorityLevel switch
            {
                "Crítica" => 0,
                "Alta" => 1,
                "Média" => 2,
                _ => 3
            })
            .ToList();
    }

    private static bool ShouldTrigger(string solutionId, FullDiagnosticReport report, out string reason)
    {
        reason = "Recomendação preventiva oficial";

        switch (solutionId)
        {
            case "SOL-EAC-REPAIR":
                if (report.AntiCheat.EasyAntiCheat.Status != DiagnosticStatus.NORMAL ||
                    report.AntiCheat.EasyAntiCheatEos.Status != DiagnosticStatus.NORMAL)
                {
                    reason = "Detectado alerta ou ausência no serviço Easy Anti-Cheat / EOS.";
                    return true;
                }
                break;

            case "SOL-FORTNITE-VERIFY":
                if (report.Fortnite.OverallStatus != DiagnosticStatus.NORMAL)
                {
                    reason = "Detectada inconsistência ou ausência na verificação do Fortnite.";
                    return true;
                }
                break;

            case "SOL-GPU-DRIVER":
                if (report.Hardware.Gpus.Any(g => g.Status != DiagnosticStatus.NORMAL) ||
                    report.DriversAndServices.DevicesWithErrors.Count > 0)
                {
                    reason = "Detectado alerta no adaptador de vídeo ou dispositivo PnP com erro.";
                    return true;
                }
                break;

            case "SOL-WINDOWS-UPDATE":
                if (report.Security.RebootRequired ||
                    report.Security.OverallStatus != DiagnosticStatus.NORMAL)
                {
                    reason = "Detectada pendência de atualização, reinicialização ou recurso de segurança do Windows.";
                    return true;
                }
                break;

            case "SOL-DNS-FIX":
                if (!report.Network.DnsResolutionWorking || !report.Network.EpicServicesReachable)
                {
                    reason = "Detectada falha ou lentidão na resolução DNS / conectividade com serviços Epic Games.";
                    return true;
                }
                break;

            case "SOL-CLOCK-SYNC":
                if (!report.Network.TimeSyncHealthy)
                {
                    reason = "O serviço de sincronização de horário do Windows (W32Time) requer atenção.";
                    return true;
                }
                break;

            case "SOL-SFC-DISM":
                if (report.SystemIntegrity.OverallStatus != DiagnosticStatus.NORMAL)
                {
                    reason = "O módulo de Integridade do Windows registrou alertas ou eventos críticos.";
                    return true;
                }
                break;

            case "SOL-VPN-PROXY":
                if (report.Network.HasVpnAdapters || report.Network.IsProxyEnabled)
                {
                    reason = "Detectada presença de adaptador VPN ou configuração de Proxy ativa.";
                    return true;
                }
                break;

            case "SOL-TPM-SECUREBOOT":
                if (report.Security.Tpm20Status.Contains("Inativo", StringComparison.OrdinalIgnoreCase) ||
                    report.Security.Tpm20Status.Contains("Não", StringComparison.OrdinalIgnoreCase) ||
                    report.Security.SecureBootStatus.Contains("Desativado", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "TPM 2.0 ou Secure Boot não estão ativos ou disponíveis.";
                    return true;
                }
                break;

            case "SOL-EPIC-SANCTIONS":
            case "SOL-EPIC-SUPPORT":
                if (report.AccessRestrictionAnalysis.Outcome is AccessRestrictionOutcome.SancaoInformadaPeloUsuario
                    or AccessRestrictionOutcome.PossivelRestricaoAcessoNaoConfirmada)
                {
                    reason = "Recomendado com base na Análise de Restrições de Acesso.";
                    return true;
                }
                break;
        }

        return false;
    }

    public static List<SolutionRecommendation> GetOfficialCatalog() =>
    [
        new SolutionRecommendation
        {
            Id = "SOL-EAC-REPAIR",
            Category = "Anticheat",
            ProblemName = "Reparação oficial do Easy Anti-Cheat",
            Description = "Restaura os serviços e arquivos assinados do Easy Anti-Cheat e Easy Anti-Cheat EOS utilizados pelo Fortnite.",
            PossibleCause = "Corrupção do serviço EasyAntiCheat, bloqueio de inicialização (Erro 30005 / 10011 / 20006) ou atualização incompleta.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Feche o Fortnite e o Epic Games Launcher.\n2. Navegue até a pasta do Fortnite (FortniteGame\\Binaries\\Win64\\EasyAntiCheat).\n3. Execute 'EasyAntiCheat_EOS_Setup.exe' como Administrador ou utilize a opção 'Verificar arquivos' no Epic Games Launcher.\n4. Reinicie o computador antes de abrir o jogo.",
            InvolvedRisks = "Risco mínimo. Requer permissão de Administrador (UAC) para registrar o serviço oficial assinado pela Epic Games.",
            OfficialDocumentationUrl = "https://www.easy.ac/pt-br/support/"
        },
        new SolutionRecommendation
        {
            Id = "SOL-FORTNITE-VERIFY",
            Category = "Fortnite",
            ProblemName = "Verificação dos arquivos do Fortnite",
            Description = "Verifica a integridade de todos os pacotes (.pak) e executáveis do Fortnite através do manifesto oficial da Epic Games.",
            PossibleCause = "Arquivos corrompidos após atualização interrompida, desligamento abrupto ou falhas de leitura em disco.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Abra o Epic Games Launcher.\n2. Acesse a seção 'Biblioteca'.\n3. Clique nos três pontos (...) ao lado do Fortnite e selecione 'Gerenciar'.\n4. Clique em 'Verificar' ao lado de 'Verificar arquivos' e aguarde a conclusão.",
            InvolvedRisks = "Nenhum risco ao sistema. Pode baixar novamente arquivos que estejam ausentes ou modificados.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new SolutionRecommendation
        {
            Id = "SOL-GPU-DRIVER",
            Category = "Hardware e Drivers",
            ProblemName = "Atualização dos drivers de vídeo",
            Description = "Instala a versão WHQL mais recente e assinada digitalmente do driver da placa de vídeo (NVIDIA, AMD ou Intel).",
            PossibleCause = "Drivers desatualizados, falhas de DirectX 11/12 (DXGI_ERROR_DEVICE_REMOVED) ou instalação corrompida após atualização do Windows.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Identifique o modelo exato da placa de vídeo no módulo 'Placa de vídeo'.\n2. Baixe o driver WHQL oficial diretamente do site da NVIDIA, AMD ou Intel.\n3. Selecione a opção de 'Instalação Limpa' no instalador oficial e reinicie o computador.",
            InvolvedRisks = "A tela pode piscar brevemente durante a atualização do driver gráfico.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new SolutionRecommendation
        {
            Id = "SOL-WINDOWS-UPDATE",
            Category = "Segurança do Windows",
            ProblemName = "Atualização do Windows",
            Description = "Aplica atualizações cumulativas de segurança, certificados raiz e correções de kernel do Windows 10/11.",
            PossibleCause = "Compilação do Windows desatualizada, falta de suporte a certificados de assinatura digital exigidos pelos anticheats ou reinicialização pendente.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Abra Configurações do Windows > Windows Update.\n2. Clique em 'Verificar se há atualizações'.\n3. Instale todas as atualizações de qualidade e segurança pendentes.\n4. Reinicie o computador completamente.",
            InvolvedRisks = "O computador precisará ser reiniciado para concluir a aplicação das atualizações.",
            OfficialDocumentationUrl = "https://support.microsoft.com/pt-br/windows/windows-update"
        },
        new SolutionRecommendation
        {
            Id = "SOL-DNS-FIX",
            Category = "Rede e Internet",
            ProblemName = "Correção de falhas de DNS",
            Description = "Limpa o cache local de resolução de nomes e restaura a conectividade com os domínios de autenticação e matchmaking da Epic Games.",
            PossibleCause = "Cache DNS corrompido, servidor DNS da operadora instável ou bloqueio de rota.",
            PriorityLevel = "Média",
            RecommendedProcedure = "1. Abra o Prompt de Comando como Administrador.\n2. Execute 'ipconfig /flushdns'.\n3. Caso o DNS da operadora apresente falhas frequentes, configure servidores DNS públicos confiáveis nas propriedades IPv4 do adaptador de rede.",
            InvolvedRisks = "Nenhum risco. Limpa apenas o cache temporário de nomes de domínio.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new SolutionRecommendation
        {
            Id = "SOL-CLOCK-SYNC",
            Category = "Rede e Internet",
            ProblemName = "Sincronização do relógio",
            Description = "Ressincroniza a data e hora do sistema operacional com os servidores NTP oficiais.",
            PossibleCause = "Bateria CMOS fraca na placa-mãe ou serviço Horário do Windows (W32Time) parado, causando falhas de validação TLS/SSL e tokens OAuth.",
            PriorityLevel = "Média",
            RecommendedProcedure = "1. Abra Configurações do Windows > Hora e idioma > Data e hora.\n2. Ative 'Definir hora automaticamente' e 'Definir fuso horário automaticamente'.\n3. Em 'Configurações adicionais', clique em 'Sincronizar agora'.",
            InvolvedRisks = "Nenhum risco.",
            OfficialDocumentationUrl = "https://support.microsoft.com/pt-br/windows/como-definir-sua-hora-e-fuso-hor%C3%A1rio"
        },
        new SolutionRecommendation
        {
            Id = "SOL-SFC-DISM",
            Category = "Integridade do Sistema",
            ProblemName = "Verificação de arquivos corrompidos",
            Description = "Utiliza as ferramentas oficiais SFC (System File Checker) e DISM da Microsoft para diagnosticar e reparar arquivos protegidos do Windows.",
            PossibleCause = "Desligamentos inesperados, quedas de energia ou setores defeituosos em disco que corromperam DLLs do sistema ou da pilha de segurança.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Abra um Prompt de Comando como Administrador.\n2. Execute 'DISM.exe /Online /Cleanup-Image /RestoreHealth' e aguarde concluir.\n3. Em seguida, execute 'sfc /scannow'.\n4. Reinicie o computador após o término.",
            InvolvedRisks = "Exige privilégios de Administrador e conexão com o Windows Update caso o DISM precise baixar arquivos originais da Microsoft.",
            OfficialDocumentationUrl = "https://support.microsoft.com/pt-br/topic/use-a-ferramenta-verificador-de-arquivos-do-sistema-para-reparar-arquivos-de-sistema-ausentes-ou-corrompidos"
        },
        new SolutionRecommendation
        {
            Id = "SOL-VPN-PROXY",
            Category = "Rede e Internet",
            ProblemName = "Desativação de VPN, Proxy e Adaptadores Virtuais",
            Description = "Desativa túneis de VPN, proxies e placas de rede virtuais durante a execução de jogos protegidos por anticheat.",
            PossibleCause = "Sistemas anticheat e servidores do Fortnite bloqueiam conexões provenientes de IPs de VPNs comerciais ou proxies para evitar evasão e instabilidade.",
            PriorityLevel = "Crítica",
            RecommendedProcedure = "1. Feche e desconecte qualquer cliente de VPN (NordVPN, ProtonVPN, ExitLag, Cloudflare WARP, Radmin VPN, Hamachi, ZeroTier).\n2. Em Configurações do Windows > Rede e Internet > Proxy, desative 'Usar um servidor proxy'.\n3. Reinicie o Epic Games Launcher.",
            InvolvedRisks = "Se você utiliza VPN corporativa para trabalho, desconecte-a apenas enquanto estiver jogando.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new SolutionRecommendation
        {
            Id = "SOL-TPM-SECUREBOOT",
            Category = "Segurança do Windows",
            ProblemName = "Habilitação de TPM 2.0 e Secure Boot na BIOS/UEFI",
            Description = "Garante que o módulo de criptografia TPM 2.0 (fTPM / PTT) e a Inicialização Segura (Secure Boot) estejam ativos.",
            PossibleCause = "BIOS configurada em modo Legacy/CSM ou TPM/Secure Boot desativados após atualização ou reset de BIOS. Exigido em torneios oficiais do Fortnite.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Verifique no módulo 'BIOS e placa-mãe' se o sistema está em modo UEFI.\n2. Acesse a configuração UEFI/BIOS da placa-mãe ao iniciar o computador.\n3. Ative o Intel PTT / AMD fTPM (TPM 2.0) e ative o Secure Boot no modo Standard.\n4. Salve as alterações e inicie o Windows.",
            InvolvedRisks = "Não desative o modo CSM/Legacy sem antes confirmar que o disco do Windows está particionado em GPT (modo UEFI), para evitar falha de boot.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new SolutionRecommendation
        {
            Id = "SOL-EPIC-SANCTIONS",
            Category = "Suporte e Conta",
            ProblemName = "Consulta de sanções",
            Description = "Verifica oficialmente junto à Epic Games se existe alguma restrição ou penalidade ativa na conta do jogador.",
            PossibleCause = "Expulsão de partidas ou bloqueio de login sem causa técnica local aparente.",
            PriorityLevel = "Alta",
            RecommendedProcedure = "1. Acesse o Portal de Sanções e Recursos da Epic Games no navegador.\n2. Faça login diretamente no site oficial da Epic Games (este software nunca solicita suas credenciais).\n3. Verifique se há notificações de sanções ativas e, caso aplicável, utilize a opção de recurso oficial.",
            InvolvedRisks = "Nenhum risco desde que o acesso seja feito exclusivamente no domínio oficial epicgames.com.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/sanctions-and-appeals-service-c202300000000000"
        },
        new SolutionRecommendation
        {
            Id = "SOL-EPIC-SUPPORT",
            Category = "Suporte e Conta",
            ProblemName = "Abertura de chamado no suporte da Epic Games",
            Description = "Encaminha um relatório técnico detalhado e mascarado ao suporte oficial da Epic Games para análise de erros persistentes.",
            PossibleCause = "Problemas persistentes de expulsão de partida, falhas de autenticação ou erros de inicialização após concluir todas as correções locais.",
            PriorityLevel = "Média",
            RecommendedProcedure = "1. No módulo 'Relatórios' deste programa, clique em 'Exportar Relatório' ou 'Copiar Resumo para Suporte'.\n2. Acesse a página oficial de suporte da Epic Games.\n3. Descreva o problema detalhadamente e anexe o relatório técnico gerado (seus dados sensíveis já estarão mascarados automaticamente).",
            InvolvedRisks = "Nenhum risco.",
            OfficialDocumentationUrl = "https://www.epicgames.com/help/pt-BR/"
        }
    ];
}
