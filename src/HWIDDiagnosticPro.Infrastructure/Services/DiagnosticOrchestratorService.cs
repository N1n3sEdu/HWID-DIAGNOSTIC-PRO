using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Services;

/// <summary>
/// Orquestrador assíncrono de diagnósticos (Seção 19).
/// Executa todos os módulos sem bloquear a interface gráfica, reportando progresso real,
/// respeitando cancelamento e persistindo o resultado localmente em JSON.
/// </summary>
public sealed class DiagnosticOrchestratorService : IDiagnosticOrchestratorService
{
    private readonly IHardwareDiagnosticService _hardwareService;
    private readonly INetworkDiagnosticService _networkService;
    private readonly IWindowsSecurityDiagnosticService _securityService;
    private readonly IDriversAndServicesDiagnosticService _driversService;
    private readonly IAntiCheatDiagnosticService _antiCheatService;
    private readonly IFortniteDiagnosticService _fortniteService;
    private readonly ISystemIntegrityDiagnosticService _integrityService;
    private readonly ISettingsStorageService _settingsStorage;
    private readonly ILocalLoggerService _logger;

    public DiagnosticOrchestratorService(
        IHardwareDiagnosticService hardwareService,
        INetworkDiagnosticService networkService,
        IWindowsSecurityDiagnosticService securityService,
        IDriversAndServicesDiagnosticService driversService,
        IAntiCheatDiagnosticService antiCheatService,
        IFortniteDiagnosticService fortniteService,
        ISystemIntegrityDiagnosticService integrityService,
        ISettingsStorageService settingsStorage,
        ILocalLoggerService logger)
    {
        _hardwareService = hardwareService;
        _networkService = networkService;
        _securityService = securityService;
        _driversService = driversService;
        _antiCheatService = antiCheatService;
        _fortniteService = fortniteService;
        _integrityService = integrityService;
        _settingsStorage = settingsStorage;
        _logger = logger;
    }

    public async Task<FullDiagnosticReport> RunFullDiagnosticAsync(
        IProgress<DiagnosticProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Orquestrador", "Iniciando diagnóstico completo do sistema...");
        var settings = await _settingsStorage.LoadSettingsAsync(cancellationToken);

        var report = new FullDiagnosticReport
        {
            GeneratedAt = DateTime.Now,
            ScanMode = "Diagnóstico Completo"
        };

        ReportProgress(progress, 8, "Hardware", "Consultando placa-mãe, BIOS, processador, RAM, discos e GPU...");
        report.Hardware = await _hardwareService.AnalyzeHardwareAsync(cancellationToken);

        ReportProgress(progress, 24, "Rede e Internet", "Analisando adaptadores ativos, DNS, proxy, VPN, latência e serviços Epic Games...");
        report.Network = await _networkService.AnalyzeNetworkAsync(settings.EnableOnlineEpicStatusCheck, cancellationToken);

        ReportProgress(progress, 40, "Segurança do Windows", "Verificando TPM 2.0, Secure Boot, Defender, Firewall, HVCI e UAC...");
        report.Security = await _securityService.AnalyzeSecurityAsync(cancellationToken);

        ReportProgress(progress, 56, "Drivers e Serviços", "Inventariando drivers instalados, assinaturas digitais e serviços...");
        report.DriversAndServices = await _driversService.AnalyzeDriversAndServicesAsync(cancellationToken);

        ReportProgress(progress, 70, "Anticheat", "Verificando Easy Anti-Cheat, Easy Anti-Cheat EOS, BattlEye e Epic Games Launcher...");
        report.AntiCheat = await _antiCheatService.AnalyzeAntiCheatAsync(cancellationToken);

        ReportProgress(progress, 84, "Fortnite", "Analisando instalação do Fortnite, requisitos, Visual C++ e logs locais...");
        report.Fortnite = await _fortniteService.AnalyzeFortniteAsync(report.Hardware, report.Network, cancellationToken);

        ReportProgress(progress, 94, "Integridade do Sistema", "Consultando integridade dos arquivos do Windows e Visualizador de Eventos...");
        report.SystemIntegrity = await _integrityService.AnalyzeSystemIntegrityAsync(runOfficialVerificationTools: false, cancellationToken);

        FinalizeReport(report);

        settings.LastDiagnosticTimestamp = report.GeneratedAt;
        await _settingsStorage.SaveSettingsAsync(settings, cancellationToken);
        if (settings.SaveLocalDiagnosticHistory)
        {
            await _settingsStorage.SaveLastReportAsync(report, cancellationToken);
        }

        ReportProgress(progress, 100, "Concluído", $"Diagnóstico completo finalizado ({report.CompletedChecksCount} verificações realizadas).");
        _logger.LogInfo("Orquestrador", $"Diagnóstico completo concluído. Pontuação técnica local: {report.SystemHealthScore}/100.");

        return report;
    }

    public async Task<FullDiagnosticReport> RunQuickDiagnosticAsync(
        IProgress<DiagnosticProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Orquestrador", "Iniciando diagnóstico rápido...");
        var settings = await _settingsStorage.LoadSettingsAsync(cancellationToken);

        var report = new FullDiagnosticReport
        {
            GeneratedAt = DateTime.Now,
            ScanMode = "Diagnóstico Rápido"
        };

        ReportProgress(progress, 15, "Hardware", "Coletando resumo essencial de hardware e BIOS...");
        report.Hardware = await _hardwareService.AnalyzeHardwareAsync(cancellationToken);

        ReportProgress(progress, 40, "Segurança do Windows", "Verificando TPM 2.0, Secure Boot e proteções essenciais...");
        report.Security = await _securityService.AnalyzeSecurityAsync(cancellationToken);

        ReportProgress(progress, 65, "Anticheat", "Verificando componentes Easy Anti-Cheat, EOS e BattlEye...");
        report.AntiCheat = await _antiCheatService.AnalyzeAntiCheatAsync(cancellationToken);

        ReportProgress(progress, 85, "Rede e Fortnite", "Verificando instalação do Fortnite e adaptadores de rede...");
        report.Network = await _networkService.AnalyzeNetworkAsync(checkEpicOnline: false, cancellationToken);
        report.Fortnite = await _fortniteService.AnalyzeFortniteAsync(report.Hardware, report.Network, cancellationToken);

        FinalizeReport(report);

        settings.LastDiagnosticTimestamp = report.GeneratedAt;
        await _settingsStorage.SaveSettingsAsync(settings, cancellationToken);
        if (settings.SaveLocalDiagnosticHistory)
        {
            await _settingsStorage.SaveLastReportAsync(report, cancellationToken);
        }

        ReportProgress(progress, 100, "Concluído", "Diagnóstico rápido concluído.");
        return report;
    }

    private static void FinalizeReport(FullDiagnosticReport report)
    {
        report.AllChecks.Clear();
        report.AllChecks.AddRange(report.Hardware.Checks);
        report.AllChecks.AddRange(report.Network.Checks);
        report.AllChecks.AddRange(report.Security.Checks);
        report.AllChecks.AddRange(report.DriversAndServices.Checks);
        report.AllChecks.AddRange(report.AntiCheat.Checks);
        report.AllChecks.AddRange(report.Fortnite.Checks);
        report.AllChecks.AddRange(report.SystemIntegrity.Checks);

        var (score, problems, alerts, completed) = HealthScoreCalculator.Calculate(report.AllChecks);
        report.SystemHealthScore = score;
        report.ProblemsFoundCount = problems;
        report.AlertsFoundCount = alerts;
        report.CompletedChecksCount = completed;

        report.AccessRestrictionAnalysis = AccessRestrictionAnalyzer.Analyze(new AccessRestrictionQuestionnaire(), report);
        report.RecommendedSolutions = SolutionCatalogProvider.BuildSolutionsForReport(report);
    }

    private static void ReportProgress(
        IProgress<DiagnosticProgressUpdate>? progress,
        int percentage,
        string module,
        string operation)
    {
        progress?.Report(new DiagnosticProgressUpdate
        {
            Percentage = percentage,
            CurrentModule = module,
            CurrentOperation = operation
        });
    }
}
