using HWIDDiagnosticPro.Core.Models;

namespace HWIDDiagnosticPro.Core.Diagnostics;

/// <summary>
/// Interpreta erros documentados do Fortnite, Easy Anti-Cheat, BattlEye e Epic Games Launcher (Seção 11).
/// Deixa claro quando não há diagnóstico conclusivo.
/// </summary>
public static class FortniteErrorInterpreter
{
    public static FortniteErrorInterpretation Interpret(string? manualErrorMessage)
    {
        if (string.IsNullOrWhiteSpace(manualErrorMessage))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = string.Empty,
                IdentifiedCodeOrPattern = "Nenhuma mensagem informada",
                Status = DiagnosticStatus.INCONCLUSIVO,
                InterpretationSummary = "Insira a mensagem ou código de erro exibido pelo Fortnite, Easy Anti-Cheat, BattlEye ou Epic Games Launcher para análise técnica.",
                PossibleCauses = ["Nenhum texto fornecido para análise."],
                OfficialRecommendations = ["Digite ou cole a mensagem exata recebida durante a partida ou inicialização."],
                IsConclusive = false,
                ConclusivenessNotice = "Diagnóstico inconclusivo: aguardando entrada do usuário."
            };
        }

        var msg = manualErrorMessage.Trim();
        var lower = msg.ToLowerInvariant();

        // 1. Mensagem clássica de expulsão de partida (VPN / IP / Lag / Máquina)
        if (lower.Contains("removido da partida") ||
            lower.Contains("removed from the match") ||
            lower.Contains("lag de internet") ||
            lower.Contains("uso de vpn") ||
            lower.Contains("seu ip ou máquina") ||
            lower.Contains("your ip or machine"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Expulsão de Partida (Conexão / VPN / Máquina / Anticheat)",
                Status = DiagnosticStatus.ATENCAO,
                InterpretationSummary = "Esta é uma mensagem genérica do Fortnite exibida quando a sessão da partida é interrompida pelo servidor ou pelo anticheat.",
                PossibleCauses =
                [
                    "Uso de VPN, proxy, adaptadores de rede virtuais (Radmin, Hamachi, WireGuard, Cloudflare WARP) ou túnel de rota.",
                    "Perda severa de pacotes, oscilação de rota ou dessincronização de horário do Windows.",
                    "Falha de comunicação ou serviço interrompido do Easy Anti-Cheat (EAC) ou BattlEye (BE) durante a partida.",
                    "Existência de restrição ativa na conta ou no equipamento registrada exclusivamente nos servidores da Epic Games."
                ],
                OfficialRecommendations =
                [
                    "Desative completamente qualquer VPN, proxy ou adaptador de rede virtual antes de abrir o Epic Games Launcher.",
                    "Verifique o Portal de Sanções e Recursos da Epic Games para confirmar se há alguma restrição oficial vinculada à conta.",
                    "Execute a verificação oficial dos arquivos do Fortnite pelo Epic Games Launcher e repare o Easy Anti-Cheat / BattlEye.",
                    "Verifique se o TPM 2.0 e o Secure Boot estão ativos na BIOS/UEFI."
                ],
                IsConclusive = false,
                ConclusivenessNotice = "DIAGNÓSTICO NÃO CONCLUSIVO: O Fortnite utiliza a mesma mensagem padrão para problemas de rede/VPN, falhas de anticheat e restrições de acesso. Somente a Epic Games pode confirmar se há uma sanção aplicada."
            };
        }

        // 2. Easy Anti-Cheat Error 30005 / CreateFile failed with 32
        if (lower.Contains("30005") || lower.Contains("createfile failed") || lower.Contains("30004"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Easy Anti-Cheat Erro 30005 / 30004 (Falha de Inicialização de Serviço/Driver)",
                Status = DiagnosticStatus.ERRO,
                InterpretationSummary = "O Easy Anti-Cheat não conseguiu criar ou acessar seu arquivo de driver/serviço no sistema operacional.",
                PossibleCauses =
                [
                    "Arquivo EasyAntiCheat.sys bloqueado por uma instância anterior ou corrompido.",
                    "Bloqueio por antivírus de terceiros ou conflito com Isolamento de Núcleo / drivers incompatíveis.",
                    "Corrupção no armazenamento ou arquivos de sistema do Windows."
                ],
                OfficialRecommendations =
                [
                    "Reinicie o computador para liberar bloqueios de arquivos do driver EAC.",
                    "Execute a verificação oficial dos arquivos do Fortnite no Epic Games Launcher.",
                    "Execute o reparo oficial do Easy Anti-Cheat na pasta de instalação do Fortnite.",
                    "Execute 'sfc /scannow' em um Prompt de Comando elevado caso o erro persista."
                ],
                IsConclusive = true,
                ConclusivenessNotice = "Diagnóstico técnico local identificado: falha documentada de inicialização do serviço Easy Anti-Cheat."
            };
        }

        // 3. Easy Anti-Cheat Error 10011 / 20006 / 10022
        if (lower.Contains("10011") || lower.Contains("20006") || lower.Contains("10022") || lower.Contains("startservice failed"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Easy Anti-Cheat Erro de Serviço (10011 / 20006 / 10022)",
                Status = DiagnosticStatus.ERRO,
                InterpretationSummary = "O serviço do Easy Anti-Cheat falhou ao iniciar através do Gerenciador de Controle de Serviços do Windows.",
                PossibleCauses =
                [
                    "Serviço EasyAntiCheat ou EasyAntiCheat_EOS desativado ou corrompido.",
                    "Modo de Assinatura de Teste (Test Signing) ou Modo de Depuração do Windows ativado.",
                    "Versão desatualizada do Windows 10/11 ou falta de atualizações de segurança (suporte a assinatura SHA-2)."
                ],
                OfficialRecommendations =
                [
                    "Verifique no módulo 'Segurança do Windows' se o Modo de Teste (TestSigning) está desativado.",
                    "Atualize o Windows pelo Windows Update e reinicie o computador.",
                    "Reinstale ou repare o Easy Anti-Cheat através do instalador oficial na pasta do Fortnite."
                ],
                IsConclusive = true,
                ConclusivenessNotice = "Diagnóstico técnico local identificado: falha ao iniciar o serviço do Easy Anti-Cheat."
            };
        }

        // 4. BattlEye Errors
        if (lower.Contains("battleye") || lower.Contains("beservice") || lower.Contains("client not responding") || lower.Contains("corrupted memory"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Falha do BattlEye (BEService / Timeout / Memória)",
                Status = DiagnosticStatus.ERRO,
                InterpretationSummary = "O componente BattlEye encontrou um erro de serviço, tempo limite de resposta ou integridade de memória.",
                PossibleCauses =
                [
                    "Serviço BEService bloqueado por firewall, antivírus de terceiros ou permissões insuficientes.",
                    "Alta perda de pacotes impedindo pacotes de heartbeat do BattlEye.",
                    "Módulos de memória RAM instáveis (XMP/DOCP instável) ou arquivos do sistema corrompidos."
                ],
                OfficialRecommendations =
                [
                    "Verifique os arquivos do Fortnite no Epic Games Launcher.",
                    "Certifique-se de que o serviço 'BEService' não está desativado no Windows.",
                    "Verifique a conexão de rede e evite conexões Wi-Fi instáveis ou VPNs."
                ],
                IsConclusive = false,
                ConclusivenessNotice = "Diagnóstico parcial: requer verificação do serviço BEService e estabilidade de rede/memória."
            };
        }

        // 5. DirectX / GPU / Driver Crash
        if (lower.Contains("d3d11") || lower.Contains("directx") || lower.Contains("dxgi_error") ||
            lower.Contains("video driver crashed") || lower.Contains("driver de vídeo falhou") || lower.Contains("out of video memory"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Falha Gráfica (DirectX / Driver de Vídeo / VRAM)",
                Status = DiagnosticStatus.ERRO,
                InterpretationSummary = "O motor gráfico Unreal Engine perdeu a comunicação com a placa de vídeo (TDR / falha de driver DirectX).",
                PossibleCauses =
                [
                    "Driver da placa de vídeo desatualizado, corrompido ou instável.",
                    "Overclock na placa de vídeo ou superaquecimento.",
                    "Incompatibilidade com DirectX 12 em hardware antigo."
                ],
                OfficialRecommendations =
                [
                    "Atualize o driver da placa de vídeo no site oficial do fabricante (NVIDIA, AMD ou Intel).",
                    "Remova qualquer overclock da GPU e teste o Fortnite em Modo Desempenho ou DirectX 11.",
                    "Feche aplicativos com overlays gráficos em segundo plano."
                ],
                IsConclusive = true,
                ConclusivenessNotice = "Diagnóstico técnico local identificado: erro de subsistema gráfico / driver de vídeo."
            };
        }

        // 6. Códigos de Rede / Epic Games Launcher (ESP-DIST, IS-0001, LS-0013, AS-0001, etc.)
        if (lower.Contains("esp-") || lower.Contains("is-") || lower.Contains("ls-") || lower.Contains("as-") ||
            lower.Contains("falha de login") || lower.Contains("unable to connect to fortnite servers"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Código de Conectividade / Inicialização Epic Games (ESP / IS / LS / AS)",
                Status = DiagnosticStatus.ATENCAO,
                InterpretationSummary = "Falha de comunicação com os servidores da Epic Games ou problema de cache de instalação do jogo.",
                PossibleCauses =
                [
                    "Falha na resolução DNS ou cache de rede corrompido.",
                    "Instabilidade temporária nos servidores públicos da Epic Games.",
                    "Cache do Epic Games Launcher corrompido ou relógio do Windows fora de sincronia."
                ],
                OfficialRecommendations =
                [
                    "Consulte o status oficial dos servidores em status.epicgames.com.",
                    "Sincronize a data e hora do Windows automaticamente.",
                    "Verifique a resolução DNS no módulo 'Rede e internet' e reinicie o roteador."
                ],
                IsConclusive = true,
                ConclusivenessNotice = "Diagnóstico técnico identificado: erro de conectividade ou inicialização do Epic Games Launcher."
            };
        }

        // 7. Arquivos corrompidos / Pak files
        if (lower.Contains("corrupt") || lower.Contains("corrompid") || lower.Contains(".pak") || lower.Contains("serialization error"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Corrupção de Arquivos (.pak / Dados do Jogo)",
                Status = DiagnosticStatus.ERRO,
                InterpretationSummary = "O Fortnite detectou arquivos de pacotes (.pak) ausentes ou corrompidos durante a leitura em disco.",
                PossibleCauses =
                [
                    "Download ou atualização interrompida no Epic Games Launcher.",
                    "Erros de leitura/gravação no SSD ou HDD (verifique o status SMART no módulo SSD e HDD).",
                    "Instabilidade de memória RAM corrompendo a descompactação de arquivos."
                ],
                OfficialRecommendations =
                [
                    "Abra o Epic Games Launcher > Biblioteca > Fortnite > Três pontos > Gerenciar > Verificar arquivos.",
                    "Confira a saúde SMART e o espaço livre do SSD/HDD no módulo 'SSD e HDD'."
                ],
                IsConclusive = true,
                ConclusivenessNotice = "Diagnóstico técnico local identificado: corrupção de arquivos locais do Fortnite."
            };
        }

        // 8. Banimento / Sanção explícita informada na mensagem
        if (lower.Contains("banido") || lower.Contains("banned") || lower.Contains("exploiting") || lower.Contains("explorando") || lower.Contains("trapaça"))
        {
            return new FortniteErrorInterpretation
            {
                UserMessage = msg,
                IdentifiedCodeOrPattern = "Mensagem Relacionada a Restrição ou Sanção",
                Status = DiagnosticStatus.CRITICO,
                InterpretationSummary = "A mensagem informada faz referência a uma restrição de acesso ou bloqueio de partida.",
                PossibleCauses =
                [
                    "Sanção ativa aplicada à conta ou ao ambiente pelos sistemas de segurança da Epic Games.",
                    "Uso de VPN/Proxy ou software incompatível com o Easy Anti-Cheat / BattlEye.",
                    "Computador adquirido de terceiros que já possuía restrição registrada anteriormente nos servidores do jogo."
                ],
                OfficialRecommendations =
                [
                    "Acesse o Portal de Sanções e Recursos da Epic Games para consultar o status oficial da sua conta.",
                    "Caso discorde de uma sanção, envie um recurso exclusivamente pelo canal oficial da Epic Games.",
                    "Evite qualquer programa que prometa alterar HWID ou contornar bloqueios, pois violam os Termos de Serviço e comprometem a segurança do Windows."
                ],
                IsConclusive = false,
                ConclusivenessNotice = "DIAGNÓSTICO NÃO CONCLUSIVO LOCALMENTE: Nenhuma ferramenta local pode confirmar ou revogar banimentos. Apenas o Portal de Sanções e Recursos da Epic Games possui informações oficiais."
            };
        }

        // 9. Caso padrão (não mapeado de forma específica)
        return new FortniteErrorInterpretation
        {
            UserMessage = msg,
            IdentifiedCodeOrPattern = "Mensagem Não Catalogada / Genérica",
            Status = DiagnosticStatus.INCONCLUSIVO,
            InterpretationSummary = "A mensagem inserida não corresponde a um código de erro único determinante no catálogo local.",
            PossibleCauses =
            [
                "Erro temporário de conexão ou atualização recente do Fortnite.",
                "Conflito local de driver, serviço ou dependência do Visual C++ / DirectX.",
                "Instabilidade temporária nos serviços online da Epic Games."
            ],
            OfficialRecommendations =
            [
                "Execute o Diagnóstico Completo neste software para verificar hardware, rede, segurança e anticheat.",
                "Realize a verificação oficial dos arquivos do Fortnite no Epic Games Launcher.",
                "Consulte a página oficial de suporte da Epic Games com a mensagem exata."
            ],
            IsConclusive = false,
            ConclusivenessNotice = "Diagnóstico inconclusivo: mensagem sem código determinante único."
        };
    }
}
