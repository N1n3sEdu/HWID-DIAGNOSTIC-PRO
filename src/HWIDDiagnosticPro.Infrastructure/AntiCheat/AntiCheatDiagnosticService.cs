using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.AntiCheat;

/// <summary>
/// Módulo de Diagnóstico Anticheat (Seção 10).
/// Verifica Easy Anti-Cheat, Easy Anti-Cheat EOS, BattlEye e Epic Games Launcher
/// utilizando exclusivamente inspeção de arquivos em disco, serviços do Windows e logs públicos,
/// sem nunca injetar código, acessar memória de processos ou interferir na execução.
/// </summary>
public sealed class AntiCheatDiagnosticService : IAntiCheatDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public AntiCheatDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public Task<AntiCheatDiagnosticResult> AnalyzeAntiCheatAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new AntiCheatDiagnosticResult();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.EasyAntiCheat = InspectComponent(
                    componentName: "Easy Anti-Cheat",
                    serviceName: "EasyAntiCheat",
                    candidatePaths:
                    [
                        @"C:\Program Files (x86)\EasyAntiCheat\EasyAntiCheat.exe",
                        @"C:\Program Files (x86)\EasyAntiCheat\EasyAntiCheat.sys"
                    ],
                    logDirectories:
                    [
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyAntiCheat")
                    ]);

                cancellationToken.ThrowIfCancellationRequested();
                result.EasyAntiCheatEos = InspectComponent(
                    componentName: "Easy Anti-Cheat EOS",
                    serviceName: "EasyAntiCheat_EOS",
                    candidatePaths:
                    [
                        @"C:\Program Files (x86)\EasyAntiCheat_EOS\EasyAntiCheat_EOS.exe",
                        @"C:\Program Files (x86)\EasyAntiCheat_EOS\EasyAntiCheat_EOS.sys"
                    ],
                    logDirectories:
                    [
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyAntiCheat")
                    ]);

                cancellationToken.ThrowIfCancellationRequested();
                result.BattlEye = InspectComponent(
                    componentName: "BattlEye",
                    serviceName: "BEService",
                    candidatePaths:
                    [
                        @"C:\Program Files (x86)\Common Files\BattlEye\BEService.exe",
                        @"C:\Program Files (x86)\Common Files\BattlEye\BEService_fn.exe"
                    ],
                    logDirectories: []);

                cancellationToken.ThrowIfCancellationRequested();
                result.EpicGamesLauncher = InspectComponent(
                    componentName: "Epic Games Launcher",
                    serviceName: "EpicOnlineServices",
                    candidatePaths:
                    [
                        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
                        @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"
                    ],
                    logDirectories:
                    [
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher", "Saved", "Logs")
                    ]);

                BuildChecks(result);
                result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);

                _logger.LogInfo("Anticheat", "Diagnóstico não-intrusivo dos componentes anticheat concluído.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Anticheat", "Erro durante verificação dos componentes anticheat.", ex);
            }

            return result;
        }, cancellationToken);
    }

    private static AntiCheatComponentStatus InspectComponent(
        string componentName,
        string serviceName,
        string[] candidatePaths,
        string[] logDirectories)
    {
        var comp = new AntiCheatComponentStatus
        {
            ComponentName = componentName,
            RegisteredServiceName = serviceName
        };

        if (!OperatingSystem.IsWindows())
        {
            comp.Status = DiagnosticStatus.INDISPONIVEL;
            return comp;
        }

        string? foundBinaryPath = candidatePaths.FirstOrDefault(File.Exists);

        // Verificar serviço registrado e caminho do executável via Registro/ServiceController
        try
        {
            using var svcKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (svcKey != null)
            {
                var imgPath = svcKey.GetValue("ImagePath")?.ToString()?.Trim('"') ?? "";
                if (string.IsNullOrWhiteSpace(foundBinaryPath) && !string.IsNullOrWhiteSpace(imgPath))
                {
                    var cleanedPath = ExtractExecutablePath(imgPath);
                    if (File.Exists(cleanedPath))
                        foundBinaryPath = cleanedPath;
                }
            }

            using var sc = new ServiceController(serviceName);
            var scStatus = sc.Status;
            var startType = sc.StartType;

            comp.RegisteredServiceStatus = scStatus switch
            {
                ServiceControllerStatus.Running => $"Em execução (Modo: {startType})",
                ServiceControllerStatus.Stopped => startType == ServiceStartMode.Disabled
                    ? "Parado e DESATIVADO (Impede inicialização do jogo)"
                    : $"Registrado e Pronto (Sob demanda / {startType})",
                _ => $"{scStatus} ({startType})"
            };

            if (startType == ServiceStartMode.Disabled)
            {
                comp.StartupFailures = $"O serviço '{serviceName}' está configurado como Desativado no Windows.";
                comp.RequiresOfficialRepairOrReinstall = true;
                comp.RepairRecommendation = $"Altere o tipo de inicialização do serviço '{serviceName}' para Manual ou execute o reparo oficial.";
            }
        }
        catch
        {
            comp.RegisteredServiceStatus = "Serviço não registrado no momento";
        }

        if (!string.IsNullOrWhiteSpace(foundBinaryPath) && File.Exists(foundBinaryPath))
        {
            comp.IsInstalled = true;
            comp.InstallationPath = PrivacyMasker.MaskUserPath(foundBinaryPath);

            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(foundBinaryPath);
                comp.LocalVersion = !string.IsNullOrWhiteSpace(fvi.FileVersion)
                    ? fvi.FileVersion
                    : !string.IsNullOrWhiteSpace(fvi.ProductVersion)
                        ? fvi.ProductVersion
                        : "Instalado (Versão não especificada no cabeçalho PE)";
            }
            catch
            {
                comp.LocalVersion = "Não disponível";
            }

            comp.ExecutableDigitalSignature = VerifyAuthenticodeCertificate(foundBinaryPath);
        }
        else
        {
            comp.IsInstalled = !comp.RegisteredServiceStatus.StartsWith("Serviço não registrado", StringComparison.OrdinalIgnoreCase);
            comp.InstallationPath = comp.IsInstalled ? "Registrado no sistema" : "Não encontrado nos caminhos padrão";
        }

        // Inspecionar logs públicos em modo somente-leitura
        foreach (var logDir in logDirectories)
        {
            if (!Directory.Exists(logDir))
                continue;

            try
            {
                var logFiles = Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Take(3);

                foreach (var lf in logFiles)
                {
                    ReadRecentPublicLogErrors(lf, comp.PublicLogErrors);
                }
            }
            catch
            {
                // Ignorar pastas de log bloqueadas
            }
        }

        // Verificar incompatibilidades documentadas (ex: TestSigning no Registro)
        try
        {
            using var controlKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control");
            var startOpts = (controlKey?.GetValue("SystemStartOptions")?.ToString() ?? "").ToUpperInvariant();
            if (startOpts.Contains("TESTSIGNING") || startOpts.Contains("DEBUG"))
            {
                comp.DocumentedIncompatibilities = $"Parâmetro de boot incompatível detectado ({startOpts.Trim()}).";
                comp.RequiresOfficialRepairOrReinstall = true;
            }
        }
        catch
        {
            // Ignorar
        }

        if (comp.RequiresOfficialRepairOrReinstall)
        {
            comp.Status = DiagnosticStatus.ERRO;
            if (comp.RepairRecommendation.Equals("Nenhuma ação necessária."))
                comp.RepairRecommendation = $"Execute a verificação de arquivos do jogo ou reinstale oficialmente o {componentName}.";
        }
        else if (comp.IsInstalled)
        {
            comp.Status = comp.PublicLogErrors.Count > 0 ? DiagnosticStatus.ATENCAO : DiagnosticStatus.NORMAL;
            comp.RepairRecommendation = comp.PublicLogErrors.Count > 0
                ? "Foram encontrados avisos recentes nos logs públicos. Se ocorrer erro ao abrir o jogo, execute a verificação oficial no Epic Games Launcher."
                : "Componente instalado e pronto para inicialização sob demanda.";
        }
        else
        {
            comp.Status = DiagnosticStatus.ATENCAO;
            comp.InstallationProblems = $"Componente '{componentName}' não localizado nos diretórios padrão.";
            comp.RequiresOfficialRepairOrReinstall = true;
            comp.RepairRecommendation = $"Inicie o jogo pelo Epic Games Launcher ou execute o instalador oficial do {componentName}.";
        }

        return comp;
    }

    private static string ExtractExecutablePath(string rawImagePath)
    {
        if (string.IsNullOrWhiteSpace(rawImagePath))
            return string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(rawImagePath.Trim());
        if (expanded.StartsWith('"'))
        {
            int endQuote = expanded.IndexOf('"', 1);
            if (endQuote > 1)
                return expanded[1..endQuote];
        }

        int exeIdx = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
            return expanded[..(exeIdx + 4)];

        int sysIdx = expanded.IndexOf(".sys", StringComparison.OrdinalIgnoreCase);
        if (sysIdx > 0)
            return expanded[..(sysIdx + 4)];

        return expanded;
    }

    private static string VerifyAuthenticodeCertificate(string filePath)
    {
        try
        {
            #pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            #pragma warning restore SYSLIB0057
            var subject = cert.GetNameInfo(X509NameType.SimpleName, false);
            return !string.IsNullOrWhiteSpace(subject)
                ? $"Assinado digitalmente ({subject})"
                : "Assinatura digital Authenticode presente";
        }
        catch
        {
            return "Assinatura via catálogo do Windows ou não verificável diretamente";
        }
    }

    private static void ReadRecentPublicLogErrors(string filePath, List<string> targetErrors)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var lines = new Queue<string>(40);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (lines.Count >= 40)
                    lines.Dequeue();
                lines.Enqueue(line);
            }

            foreach (var l in lines)
            {
                if ((l.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                     l.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                     l.Contains("Exception", StringComparison.OrdinalIgnoreCase)) &&
                    targetErrors.Count < 6)
                {
                    targetErrors.Add(PrivacyMasker.SanitizeTextForReport(l.Trim()));
                }
            }
        }
        catch
        {
            // Ignorar arquivos de log inacessíveis
        }
    }

    private static void BuildChecks(AntiCheatDiagnosticResult result)
    {
        foreach (var comp in result.AllComponents)
        {
            result.Checks.Add(new DiagnosticCheckItem
            {
                Id = $"AC-{comp.RegisteredServiceName}",
                Category = "Anticheat",
                Name = comp.ComponentName,
                CurrentValue = $"{comp.InstallationStatus} | Serviço: {comp.RegisteredServiceStatus} | Versão: {comp.LocalVersion}",
                Status = comp.Status,
                Purpose = $"Verifica instalação, serviço registrado, assinatura digital e logs públicos do {comp.ComponentName}.",
                TechnicalDetails = $"Caminho: {comp.InstallationPath} | Assinatura: {comp.ExecutableDigitalSignature} | Incompatibilidades: {comp.DocumentedIncompatibilities}",
                OfficialRecommendation = comp.RepairRecommendation
            });
        }
    }
}
