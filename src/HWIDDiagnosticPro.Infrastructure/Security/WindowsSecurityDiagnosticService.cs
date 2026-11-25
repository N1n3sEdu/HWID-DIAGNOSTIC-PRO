using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;
using HWIDDiagnosticPro.Infrastructure.Helpers;

namespace HWIDDiagnosticPro.Infrastructure.Security;

/// <summary>
/// Módulo de Segurança do Windows (Seção 8).
/// Verifica todos os recursos de segurança exigidos por anticheats modernos (TPM 2.0, Secure Boot,
/// Defender, Firewall, Integridade de Memória, Isolamento de Núcleo, Windows Update, Assinatura de Drivers,
/// UAC, Inicialização, Proteções, Compilação e Reinicialização Pendente).
/// Nunca desativa automaticamente nenhuma proteção.
/// </summary>
public sealed class WindowsSecurityDiagnosticService : IWindowsSecurityDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public WindowsSecurityDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public Task<WindowsSecurityResult> AnalyzeSecurityAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new WindowsSecurityResult();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tpmStatus = CheckTpm20(result);

                cancellationToken.ThrowIfCancellationRequested();
                var sbStatus = CheckSecureBoot(result);

                cancellationToken.ThrowIfCancellationRequested();
                var defStatus = CheckMicrosoftDefender(result);

                cancellationToken.ThrowIfCancellationRequested();
                var fwStatus = CheckFirewall(result);

                cancellationToken.ThrowIfCancellationRequested();
                var (hvciStatus, vbsStatus) = CheckCoreIsolationAndMemoryIntegrity(result);

                cancellationToken.ThrowIfCancellationRequested();
                var wuStatus = CheckWindowsUpdate(result);

                cancellationToken.ThrowIfCancellationRequested();
                var (dseStatus, bootStatus) = CheckBootAndDriverSignature(result);

                cancellationToken.ThrowIfCancellationRequested();
                var uacStatus = CheckUac(result);

                cancellationToken.ThrowIfCancellationRequested();
                var protStatus = CheckAvailableProtections(result);

                cancellationToken.ThrowIfCancellationRequested();
                var winVerStatus = CheckWindowsVersionAndBuild(result);

                cancellationToken.ThrowIfCancellationRequested();
                var rebootStatus = CheckPendingReboot(result);

                AddSecurityFeature(
                    result,
                    "SEC-TPM",
                    "TPM 2.0 (Trusted Platform Module)",
                    result.Tpm20Status,
                    tpmStatus,
                    "Fornece raiz de confiança criptográfica em hardware exigida pelo Windows 11 e por torneios competitivos do Fortnite.",
                    tpmStatus == DiagnosticStatus.NORMAL
                        ? "Mantenha o TPM 2.0 (Intel PTT / AMD fTPM) ativo na BIOS/UEFI."
                        : "Habilite o Intel PTT ou AMD fTPM (TPM 2.0) nas configurações de segurança da BIOS/UEFI.");

                AddSecurityFeature(
                    result,
                    "SEC-SECUREBOOT",
                    "Secure Boot (Inicialização Segura UEFI)",
                    result.SecureBootStatus,
                    sbStatus,
                    "Garante que apenas bootloaders e drivers de kernel assinados por autoridades confiáveis sejam carregados antes do Windows.",
                    sbStatus == DiagnosticStatus.NORMAL
                        ? "Nenhuma ação necessária."
                        : "Ative o Secure Boot em modo UEFI na BIOS da placa-mãe.");

                AddSecurityFeature(
                    result,
                    "SEC-DEFENDER",
                    "Microsoft Defender Antivírus",
                    result.MicrosoftDefenderStatus,
                    defStatus,
                    "Protege o sistema em tempo real contra ameaças sem causar incompatibilidades com o Easy Anti-Cheat e BattlEye.",
                    "Mantenha a proteção em tempo real e a proteção contra violações (Tamper Protection) ativadas.");

                AddSecurityFeature(
                    result,
                    "SEC-FIREWALL",
                    "Firewall do Windows Defender",
                    result.FirewallStatus,
                    fwStatus,
                    "Filtra conexões de rede de entrada e saída nos perfis de Domínio, Privado e Público.",
                    "Mantenha o Firewall do Windows ativo em todos os perfis de rede.");

                AddSecurityFeature(
                    result,
                    "SEC-HVCI",
                    "Integridade da Memória (HVCI)",
                    result.MemoryIntegrityStatus,
                    hvciStatus,
                    "Impede a injeção de código malicioso ou não assinado em processos de alta segurança no kernel do Windows.",
                    "Pode ser gerenciado em Segurança do Windows > Segurança do Dispositivo > Isolamento de Núcleo.");

                AddSecurityFeature(
                    result,
                    "SEC-VBS",
                    "Isolamento de Núcleo (VBS)",
                    result.CoreIsolationStatus,
                    vbsStatus,
                    "Utiliza segurança baseada em virtualização para isolar áreas críticas de memória do sistema operacional.",
                    "Mantenha configurado conforme a compatibilidade dos seus drivers WHQL.");

                AddSecurityFeature(
                    result,
                    "SEC-WINUPDATE",
                    "Windows Update",
                    result.WindowsUpdateStatus,
                    wuStatus,
                    "Mantém certificados digitais raiz, pacotes de serviço e correções de kernel atualizados.",
                    "Instale regularmente as atualizações de qualidade e segurança do Windows.");

                AddSecurityFeature(
                    result,
                    "SEC-DRIVERSIG",
                    "Assinatura Digital dos Drivers (Code Integrity)",
                    result.DriverSignatureEnforcementStatus,
                    dseStatus,
                    "Sistemas anticheat recusam a inicialização do jogo caso a verificação de assinatura de drivers esteja desativada (TestSigning).",
                    dseStatus == DiagnosticStatus.NORMAL
                        ? "Imposição de assinatura digital ativa e íntegra."
                        : "Execute 'bcdedit /set testsigning off' e 'bcdedit /set nointegritychecks off' como Administrador e reinicie.");

                AddSecurityFeature(
                    result,
                    "SEC-UAC",
                    "Controle de Conta de Usuário (UAC)",
                    result.UacStatus,
                    uacStatus,
                    "Solicita confirmação antes que aplicativos realizem alterações administrativas ou instalem serviços.",
                    "Mantenha o UAC ativado no nível padrão do Windows.");

                AddSecurityFeature(
                    result,
                    "SEC-BOOTCFG",
                    "Configurações de Inicialização",
                    result.BootConfigurations,
                    bootStatus,
                    "Verifica parâmetros de boot como Modo de Depuração (Debug), Modo de Segurança e Inicialização Rápida.",
                    bootStatus == DiagnosticStatus.NORMAL
                        ? "Parâmetros de inicialização padrão."
                        : "Desative o modo de depuração do kernel ('bcdedit /debug off') para jogar títulos com anticheat.");

                AddSecurityFeature(
                    result,
                    "SEC-PROTECTIONS",
                    "Proteções de Segurança Disponíveis",
                    result.AvailableSecurityProtections,
                    protStatus,
                    "Lista proteções nativas de hardware e sistema como DEP (Prevenção de Execução de Dados) e SmartScreen.",
                    "Mantenha a Prevenção de Execução de Dados (DEP) ativa.");

                AddSecurityFeature(
                    result,
                    "SEC-WINVER",
                    "Versão e Compilação do Windows",
                    result.WindowsVersionAndBuild,
                    winVerStatus,
                    "Verifica se a edição e compilação do Windows 10/11 são suportadas oficialmente pela Epic Games.",
                    "Utilize Windows 10 (64 bits versão 22H2) ou Windows 11 (64 bits atualizado).");

                AddSecurityFeature(
                    result,
                    "SEC-REBOOT",
                    "Necessidade de Reinicialização",
                    result.RebootRequiredDisplay,
                    rebootStatus,
                    "Verifica se existem atualizações de componentes (CBS/Windows Update) ou renomeações de drivers aguardando reboot.",
                    result.RebootRequired
                        ? "Reinicie o computador antes de iniciar o Fortnite para concluir a instalação de atualizações pendentes."
                        : "Nenhuma reinicialização pendente.");

                result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);
                _logger.LogInfo("Segurança", "Verificações de segurança do Windows concluídas.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Segurança", "Erro durante análise de segurança do Windows.", ex);
            }

            return result;
        }, cancellationToken);
    }

    private static void AddSecurityFeature(
        WindowsSecurityResult result,
        string id,
        string name,
        string state,
        DiagnosticStatus status,
        string purpose,
        string recommendation)
    {
        result.SecurityFeatures.Add(new SecurityFeatureItem
        {
            Name = name,
            CurrentState = state,
            Status = status,
            PurposeExplanation = purpose,
            OfficialRecommendation = recommendation
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = id,
            Category = "Segurança do Windows",
            Name = name,
            CurrentValue = state,
            Status = status,
            Purpose = purpose,
            TechnicalDetails = state,
            OfficialRecommendation = recommendation
        });
    }

    private static DiagnosticStatus CheckTpm20(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            var tpmRows = WmiQueryHelper.Query(
                "SELECT IsActivated_InitialValue, IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm",
                @"\\.\root\CIMV2\Security\MicrosoftTpm",
                TimeSpan.FromSeconds(4));

            if (tpmRows.Count > 0)
            {
                var row = tpmRows[0];
                var enabled = WmiQueryHelper.GetNullableBool(row, "IsEnabled_InitialValue") ?? true;
                var activated = WmiQueryHelper.GetNullableBool(row, "IsActivated_InitialValue") ?? true;
                var spec = WmiQueryHelper.GetString(row, "SpecVersion", "2.0");

                if (enabled && activated && spec.StartsWith("2.", StringComparison.OrdinalIgnoreCase))
                {
                    result.Tpm20Status = $"Ativo e Pronto (Especificação TPM {spec})";
                    return DiagnosticStatus.NORMAL;
                }

                result.Tpm20Status = $"Detectado (Versão: {spec} | Habilitado: {enabled} | Ativado: {activated})";
                return DiagnosticStatus.ATENCAO;
            }

            // Fallback via serviço TPM no Registro (quando executado sem privilégio de Administrador)
            using var tpmServiceKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM");
            if (tpmServiceKey != null)
            {
                var startVal = Convert.ToInt32(tpmServiceKey.GetValue("Start", 4));
                if (startVal is 0 or 1 or 2 or 3)
                {
                    result.Tpm20Status = "Driver TPM registrado no Windows (Execute como Administrador para consultar versão detalhada WMI)";
                    return DiagnosticStatus.NORMAL;
                }
            }

            result.Tpm20Status = "Não detectado ou desativado na BIOS";
            return DiagnosticStatus.ATENCAO;
        }
        catch
        {
            result.Tpm20Status = "Não disponível (Requer privilégio administrativo)";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static DiagnosticStatus CheckSecureBoot(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var val = key?.GetValue("UEFISecureBootEnabled");
            if (val != null)
            {
                bool enabled = Convert.ToInt32(val) == 1;
                result.SecureBootStatus = enabled ? "Ativado (Modo UEFI Seguro)" : "Desativado na BIOS/UEFI";
                return enabled ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO;
            }

            result.SecureBootStatus = "Não suportado ou BIOS em modo Legacy/CSM";
            return DiagnosticStatus.ATENCAO;
        }
        catch
        {
            result.SecureBootStatus = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static DiagnosticStatus CheckMicrosoftDefender(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            var mpRows = WmiQueryHelper.Query(
                "SELECT AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureVersion FROM MSFT_MpComputerStatus",
                @"\\.\root\Microsoft\Windows\Defender",
                TimeSpan.FromSeconds(4));

            if (mpRows.Count > 0)
            {
                var row = mpRows[0];
                var avEnabled = WmiQueryHelper.GetNullableBool(row, "AntivirusEnabled") ?? false;
                var rtpEnabled = WmiQueryHelper.GetNullableBool(row, "RealTimeProtectionEnabled") ?? false;
                var sigVer = WmiQueryHelper.GetString(row, "AntivirusSignatureVersion", "");

                if (avEnabled && rtpEnabled)
                {
                    result.MicrosoftDefenderStatus = $"Ativo (Proteção em tempo real habilitada | Assinatura {sigVer})";
                    return DiagnosticStatus.NORMAL;
                }

                result.MicrosoftDefenderStatus = avEnabled
                    ? "Ativo (Proteção em tempo real desativada ou gerenciada por outro antivírus)"
                    : "Desativado (Ou substituído por antivírus de terceiros)";
                return DiagnosticStatus.ATENCAO;
            }

            using var sc = new ServiceController("WinDefend");
            result.MicrosoftDefenderStatus = sc.Status == ServiceControllerStatus.Running
                ? "Serviço Microsoft Defender em execução"
                : $"Serviço Microsoft Defender: {sc.Status}";
            return sc.Status == ServiceControllerStatus.Running ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO;
        }
        catch
        {
            result.MicrosoftDefenderStatus = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static DiagnosticStatus CheckFirewall(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            using var stdKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            using var pubKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");
            using var domKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile");

            bool std = Convert.ToInt32(stdKey?.GetValue("EnableFirewall", 0)) == 1;
            bool pub = Convert.ToInt32(pubKey?.GetValue("EnableFirewall", 0)) == 1;
            bool dom = Convert.ToInt32(domKey?.GetValue("EnableFirewall", 0)) == 1;

            if (std && pub)
            {
                result.FirewallStatus = $"Ativo (Domínio: {(dom ? "Sim" : "Não")}, Privado: Sim, Público: Sim)";
                return DiagnosticStatus.NORMAL;
            }

            result.FirewallStatus = $"Atenção (Domínio: {(dom ? "Ativo" : "Inativo")}, Privado: {(std ? "Ativo" : "Inativo")}, Público: {(pub ? "Ativo" : "Inativo")})";
            return DiagnosticStatus.ATENCAO;
        }
        catch
        {
            result.FirewallStatus = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static (DiagnosticStatus HvciStatus, DiagnosticStatus VbsStatus) CheckCoreIsolationAndMemoryIntegrity(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return (DiagnosticStatus.INDISPONIVEL, DiagnosticStatus.INDISPONIVEL);

        DiagnosticStatus hvciDiag = DiagnosticStatus.INDISPONIVEL;
        DiagnosticStatus vbsDiag = DiagnosticStatus.INDISPONIVEL;

        try
        {
            using var hvciKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            if (hvciKey != null)
            {
                var enabled = Convert.ToInt32(hvciKey.GetValue("Enabled", 0)) == 1;
                result.MemoryIntegrityStatus = enabled ? "Ativada (HVCI Habilitado)" : "Desativada";
                hvciDiag = DiagnosticStatus.NORMAL;
            }
            else
            {
                result.MemoryIntegrityStatus = "Padrão do sistema (Chave HVCI não configurada explicitamente)";
                hvciDiag = DiagnosticStatus.NORMAL;
            }

            using var dgKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard");
            if (dgKey != null)
            {
                var vbs = Convert.ToInt32(dgKey.GetValue("EnableVirtualizationBasedSecurity", 0)) == 1;
                result.CoreIsolationStatus = vbs ? "Isolamento de Núcleo (VBS) Ativo" : "Isolamento de Núcleo (VBS) Desativado";
                vbsDiag = DiagnosticStatus.NORMAL;
            }
            else
            {
                result.CoreIsolationStatus = "Configuração padrão do Windows";
                vbsDiag = DiagnosticStatus.NORMAL;
            }
        }
        catch
        {
            result.MemoryIntegrityStatus = "Não disponível";
            result.CoreIsolationStatus = "Não disponível";
        }

        return (hvciDiag, vbsDiag);
    }

    private static DiagnosticStatus CheckWindowsUpdate(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            var qfeRows = WmiQueryHelper.Query("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering");
            var latestHotfix = qfeRows.LastOrDefault();
            var hotfixId = latestHotfix != null ? WmiQueryHelper.GetString(latestHotfix, "HotFixID", "") : "";

            using var sc = new ServiceController("wuauserv");
            bool isDisabled = sc.StartType == ServiceStartMode.Disabled;

            if (isDisabled)
            {
                result.WindowsUpdateStatus = "Serviço Windows Update (wuauserv) está Desativado";
                return DiagnosticStatus.ATENCAO;
            }

            result.WindowsUpdateStatus = !string.IsNullOrWhiteSpace(hotfixId)
                ? $"Operacional ({qfeRows.Count} atualizações registradas | Última: {hotfixId})"
                : $"Serviço disponível ({sc.Status})";
            return DiagnosticStatus.NORMAL;
        }
        catch
        {
            result.WindowsUpdateStatus = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static (DiagnosticStatus DseStatus, DiagnosticStatus BootStatus) CheckBootAndDriverSignature(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return (DiagnosticStatus.INDISPONIVEL, DiagnosticStatus.INDISPONIVEL);

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control");
            var startOptions = (key?.GetValue("SystemStartOptions")?.ToString() ?? string.Empty).ToUpperInvariant();

            bool testSigning = startOptions.Contains("TESTSIGNING");
            bool noIntegrity = startOptions.Contains("DISABLE_INTEGRITY_CHECKS") || startOptions.Contains("NOINTEGRITYCHECKS");
            bool kernelDebug = startOptions.Contains("DEBUG");

            if (testSigning || noIntegrity)
            {
                result.DriverSignatureEnforcementStatus =
                    "CRÍTICO: Modo de Teste (TestSigning) ou Desativação de Integridade detectado! Sistemas anticheat bloquearão a inicialização do jogo.";
            }
            else
            {
                result.DriverSignatureEnforcementStatus = "Ativa (Imposição obrigatória de assinatura digital de drivers habilitada)";
            }

            if (kernelDebug)
            {
                result.BootConfigurations = $"ALERTA: Depuração de Kernel ativa ({startOptions}). Incompatível com Easy Anti-Cheat e BattlEye.";
            }
            else
            {
                result.BootConfigurations = string.IsNullOrWhiteSpace(startOptions)
                    ? "Inicialização Normal Segura (Sem parâmetros de depuração)"
                    : $"Inicialização Normal (Opções: {startOptions.Trim()})";
            }

            var dseDiag = (testSigning || noIntegrity) ? DiagnosticStatus.CRITICO : DiagnosticStatus.NORMAL;
            var bootDiag = kernelDebug ? DiagnosticStatus.CRITICO : DiagnosticStatus.NORMAL;
            return (dseDiag, bootDiag);
        }
        catch
        {
            result.DriverSignatureEnforcementStatus = "Não disponível";
            result.BootConfigurations = "Não disponível";
            return (DiagnosticStatus.INDISPONIVEL, DiagnosticStatus.INDISPONIVEL);
        }
    }

    private static DiagnosticStatus CheckUac(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            if (key != null)
            {
                var enableLua = Convert.ToInt32(key.GetValue("EnableLUA", 1)) == 1;
                var promptBehavior = Convert.ToInt32(key.GetValue("ConsentPromptBehaviorAdmin", 5));

                if (enableLua && promptBehavior > 0)
                {
                    result.UacStatus = "Ativado (Proteção padrão de elevação de privilégios)";
                    return DiagnosticStatus.NORMAL;
                }

                result.UacStatus = "Desativado ou sem confirmação de administrador";
                return DiagnosticStatus.ATENCAO;
            }
        }
        catch
        {
            // Fallback
        }

        result.UacStatus = "Não disponível";
        return DiagnosticStatus.INDISPONIVEL;
    }

    private static DiagnosticStatus CheckAvailableProtections(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            var protections = new List<string>();
            var osRows = WmiQueryHelper.Query("SELECT DataExecutionPrevention_Available, DataExecutionPrevention_SupportPolicy FROM Win32_OperatingSystem");
            if (osRows.Count > 0)
            {
                var depAvail = WmiQueryHelper.GetNullableBool(osRows[0], "DataExecutionPrevention_Available") ?? false;
                if (depAvail)
                    protections.Add("DEP (Prevenção de Execução de Dados em Hardware)");
            }

            using var explorerKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
            var smartScreen = explorerKey?.GetValue("SmartScreenEnabled")?.ToString();
            if (!string.IsNullOrWhiteSpace(smartScreen) && !smartScreen.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                protections.Add($"SmartScreen ({smartScreen})");
            }

            if (Environment.Is64BitOperatingSystem)
                protections.Add("Kernel Patch Protection (PatchGuard x64)");

            result.AvailableSecurityProtections = protections.Count > 0
                ? string.Join(" | ", protections)
                : "Proteções padrão x64 do Windows";
            return DiagnosticStatus.NORMAL;
        }
        catch
        {
            result.AvailableSecurityProtections = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }

    private static DiagnosticStatus CheckWindowsVersionAndBuild(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var productName = key.GetValue("ProductName")?.ToString() ?? "Windows";
                var displayVer = key.GetValue("DisplayVersion")?.ToString() ?? "";
                var buildStr = key.GetValue("CurrentBuildNumber")?.ToString() ?? "0";
                var ubr = key.GetValue("UBR")?.ToString() ?? "0";

                int.TryParse(buildStr, out int buildNum);
                if (buildNum >= 22000 && productName.Contains("Windows 10"))
                    productName = productName.Replace("Windows 10", "Windows 11");

                result.WindowsVersionAndBuild = $"{productName} {displayVer} (Compilação {buildStr}.{ubr} - 64 bits)";
                return buildNum >= 19041 ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO;
            }
        }
        catch
        {
            // Fallback
        }

        result.WindowsVersionAndBuild = "Não disponível";
        return DiagnosticStatus.INDISPONIVEL;
    }

    private static DiagnosticStatus CheckPendingReboot(WindowsSecurityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return DiagnosticStatus.INDISPONIVEL;

        try
        {
            var reasons = new List<string>();

            using (var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
            {
                if (cbsKey != null)
                    reasons.Add("Atualização de componentes do Windows (CBS)");
            }

            using (var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
            {
                if (wuKey != null)
                    reasons.Add("Windows Update aguardando reinicialização");
            }

            result.RebootRequired = reasons.Count > 0;
            result.RebootRequiredDisplay = result.RebootRequired
                ? $"Sim ({string.Join(", ", reasons)})"
                : "Não (Sistema atualizado sem pendência de reinicialização)";

            return result.RebootRequired ? DiagnosticStatus.ATENCAO : DiagnosticStatus.NORMAL;
        }
        catch
        {
            result.RebootRequiredDisplay = "Não disponível";
            return DiagnosticStatus.INDISPONIVEL;
        }
    }
}
