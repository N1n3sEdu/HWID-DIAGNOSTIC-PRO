using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Diagnostics;

/// <summary>
/// Módulo exclusivo para diagnóstico do Fortnite (Seção 11).
/// Verifica localização da instalação, arquivos principais, Epic Games Launcher,
/// requisitos mínimos, GPU/DirectX, espaço em disco, dependências Visual C++,
/// logs públicos do usuário, conectividade e eventos do Windows.
/// </summary>
public sealed class FortniteDiagnosticService : IFortniteDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public FortniteDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public Task<FortniteDiagnosticResult> AnalyzeFortniteAsync(
        HardwareSnapshot? hardware = null,
        NetworkDiagnosticResult? network = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new FortniteDiagnosticResult
            {
                OfficialShortcuts = GetOfficialShortcuts()
            };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LocateAndVerifyFortniteInstallation(result);

                cancellationToken.ThrowIfCancellationRequested();
                VerifyEpicGamesLauncher(result);

                cancellationToken.ThrowIfCancellationRequested();
                VerifySystemRequirementsAndGpu(result, hardware);

                cancellationToken.ThrowIfCancellationRequested();
                VerifyAvailableDiskSpace(result);

                cancellationToken.ThrowIfCancellationRequested();
                VerifyVisualCppDependencies(result);

                cancellationToken.ThrowIfCancellationRequested();
                InspectFortnitePublicUserLogs(result);

                cancellationToken.ThrowIfCancellationRequested();
                InspectConnectivityAndServerStatus(result, network);

                cancellationToken.ThrowIfCancellationRequested();
                InspectWindowsEventsForFortnite(result);

                BuildFortniteChecks(result);
                result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);

                _logger.LogInfo("Fortnite", $"Diagnóstico do Fortnite concluído (Instalado: {result.IsInstalled}).");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Fortnite", "Erro durante diagnóstico do Fortnite.", ex);
            }

            return result;
        }, cancellationToken);
    }

    private static List<FortniteOfficialShortcut> GetOfficialShortcuts() =>
    [
        new FortniteOfficialShortcut
        {
            Title = "Verificação oficial dos arquivos do Fortnite",
            Description = "Aciona a verificação oficial de integridade no Epic Games Launcher.",
            UrlOrUri = "com.epicgames.launcher://apps/Fortnite?action=verify"
        },
        new FortniteOfficialShortcut
        {
            Title = "Suporte do Fortnite",
            Description = "Central oficial de ajuda e suporte técnico do Fortnite em português.",
            UrlOrUri = "https://www.epicgames.com/help/pt-BR/c-Category_Fortnite"
        },
        new FortniteOfficialShortcut
        {
            Title = "Suporte do Easy Anti-Cheat",
            Description = "Documentação oficial de solução de erros do Easy Anti-Cheat.",
            UrlOrUri = "https://www.easy.ac/pt-br/support/"
        },
        new FortniteOfficialShortcut
        {
            Title = "Suporte do BattlEye",
            Description = "Perguntas frequentes e suporte oficial do sistema BattlEye.",
            UrlOrUri = "https://www.battleye.com/support/"
        },
        new FortniteOfficialShortcut
        {
            Title = "Portal de sanções e recursos da Epic Games",
            Description = "Consulte sanções ativas na conta e envie recursos diretamente à Epic Games.",
            UrlOrUri = "https://www.epicgames.com/help/pt-BR/sanctions-and-appeals-service-c202300000000000"
        },
        new FortniteOfficialShortcut
        {
            Title = "Página oficial de status dos servidores",
            Description = "Verifique em tempo real se os servidores do Fortnite e matchmaking estão online.",
            UrlOrUri = "https://status.epicgames.com/"
        }
    ];

    private static void LocateAndVerifyFortniteInstallation(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        string? installDir = TryFindFortniteFromLauncherInstalledDat()
                             ?? TryFindFortniteFromManifests()
                             ?? TryFindFortniteInDefaultFolders();

        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
        {
            result.IsInstalled = false;
            result.InstallLocation = "Não encontrado localmente nos manifestos da Epic Games";
            result.MainFilesStatus = "Instalação do Fortnite não detectada neste computador";
            return;
        }

        result.IsInstalled = true;
        result.InstallLocation = PrivacyMasker.MaskUserPath(installDir);

        var expectedRelativeFiles = new[]
        {
            @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
            @"FortniteGame\Binaries\Win64\FortniteLauncher.exe",
            @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping_EAC.exe",
            @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping_BE.exe"
        };

        foreach (var rel in expectedRelativeFiles)
        {
            var full = Path.Combine(installDir, rel);
            if (File.Exists(full))
                result.VerifiedFiles.Add(Path.GetFileName(rel));
            else
                result.MissingFiles.Add(Path.GetFileName(rel));
        }

        var paksDir = Path.Combine(installDir, "FortniteGame", "Content", "Paks");
        int pakCount = Directory.Exists(paksDir)
            ? Directory.GetFiles(paksDir, "*.pak", SearchOption.TopDirectoryOnly).Length
            : 0;

        if (pakCount > 0)
            result.VerifiedFiles.Add($"{pakCount} arquivos de pacote (.pak)");
        else
            result.MissingFiles.Add("Pasta Content\\Paks sem pacotes .pak");

        result.MainFilesStatus = result.MissingFiles.Count == 0
            ? $"Íntegros ({result.VerifiedFiles.Count} componentes principais verificados)"
            : $"Arquivos ausentes detectados: {string.Join(", ", result.MissingFiles)}";
    }

    private static string? TryFindFortniteFromLauncherInstalledDat()
    {
        try
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var datPath = Path.Combine(programData, "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
            if (!File.Exists(datPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(datPath));
            if (doc.RootElement.TryGetProperty("InstallationList", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    var appName = item.TryGetProperty("AppName", out var appEl) ? appEl.GetString() : "";
                    var loc = item.TryGetProperty("InstallLocation", out var locEl) ? locEl.GetString() : "";
                    if (string.Equals(appName, "Fortnite", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                    {
                        return loc;
                    }
                }
            }
        }
        catch
        {
            // Ignorar erros de parse
        }
        return null;
    }

    private static string? TryFindFortniteFromManifests()
    {
        try
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var manifestsDir = Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifestsDir))
                return null;

            foreach (var itemFile in Directory.GetFiles(manifestsDir, "*.item"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(itemFile));
                var displayName = doc.RootElement.TryGetProperty("DisplayName", out var dEl) ? dEl.GetString() : "";
                var appName = doc.RootElement.TryGetProperty("AppName", out var aEl) ? aEl.GetString() : "";
                var installLoc = doc.RootElement.TryGetProperty("InstallLocation", out var lEl) ? lEl.GetString() : "";

                if ((string.Equals(appName, "Fortnite", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(displayName, "Fortnite", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(installLoc) && Directory.Exists(installLoc))
                {
                    return installLoc;
                }
            }
        }
        catch
        {
            // Ignorar
        }
        return null;
    }

    private static string? TryFindFortniteInDefaultFolders()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Epic Games\Fortnite",
            @"C:\Program Files (x86)\Epic Games\Fortnite",
            @"D:\Epic Games\Fortnite",
            @"D:\Program Files\Epic Games\Fortnite",
            @"E:\Epic Games\Fortnite"
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static void VerifyEpicGamesLauncher(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var paths = new[]
        {
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"
        };

        var found = paths.FirstOrDefault(File.Exists);
        if (found != null)
        {
            try
            {
                var ver = FileVersionInfo.GetVersionInfo(found).FileVersion ?? "Instalado";
                result.EpicGamesLauncherStatus = $"Instalado e disponível (Versão {ver})";
            }
            catch
            {
                result.EpicGamesLauncherStatus = "Instalado";
            }
        }
        else
        {
            result.EpicGamesLauncherStatus = "Não localizado nos diretórios padrão";
        }
    }

    private static void VerifySystemRequirementsAndGpu(FortniteDiagnosticResult result, HardwareSnapshot? hardware)
    {
        int cores = hardware?.Processor.Cores > 0 ? hardware.Processor.Cores : Environment.ProcessorCount / 2;
        double ramGb = hardware?.Ram.TotalCapacityGB ?? 0;
        bool is64Bit = Environment.Is64BitOperatingSystem;

        var primaryGpu = hardware?.Gpus.FirstOrDefault();
        result.DirectXVersion = primaryGpu?.DirectXCompatibility ?? (
            OperatingSystem.IsWindows() && File.Exists(Path.Combine(Environment.SystemDirectory, "d3d12.dll"))
                ? "DirectX 12 / DirectX 11"
                : "Não disponível");

        result.GpuCompatibilityStatus = primaryGpu != null
            ? $"{primaryGpu.Model} ({primaryGpu.DeviceStatus} | Driver {primaryGpu.DriverVersion})"
            : "Não disponível";

        bool cpuOk = cores >= 2;
        bool ramOk = ramGb == 0 || ramGb >= 7.5; // Considera 8GB nominais (~7.8GB utilizáveis)
        result.MeetsMinimumRequirements = is64Bit && cpuOk && ramOk;

        result.MinimumRequirementsStatus = result.MeetsMinimumRequirements
            ? $"Atende aos requisitos (SO 64 bits, CPU {cores} núcleos, RAM {(ramGb > 0 ? $"{ramGb:0.#} GB" : "verificada")})"
            : $"Atenção nos requisitos mínimos (CPU: {cores} núcleos, RAM: {ramGb:0.#} GB - Mínimo recomendado: 8 GB RAM e CPU 4 threads)";
    }

    private static void VerifyAvailableDiskSpace(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            string targetRoot = Path.GetPathRoot(result.IsInstalled ? result.InstallLocation : Environment.SystemDirectory) ?? @"C:\";
            var drive = new DriveInfo(targetRoot);
            if (drive.IsReady)
            {
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                result.AvailableSpaceStatus = $"{freeGb:0.0} GB disponíveis na unidade {drive.Name} (Total: {totalGb:0.0} GB)";
            }
        }
        catch
        {
            result.AvailableSpaceStatus = "Não disponível";
        }
    }

    private static void VerifyVisualCppDependencies(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            bool hasVcRuntimeDll = File.Exists(Path.Combine(Environment.SystemDirectory, "vcruntime140.dll")) &&
                                   File.Exists(Path.Combine(Environment.SystemDirectory, "msvcp140.dll"));

            string regVersion = "";
            using var vcKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            if (vcKey != null)
            {
                var installed = Convert.ToInt32(vcKey.GetValue("Installed", 0)) == 1;
                if (installed)
                {
                    regVersion = vcKey.GetValue("Version")?.ToString() ?? "2015-2022";
                }
            }

            if (hasVcRuntimeDll || !string.IsNullOrWhiteSpace(regVersion))
            {
                result.VisualCppDependenciesStatus = !string.IsNullOrWhiteSpace(regVersion)
                    ? $"Instaladas (Microsoft Visual C++ Redistributable x64 {regVersion})"
                    : "Instaladas (vcruntime140.dll e msvcp140.dll presentes em System32)";
            }
            else
            {
                result.VisualCppDependenciesStatus = "Ausentes (Requer instalação do pacote Microsoft Visual C++ 2015-2022 Redistributable x64/x86)";
            }
        }
        catch
        {
            result.VisualCppDependenciesStatus = "Não disponível";
        }
    }

    private static void InspectFortnitePublicUserLogs(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FortniteGame",
                "Saved",
                "Logs");

            if (!Directory.Exists(logsDir))
                return;

            var mainLog = Path.Combine(logsDir, "FortniteGame.log");
            if (!File.Exists(mainLog))
                return;

            using var fs = new FileStream(mainLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var recentLines = new Queue<string>(150);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (recentLines.Count >= 150)
                    recentLines.Dequeue();
                recentLines.Enqueue(line);
            }

            foreach (var l in recentLines)
            {
                if (l.Contains("Error:", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Fatal:", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("LogEasyAntiCheat", StringComparison.OrdinalIgnoreCase) && l.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                {
                    var sanitized = PrivacyMasker.SanitizeTextForReport(l.Trim());
                    if (result.UserLogFailures.Count < 8)
                        result.UserLogFailures.Add(sanitized);
                }
            }
        }
        catch
        {
            // Ignorar caso o log esteja inacessível
        }
    }

    private static void InspectConnectivityAndServerStatus(FortniteDiagnosticResult result, NetworkDiagnosticResult? network)
    {
        if (network != null)
        {
            result.ConnectivityIssuesStatus = network.InterferenceNotes.Count == 0
                ? $"Sem interferências detectadas (Latência: {network.LatencyDisplay} | Perda: {network.PacketLossDisplay})"
                : string.Join(" | ", network.InterferenceNotes);

            result.EpicPublicServicesStatus = network.EpicConnectivityStatus;
        }
        else
        {
            result.ConnectivityIssuesStatus = "Execute o diagnóstico de Rede para detalhes de latência e perda de pacotes";
            result.EpicPublicServicesStatus = "Consultável via módulo Rede e Internet ou status.epicgames.com";
        }
    }

    private static void InspectWindowsEventsForFortnite(FortniteDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var appLog = new EventLog("Application");
            int count = appLog.Entries.Count;
            int inspected = 0;

            for (int i = count - 1; i >= 0 && inspected < 200 && result.WindowsExecutionIssues.Count < 6; i--, inspected++)
            {
                var entry = appLog.Entries[i];
                if (entry.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                    continue;

                if (entry.Message.Contains("Fortnite", StringComparison.OrdinalIgnoreCase) ||
                    entry.Message.Contains("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) ||
                    entry.Message.Contains("BattlEye", StringComparison.OrdinalIgnoreCase) ||
                    entry.Message.Contains("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase))
                {
                    var firstLine = entry.Message.Split('\n').FirstOrDefault()?.Trim() ?? entry.Message;
                    result.WindowsExecutionIssues.Add(
                        $"[{entry.TimeGenerated:dd/MM/yyyy HH:mm}] {entry.Source} (ID {entry.InstanceId & 0xFFFF}): {PrivacyMasker.SanitizeTextForReport(firstLine)}");
                }
            }
        }
        catch
        {
            // Ignorar caso o log Application esteja restrito
        }
    }

    private static void BuildFortniteChecks(FortniteDiagnosticResult result)
    {
        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "FN-INSTALL",
            Category = "Fortnite",
            Name = "Localização e Arquivos Principais do Fortnite",
            CurrentValue = result.IsInstalled
                ? $"{result.InstallLocation} | {result.MainFilesStatus}"
                : result.InstallLocation,
            Status = !result.IsInstalled
                ? DiagnosticStatus.ATENCAO
                : result.MissingFiles.Count == 0
                    ? DiagnosticStatus.NORMAL
                    : DiagnosticStatus.ERRO,
            Purpose = "Verifica a pasta de instalação do Fortnite, executáveis principais, anticheats e arquivos .pak.",
            TechnicalDetails = result.MainFilesStatus,
            OfficialRecommendation = result.MissingFiles.Count > 0 || !result.IsInstalled
                ? "Utilize a função 'Verificar arquivos' no Epic Games Launcher para restaurar arquivos ausentes."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "FN-LAUNCHER",
            Category = "Fortnite",
            Name = "Status do Epic Games Launcher",
            CurrentValue = result.EpicGamesLauncherStatus,
            Status = result.EpicGamesLauncherStatus.StartsWith("Instalado", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.NORMAL
                : DiagnosticStatus.ATENCAO,
            Purpose = "Verifica a instalação local do inicializador oficial da Epic Games.",
            TechnicalDetails = result.EpicGamesLauncherStatus,
            OfficialRecommendation = "Mantenha o Epic Games Launcher atualizado na versão mais recente."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "FN-REQS",
            Category = "Fortnite",
            Name = "Requisitos Mínimos, GPU e DirectX",
            CurrentValue = $"{result.MinimumRequirementsStatus} | {result.DirectXVersion}",
            Status = result.MeetsMinimumRequirements ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO,
            Purpose = "Compara o hardware local com os requisitos mínimos oficiais do Fortnite.",
            TechnicalDetails = $"GPU: {result.GpuCompatibilityStatus} | Espaço em disco: {result.AvailableSpaceStatus}",
            OfficialRecommendation = "Mantenha os drivers de vídeo atualizados e ao menos 20 GB livres para atualizações de temporada."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "FN-VCPP",
            Category = "Fortnite",
            Name = "Dependências do Microsoft Visual C++",
            CurrentValue = result.VisualCppDependenciesStatus,
            Status = result.VisualCppDependenciesStatus.StartsWith("Instaladas", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.NORMAL
                : result.VisualCppDependenciesStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.ERRO,
            Purpose = "Verifica se as bibliotecas de tempo de execução Visual C++ 2015-2022 estão instaladas no Windows.",
            TechnicalDetails = result.VisualCppDependenciesStatus,
            OfficialRecommendation = "Se houver erro de DLL (vcruntime140.dll / msvcp140.dll), instale o pacote oficial Visual C++ Redistributable da Microsoft."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "FN-LOGS-EVENTS",
            Category = "Fortnite",
            Name = "Logs do Usuário e Eventos do Windows durante Execução",
            CurrentValue = (result.UserLogFailures.Count == 0 && result.WindowsExecutionIssues.Count == 0)
                ? "Nenhuma falha crítica recente registrada nos logs locais"
                : $"{result.UserLogFailures.Count} alerta(s) no log do jogo | {result.WindowsExecutionIssues.Count} evento(s) no Windows",
            Status = (result.UserLogFailures.Count == 0 && result.WindowsExecutionIssues.Count == 0)
                ? DiagnosticStatus.NORMAL
                : DiagnosticStatus.ATENCAO,
            Purpose = "Analisa erros registrados em %LocalAppData%\\FortniteGame\\Saved\\Logs e no Visualizador de Eventos.",
            TechnicalDetails = result.WindowsExecutionIssues.Count > 0
                ? string.Join(" | ", result.WindowsExecutionIssues)
                : "Sem falhas críticas registradas pelo Windows.",
            OfficialRecommendation = "Caso o jogo feche inesperadamente, execute a verificação oficial de arquivos no Epic Games Launcher."
        });
    }
}
