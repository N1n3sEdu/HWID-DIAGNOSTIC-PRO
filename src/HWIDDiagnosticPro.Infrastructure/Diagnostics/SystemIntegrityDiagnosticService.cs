using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Diagnostics;

/// <summary>
/// Módulo de Integridade do Windows e Logs de Eventos (Seção 13).
/// Utiliza mecanismos oficiais de diagnóstico (SFC /verifyonly, DISM /CheckHealth, Visualizador de Eventos
/// e verificações de armazenamento) sem executar reparações automáticas sem confirmação explícita.
/// </summary>
public sealed class SystemIntegrityDiagnosticService : ISystemIntegrityDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public SystemIntegrityDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public async Task<SystemIntegrityResult> AnalyzeSystemIntegrityAsync(
        bool runOfficialVerificationTools = false,
        CancellationToken cancellationToken = default)
    {
        var result = new SystemIntegrityResult
        {
            IsRunningElevated = IsCurrentProcessElevated()
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectSystemFilesAndDependencies(result);

            cancellationToken.ThrowIfCancellationRequested();
            InspectComponentStoreAndCbsLogs(result);

            cancellationToken.ThrowIfCancellationRequested();
            InspectEventLogs(result);

            if (runOfficialVerificationTools && OperatingSystem.IsWindows())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunReadOnlyDismAndSfcChecksAsync(result, cancellationToken);
            }
            else
            {
                result.DismCheckHealthSummary = result.IsRunningElevated
                    ? "Disponível para execução sob demanda no módulo Integridade do Sistema"
                    : "Requer execução como Administrador para executar DISM /CheckHealth e SFC /verifyonly";
                result.SfcVerifySummary = result.DismCheckHealthSummary;
            }

            BuildIntegrityChecks(result);
            result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);

            _logger.LogInfo("Integridade", $"Diagnóstico de integridade concluído ({result.RelevantEvents.Count} eventos relevantes coletados).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Integridade", "Erro durante verificação de integridade do Windows.", ex);
        }

        return result;
    }

    private static bool IsCurrentProcessElevated()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void InspectSystemFilesAndDependencies(SystemIntegrityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var sys32 = Environment.SystemDirectory;
        var criticalFiles = new[]
        {
            "ntoskrnl.exe",
            "kernel32.dll",
            "ntdll.dll",
            "d3d11.dll",
            "d3d12.dll",
            "dxgi.dll",
            "vcruntime140.dll",
            "msvcp140.dll",
            "ws2_32.dll",
            "winTRUST.dll",
            "crypt32.dll"
        };

        var missing = criticalFiles.Where(f => !File.Exists(Path.Combine(sys32, f))).ToList();
        if (missing.Count == 0)
        {
            result.SystemFilesIntegrityStatus = "Arquivos nucleares do sistema e bibliotecas DirectX/C++ presentes em System32";
            result.MissingDependenciesStatus = "Nenhuma dependência essencial ausente (DirectX, WinTrust, Crypt32 e VC++ presentes)";
        }
        else
        {
            result.SystemFilesIntegrityStatus = $"Alerta: arquivos ausentes em System32 ({string.Join(", ", missing)})";
            result.MissingDependenciesStatus = $"Dependências ausentes detectadas: {string.Join(", ", missing)}";
        }
    }

    private static void InspectComponentStoreAndCbsLogs(SystemIntegrityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing");
            var rebootPending = cbsKey?.OpenSubKey("RebootPending") != null;

            result.WindowsComponentCorruptionStatus = rebootPending
                ? "Reparo ou atualização CBS aguardando reinicialização do Windows"
                : "Repositório de componentes (Component Store) sem indicador de corrupção no Registro";
        }
        catch
        {
            result.WindowsComponentCorruptionStatus = "Não disponível";
        }
    }

    private static void InspectEventLogs(SystemIntegrityResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        int storageErrors = 0;
        int updateFailures = 0;
        int criticalErrors = 0;
        int driverEvents = 0;
        int bootIssues = 0;

        ReadLogEntries("System", result.RelevantEvents, ref storageErrors, ref updateFailures, ref criticalErrors, ref driverEvents, ref bootIssues);
        ReadLogEntries("Application", result.RelevantEvents, ref storageErrors, ref updateFailures, ref criticalErrors, ref driverEvents, ref bootIssues);

        result.StorageErrorsStatus = storageErrors == 0
            ? "Nenhum erro recente de disco/NTFS registrado no Visualizador de Eventos"
            : $"{storageErrors} evento(s) de armazenamento/disco registrado(s) recentemente";

        result.UpdateFailuresStatus = updateFailures == 0
            ? "Nenhuma falha recente do WindowsUpdateClient registrada"
            : $"{updateFailures} falha(s) recente(s) do Windows Update detectada(s)";

        result.CriticalSystemErrorsStatus = criticalErrors == 0
            ? "Nenhum desligamento crítico (Kernel-Power 41 / BugCheck) nos registros recentes analisados"
            : $"{criticalErrors} evento(s) crítico(s) do sistema (Kernel-Power / WER / WHEA) encontrado(s)";

        result.DriverRelatedEventsStatus = driverEvents == 0
            ? "Nenhum erro crítico de carregamento de driver nos eventos recentes"
            : $"{driverEvents} evento(s) relacionado(s) a drivers ou serviços de kernel";

        result.BootIssuesStatus = bootIssues == 0
            ? "Inicialização do sistema sem falhas críticas reportadas"
            : $"{bootIssues} alerta(s) de inicialização ou serviço essencial detectado(s)";
    }

    private static void ReadLogEntries(
        string logName,
        List<SystemEventLogEntry> targetList,
        ref int storageErrors,
        ref int updateFailures,
        ref int criticalErrors,
        ref int driverEvents,
        ref int bootIssues)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var log = new EventLog(logName);
            int total = log.Entries.Count;
            int inspected = 0;
            var cutoff = DateTime.Now.AddDays(-14);

            for (int i = total - 1; i >= 0 && inspected < 300 && targetList.Count < 60; i--, inspected++)
            {
                var entry = log.Entries[i];
                if (entry.TimeGenerated < cutoff)
                    break;

                if (entry.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                    continue;

                int eventId = (int)(entry.InstanceId & 0xFFFF);
                string src = entry.Source ?? string.Empty;
                string category = "Sistema";

                if (src.Contains("Disk", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Ntfs", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("storahci", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("stornvme", StringComparison.OrdinalIgnoreCase))
                {
                    storageErrors++;
                    category = "Armazenamento";
                }
                else if (src.Contains("WindowsUpdateClient", StringComparison.OrdinalIgnoreCase))
                {
                    updateFailures++;
                    category = "Windows Update";
                }
                else if (src.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) ||
                         src.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) ||
                         src.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase) ||
                         src.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase))
                {
                    criticalErrors++;
                    category = "Erro Crítico";
                }
                else if (src.Contains("Kernel-PnP", StringComparison.OrdinalIgnoreCase) ||
                         src.Contains("DriverFrameworks", StringComparison.OrdinalIgnoreCase) ||
                         (src.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase) && eventId is 7000 or 7026))
                {
                    driverEvents++;
                    category = "Driver / Kernel";
                }
                else if (src.Contains("Kernel-Boot", StringComparison.OrdinalIgnoreCase) ||
                         src.Contains("Wininit", StringComparison.OrdinalIgnoreCase))
                {
                    bootIssues++;
                    category = "Inicialização";
                }
                else if (entry.Message.Contains("Fortnite", StringComparison.OrdinalIgnoreCase) ||
                         entry.Message.Contains("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) ||
                         entry.Message.Contains("BattlEye", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Jogo / Anticheat";
                }
                else if (entry.EntryType != EventLogEntryType.Error)
                {
                    continue;
                }

                var firstLine = entry.Message.Split('\n').FirstOrDefault()?.Trim() ?? entry.Message;
                targetList.Add(new SystemEventLogEntry
                {
                    Timestamp = entry.TimeGenerated,
                    LogName = logName,
                    Source = src,
                    EventId = eventId,
                    Severity = entry.EntryType == EventLogEntryType.Error ? "Erro" : "Alerta",
                    Category = category,
                    Message = PrivacyMasker.SanitizeTextForReport(firstLine),
                    Status = entry.EntryType == EventLogEntryType.Error ? DiagnosticStatus.ERRO : DiagnosticStatus.ATENCAO
                });
            }
        }
        catch
        {
            // Ignorar logs restritos
        }
    }

    private async Task RunReadOnlyDismAndSfcChecksAsync(SystemIntegrityResult result, CancellationToken cancellationToken)
    {
        if (!result.IsRunningElevated)
        {
            result.DismCheckHealthSummary = "Não executado: requer privilégios de Administrador.";
            result.SfcVerifySummary = "Não executado: requer privilégios de Administrador.";
            return;
        }

        // 1. DISM /Online /Cleanup-Image /CheckHealth (Rápido e estritamente somente leitura)
        var dismOutput = await ExecuteCommandWithTimeoutAsync(
            "dism.exe",
            "/Online /Cleanup-Image /CheckHealth",
            TimeSpan.FromSeconds(15),
            cancellationToken);

        result.DismCheckHealthSummary = string.IsNullOrWhiteSpace(dismOutput)
            ? "DISM CheckHealth concluído."
            : PrivacyMasker.SanitizeTextForReport(dismOutput);

        // 2. SFC /verifyonly (Modo de verificação somente leitura, sem alterar arquivos)
        var sfcOutput = await ExecuteCommandWithTimeoutAsync(
            "sfc.exe",
            "/verifyonly",
            TimeSpan.FromSeconds(25),
            cancellationToken);

        result.SfcVerifySummary = string.IsNullOrWhiteSpace(sfcOutput)
            ? "SFC /verifyonly executado."
            : PrivacyMasker.SanitizeTextForReport(sfcOutput);
    }

    private async Task<string> ExecuteCommandWithTimeoutAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return "Não foi possível iniciar o utilitário oficial.";

            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var lines = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(4);

            return string.Join(" | ", lines);
        }
        catch (OperationCanceledException)
        {
            return "Verificação oficial interrompida por limite de tempo (timeout).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Integridade", $"Não foi possível executar {fileName}: {ex.Message}");
            return $"Indisponível ({ex.Message})";
        }
    }

    private static void BuildIntegrityChecks(SystemIntegrityResult result)
    {
        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "INT-SYSFILES",
            Category = "Integridade do Sistema",
            Name = "Integridade dos Arquivos do Sistema e Dependências",
            CurrentValue = result.SystemFilesIntegrityStatus,
            Status = result.SystemFilesIntegrityStatus.StartsWith("Alerta", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.ERRO
                : result.SystemFilesIntegrityStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.NORMAL,
            Purpose = "Verifica a presença de arquivos essenciais do kernel, DirectX, criptografia e bibliotecas Visual C++.",
            TechnicalDetails = $"{result.MissingDependenciesStatus} | SFC: {result.SfcVerifySummary}",
            OfficialRecommendation = "Execute 'sfc /scannow' em um Prompt de Comando como Administrador caso haja arquivos ausentes.",
            RequiresElevation = true
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "INT-DISM-CBS",
            Category = "Integridade do Sistema",
            Name = "Repositório de Componentes do Windows (DISM / CBS)",
            CurrentValue = result.WindowsComponentCorruptionStatus,
            Status = result.WindowsComponentCorruptionStatus.Contains("aguardando reinicialização", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.ATENCAO
                : result.WindowsComponentCorruptionStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.NORMAL,
            Purpose = "Verifica se há corrupção registrada no Component-Based Servicing (CBS) do Windows.",
            TechnicalDetails = $"DISM CheckHealth: {result.DismCheckHealthSummary}",
            OfficialRecommendation = "Se houver corrupção de componentes, execute 'DISM /Online /Cleanup-Image /RestoreHealth' como Administrador.",
            RequiresElevation = true
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "INT-STORAGE-EVENTS",
            Category = "Integridade do Sistema",
            Name = "Diagnóstico de Erros de Armazenamento e NTFS",
            CurrentValue = result.StorageErrorsStatus,
            Status = result.StorageErrorsStatus.StartsWith("Nenhum", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.NORMAL
                : result.StorageErrorsStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.ERRO,
            Purpose = "Consulta o Visualizador de Eventos por falhas de controlador de disco (Disk, NTFS, StorNVMe, StorAHCI).",
            TechnicalDetails = result.StorageErrorsStatus,
            OfficialRecommendation = "Se houver erros de disco, verifique os cabos SATA/encaixe M.2 e a saúde SMART do SSD/HDD."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "INT-CRITICAL-EVENTS",
            Category = "Integridade do Sistema",
            Name = "Erros Críticos, Drivers e Inicialização no Visualizador de Eventos",
            CurrentValue = $"{result.CriticalSystemErrorsStatus} | {result.DriverRelatedEventsStatus}",
            Status = result.CriticalSystemErrorsStatus.StartsWith("Nenhum", StringComparison.OrdinalIgnoreCase) &&
                     result.DriverRelatedEventsStatus.StartsWith("Nenhum", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.NORMAL
                : result.CriticalSystemErrorsStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.ATENCAO,
            Purpose = "Identifica eventos Kernel-Power 41, BugCheck (Tela Azul), WHEA-Logger e falhas de carregamento de drivers.",
            TechnicalDetails = $"Atualizações: {result.UpdateFailuresStatus} | Inicialização: {result.BootIssuesStatus}",
            OfficialRecommendation = "Analise a aba 'Logs e eventos' para detalhes sobre cada evento registrado pelo Windows."
        });
    }
}
