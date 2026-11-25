using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using HWIDDiagnosticPro.App.Commands;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDiagnosticOrchestratorService _orchestrator;
    private readonly IAntiCheatDiagnosticService _antiCheatService;
    private readonly IFortniteDiagnosticService _fortniteService;
    private readonly ISystemIntegrityDiagnosticService _integrityService;
    private readonly IReportExportService _reportExportService;
    private readonly ISettingsStorageService _settingsStorage;
    private readonly ILocalLoggerService _logger;

    private CancellationTokenSource? _scanCts;
    private FullDiagnosticReport _currentReport = new();
    private AppSettings _settings = new();
    private string _selectedModuleKey = "Dashboard";
    private string _selectedModuleTitle = "Dashboard Principal";
    private bool _isBusy;
    private int _progressPercentage;
    private string _progressStatusText = "Pronto para iniciar o diagnóstico local.";
    private string _notificationMessage = string.Empty;

    // Campos de entrada do Módulo Fortnite e Análise de Restrições (Seções 11 e 12)
    private string _manualFortniteErrorInput = string.Empty;
    private FortniteErrorInterpretation _currentErrorInterpretation;
    private string _restrictionDuration = "Nenhuma / Não informada";
    private string _restrictionMessage = string.Empty;
    private bool _restrictionCanAccessLobby = true;
    private bool _restrictionKickedDuringMatch;
    private bool _restrictionWindowsReinstalled;
    private bool _restrictionUsesVpnOrProxy;
    private bool _restrictionPurchasedThirdParty;
    private bool _restrictionConfirmedInEpicPortal;
    private AccessRestrictionAnalysisResult _currentRestrictionResult = new();

    // Relatórios
    private string _reportPreviewText = string.Empty;
    private string _lastExportedFilePath = "Nenhum arquivo exportado nesta sessão";

    public MainViewModel(
        IDiagnosticOrchestratorService orchestrator,
        IAntiCheatDiagnosticService antiCheatService,
        IFortniteDiagnosticService fortniteService,
        ISystemIntegrityDiagnosticService integrityService,
        IReportExportService reportExportService,
        ISettingsStorageService settingsStorage,
        ILocalLoggerService logger)
    {
        _orchestrator = orchestrator;
        _antiCheatService = antiCheatService;
        _fortniteService = fortniteService;
        _integrityService = integrityService;
        _reportExportService = reportExportService;
        _settingsStorage = settingsStorage;
        _logger = logger;

        _currentErrorInterpretation = FortniteErrorInterpreter.Interpret(string.Empty);

        NavigationItems = new ObservableCollection<NavigationItemViewModel>(BuildSidebarModules());
        PunishmentDurationOptions =
        [
            "Nenhuma / Não informada",
            "Expulsão da partida sem tempo exibido",
            "Temporária (24 horas)",
            "Temporária (7 a 30 dias)",
            "Permanente / Indefinida",
            "Não sei informar"
        ];
        ReportFormatOptions = ["HTML", "PDF", "JSON", "TXT"];

        SelectModuleCommand = new RelayCommand(param =>
        {
            if (param is string key)
                SelectModule(key);
            else if (param is NavigationItemViewModel nav)
                SelectModule(nav.Key);
        });

        StartFullDiagnosticCommand = new AsyncRelayCommand(ExecuteFullDiagnosticAsync, () => !IsBusy);
        StartQuickDiagnosticCommand = new AsyncRelayCommand(ExecuteQuickDiagnosticAsync, () => !IsBusy);
        VerifyAntiCheatCommand = new AsyncRelayCommand(ExecuteAntiCheatVerificationAsync, () => !IsBusy);
        VerifyFortniteCommand = new AsyncRelayCommand(ExecuteFortniteVerificationAsync, () => !IsBusy);
        RunReadOnlyIntegrityVerificationCommand = new AsyncRelayCommand(ExecuteDeepIntegrityCheckAsync, () => !IsBusy);
        CancelDiagnosticCommand = new RelayCommand(CancelCurrentScan, () => IsBusy);

        InterpretFortniteErrorCommand = new RelayCommand(() =>
        {
            CurrentErrorInterpretation = FortniteErrorInterpreter.Interpret(ManualFortniteErrorInput);
            NotificationMessage = "Mensagem de erro analisada com base na documentação oficial.";
        });

        AnalyzeAccessRestrictionsCommand = new RelayCommand(ExecuteAccessRestrictionAnalysis);

        ExportReportCommand = new AsyncRelayCommand(() => ExportReportInFormatAsync(Settings.DefaultReportFormat));
        ExportPdfCommand = new AsyncRelayCommand(() => ExportReportInFormatAsync("PDF"));
        ExportHtmlCommand = new AsyncRelayCommand(() => ExportReportInFormatAsync("HTML"));
        ExportJsonCommand = new AsyncRelayCommand(() => ExportReportInFormatAsync("JSON"));
        ExportTxtCommand = new AsyncRelayCommand(() => ExportReportInFormatAsync("TXT"));
        CopySupportSummaryCommand = new RelayCommand(CopyExecutiveSummaryToClipboard);
        OpenOfficialUrlCommand = new RelayCommand(param => OpenUrlSafely(param?.ToString()));
        SaveSettingsCommand = new AsyncRelayCommand(SaveUserSettingsAsync);

        SelectModule("Dashboard");
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
    public IReadOnlyList<string> PunishmentDurationOptions { get; }
    public IReadOnlyList<string> ReportFormatOptions { get; }

    public RelayCommand SelectModuleCommand { get; }
    public AsyncRelayCommand StartFullDiagnosticCommand { get; }
    public AsyncRelayCommand StartQuickDiagnosticCommand { get; }
    public AsyncRelayCommand VerifyAntiCheatCommand { get; }
    public AsyncRelayCommand VerifyFortniteCommand { get; }
    public AsyncRelayCommand RunReadOnlyIntegrityVerificationCommand { get; }
    public RelayCommand CancelDiagnosticCommand { get; }
    public RelayCommand InterpretFortniteErrorCommand { get; }
    public RelayCommand AnalyzeAccessRestrictionsCommand { get; }
    public AsyncRelayCommand ExportReportCommand { get; }
    public AsyncRelayCommand ExportPdfCommand { get; }
    public AsyncRelayCommand ExportHtmlCommand { get; }
    public AsyncRelayCommand ExportJsonCommand { get; }
    public AsyncRelayCommand ExportTxtCommand { get; }
    public RelayCommand CopySupportSummaryCommand { get; }
    public RelayCommand OpenOfficialUrlCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }

    public FullDiagnosticReport CurrentReport
    {
        get => _currentReport;
        private set
        {
            if (SetProperty(ref _currentReport, value))
            {
                RefreshDashboardProperties();
            }
        }
    }

    public AppSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public string SelectedModuleKey
    {
        get => _selectedModuleKey;
        private set => SetProperty(ref _selectedModuleKey, value);
    }

    public string SelectedModuleTitle
    {
        get => _selectedModuleTitle;
        private set => SetProperty(ref _selectedModuleTitle, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                StartFullDiagnosticCommand.RaiseCanExecuteChanged();
                StartQuickDiagnosticCommand.RaiseCanExecuteChanged();
                VerifyAntiCheatCommand.RaiseCanExecuteChanged();
                VerifyFortniteCommand.RaiseCanExecuteChanged();
                RunReadOnlyIntegrityVerificationCommand.RaiseCanExecuteChanged();
                CancelDiagnosticCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    public string ProgressStatusText
    {
        get => _progressStatusText;
        private set => SetProperty(ref _progressStatusText, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        set => SetProperty(ref _notificationMessage, value);
    }

    // 15 Indicadores do Dashboard Principal (Seção 5)
    public string ComputerName => CurrentReport.Hardware.ComputerName;
    public string WindowsVersion => CurrentReport.Hardware.WindowsVersion;
    public string ProcessorModel => CurrentReport.Hardware.Processor.Model;
    public string RamAmount => CurrentReport.Hardware.Ram.TotalCapacityDisplay;
    public string MotherboardModel => $"{CurrentReport.Hardware.Motherboard.Manufacturer} {CurrentReport.Hardware.Motherboard.Model}".Trim();
    public string GpuModel => CurrentReport.Hardware.Gpus.FirstOrDefault()?.Model ?? "Não disponível";
    public string DiskCount => CurrentReport.Hardware.StorageDevices.Count > 0
        ? $"{CurrentReport.Hardware.StorageDevices.Count} unidade(s) física(s)"
        : "Não disponível";
    public string ConnectionStatus => CurrentReport.Network.ConnectionSummary;
    public string SecureBootStatus => CurrentReport.Security.SecureBootStatus;
    public string TpmStatus => CurrentReport.Security.Tpm20Status;
    public string AntiCheatComponentsStatus =>
        $"EAC: {CurrentReport.AntiCheat.EasyAntiCheat.StatusDisplay} | EOS: {CurrentReport.AntiCheat.EasyAntiCheatEos.StatusDisplay} | BE: {CurrentReport.AntiCheat.BattlEye.StatusDisplay}";
    public string LastDiagnosticDate => Settings.LastDiagnosticTimestamp.HasValue
        ? Settings.LastDiagnosticTimestamp.Value.ToString("dd/MM/yyyy HH:mm:ss")
        : CurrentReport.GeneratedAtDisplay;
    public int ProblemsFoundCount => CurrentReport.ProblemsFoundCount;
    public int AlertsCount => CurrentReport.AlertsFoundCount;
    public int CompletedChecksCount => CurrentReport.CompletedChecksCount;
    public int HealthScore => CurrentReport.SystemHealthScore;
    public string HealthScoreColorHex => HealthScore >= 80 ? "#10B981" : HealthScore >= 55 ? "#F59E0B" : "#EF4444";

    // Propriedades de Entrada de Erros do Fortnite e Restrições
    public string ManualFortniteErrorInput
    {
        get => _manualFortniteErrorInput;
        set => SetProperty(ref _manualFortniteErrorInput, value);
    }

    public FortniteErrorInterpretation CurrentErrorInterpretation
    {
        get => _currentErrorInterpretation;
        private set => SetProperty(ref _currentErrorInterpretation, value);
    }

    public string RestrictionDuration
    {
        get => _restrictionDuration;
        set => SetProperty(ref _restrictionDuration, value);
    }

    public string RestrictionMessage
    {
        get => _restrictionMessage;
        set => SetProperty(ref _restrictionMessage, value);
    }

    public bool RestrictionCanAccessLobby
    {
        get => _restrictionCanAccessLobby;
        set => SetProperty(ref _restrictionCanAccessLobby, value);
    }

    public bool RestrictionKickedDuringMatch
    {
        get => _restrictionKickedDuringMatch;
        set => SetProperty(ref _restrictionKickedDuringMatch, value);
    }

    public bool RestrictionWindowsReinstalled
    {
        get => _restrictionWindowsReinstalled;
        set => SetProperty(ref _restrictionWindowsReinstalled, value);
    }

    public bool RestrictionUsesVpnOrProxy
    {
        get => _restrictionUsesVpnOrProxy;
        set => SetProperty(ref _restrictionUsesVpnOrProxy, value);
    }

    public bool RestrictionPurchasedThirdParty
    {
        get => _restrictionPurchasedThirdParty;
        set => SetProperty(ref _restrictionPurchasedThirdParty, value);
    }

    public bool RestrictionConfirmedInEpicPortal
    {
        get => _restrictionConfirmedInEpicPortal;
        set => SetProperty(ref _restrictionConfirmedInEpicPortal, value);
    }

    public AccessRestrictionAnalysisResult CurrentRestrictionResult
    {
        get => _currentRestrictionResult;
        private set => SetProperty(ref _currentRestrictionResult, value);
    }

    public string ReportPreviewText
    {
        get => _reportPreviewText;
        private set => SetProperty(ref _reportPreviewText, value);
    }

    public string LastExportedFilePath
    {
        get => _lastExportedFilePath;
        private set => SetProperty(ref _lastExportedFilePath, value);
    }

    public IReadOnlyList<SystemEventLogEntry> CombinedEventLogs
    {
        get
        {
            var list = new List<SystemEventLogEntry>();
            list.AddRange(CurrentReport.SystemIntegrity.RelevantEvents);
            list.AddRange(_logger.GetApplicationLogs());
            return list.OrderByDescending(e => e.Timestamp).ToList();
        }
    }

    public async Task InitializeAsync()
    {
        Settings = await _settingsStorage.LoadSettingsAsync();
        var cachedReport = await _settingsStorage.LoadLastReportAsync();
        if (cachedReport != null)
        {
            CurrentReport = cachedReport;
            CurrentRestrictionResult = cachedReport.AccessRestrictionAnalysis;
            ReportPreviewText = _reportExportService.GenerateExecutiveSupportSummary(cachedReport);
        }

        // Ao abrir o executável na máquina, inicia automaticamente a coleta completa e salva os relatórios na pasta Relatorios/
        await ExecuteFullDiagnosticAsync();
    }

    public void SelectModule(string key)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase);
            if (item.IsSelected)
            {
                SelectedModuleKey = item.Key;
                SelectedModuleTitle = item.Title;
            }
        }
    }

    private async Task ExecuteFullDiagnosticAsync()
    {
        await RunWithProgressAsync(async (progress, ct) =>
        {
            var report = await _orchestrator.RunFullDiagnosticAsync(progress, ct);
            CurrentReport = report;
            Settings.LastDiagnosticTimestamp = report.GeneratedAt;
            ExecuteAccessRestrictionAnalysis();
            ReportPreviewText = _reportExportService.GenerateTxtReport(report);

            // Salvar automaticamente os 4 formatos (HTML, PDF, TXT, JSON) na pasta Relatorios ao lado do executável
            var htmlPath = await _reportExportService.ExportReportToFileAsync(report, "HTML", Settings.ReportsOutputDirectory, ct);
            await _reportExportService.ExportReportToFileAsync(report, "PDF", Settings.ReportsOutputDirectory, ct);
            await _reportExportService.ExportReportToFileAsync(report, "TXT", Settings.ReportsOutputDirectory, ct);
            await _reportExportService.ExportReportToFileAsync(report, "JSON", Settings.ReportsOutputDirectory, ct);
            LastExportedFilePath = htmlPath;

            NotificationMessage = $"Diagnóstico completo concluído e relatórios salvos em: {Settings.ReportsOutputDirectory}";
        });
    }

    private async Task ExecuteQuickDiagnosticAsync()
    {
        await RunWithProgressAsync(async (progress, ct) =>
        {
            var report = await _orchestrator.RunQuickDiagnosticAsync(progress, ct);
            CurrentReport = report;
            Settings.LastDiagnosticTimestamp = report.GeneratedAt;
            ExecuteAccessRestrictionAnalysis();
            ReportPreviewText = _reportExportService.GenerateExecutiveSupportSummary(report);
            NotificationMessage = "Diagnóstico rápido concluído com sucesso.";
        });
    }

    private async Task ExecuteAntiCheatVerificationAsync()
    {
        SelectModule("Anticheat");
        await RunWithProgressAsync(async (progress, ct) =>
        {
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 40,
                CurrentModule = "Anticheat",
                CurrentOperation = "Verificando Easy Anti-Cheat, Easy Anti-Cheat EOS, BattlEye e Epic Games Launcher..."
            });

            var acResult = await _antiCheatService.AnalyzeAntiCheatAsync(ct);
            CurrentReport.AntiCheat = acResult;
            RecalculateReportAggregates();
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 100,
                CurrentModule = "Anticheat",
                CurrentOperation = "Verificação dos componentes anticheat concluída."
            });
            NotificationMessage = "Verificação dedicada de Anticheat concluída.";
        });
    }

    private async Task ExecuteFortniteVerificationAsync()
    {
        SelectModule("Fortnite");
        await RunWithProgressAsync(async (progress, ct) =>
        {
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 45,
                CurrentModule = "Fortnite",
                CurrentOperation = "Verificando instalação, arquivos principais, Visual C++ e logs do Fortnite..."
            });

            var fnResult = await _fortniteService.AnalyzeFortniteAsync(CurrentReport.Hardware, CurrentReport.Network, ct);
            CurrentReport.Fortnite = fnResult;
            RecalculateReportAggregates();
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 100,
                CurrentModule = "Fortnite",
                CurrentOperation = "Diagnóstico do Fortnite concluído."
            });
            NotificationMessage = "Verificação dedicada do Fortnite concluída.";
        });
    }

    private async Task ExecuteDeepIntegrityCheckAsync()
    {
        var confirm = MessageBox.Show(
            "Esta verificação executará os utilitários oficiais do Windows 'DISM /CheckHealth' e 'SFC /verifyonly' em modo estritamente de leitura (sem modificar arquivos do sistema).\n\nDeseja prosseguir?",
            "Confirmação de Verificação Oficial (SFC / DISM)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (confirm != MessageBoxResult.Yes)
            return;

        await RunWithProgressAsync(async (progress, ct) =>
        {
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 35,
                CurrentModule = "Integridade do Sistema",
                CurrentOperation = "Executando DISM /CheckHealth e SFC /verifyonly em modo somente leitura..."
            });

            var integrity = await _integrityService.AnalyzeSystemIntegrityAsync(runOfficialVerificationTools: true, ct);
            CurrentReport.SystemIntegrity = integrity;
            RecalculateReportAggregates();
            progress.Report(new DiagnosticProgressUpdate
            {
                Percentage = 100,
                CurrentModule = "Integridade do Sistema",
                CurrentOperation = "Verificação profunda de integridade concluída."
            });
            NotificationMessage = "Verificação de integridade do Windows finalizada.";
        });
    }

    private void ExecuteAccessRestrictionAnalysis()
    {
        var questionnaire = new AccessRestrictionQuestionnaire
        {
            PunishmentDuration = RestrictionDuration,
            FortniteMessage = RestrictionMessage,
            CanAccessLobby = RestrictionCanAccessLobby,
            KickedDuringMatch = RestrictionKickedDuringMatch,
            WindowsReinstalled = RestrictionWindowsReinstalled,
            UsesVpnOrProxy = RestrictionUsesVpnOrProxy,
            ComputerPurchasedFromThirdParty = RestrictionPurchasedThirdParty,
            ConfirmedSanctionInEpicPortal = RestrictionConfirmedInEpicPortal
        };

        var analysis = AccessRestrictionAnalyzer.Analyze(questionnaire, CurrentReport);
        CurrentReport.AccessRestrictionAnalysis = analysis;
        CurrentRestrictionResult = analysis;
        NotificationMessage = $"Análise de restrições atualizada: {analysis.OutcomeTitle}.";
    }

    private async Task ExportReportInFormatAsync(string format)
    {
        try
        {
            var path = await _reportExportService.ExportReportToFileAsync(
                CurrentReport,
                format,
                Settings.ReportsOutputDirectory);

            LastExportedFilePath = path;
            ReportPreviewText = format.ToUpperInvariant() switch
            {
                "JSON" => _reportExportService.GenerateJsonReport(CurrentReport),
                "HTML" => _reportExportService.GenerateHtmlReport(CurrentReport),
                _ => _reportExportService.GenerateTxtReport(CurrentReport)
            };

            NotificationMessage = $"Relatório {format.ToUpperInvariant()} exportado com sucesso (dados sensíveis mascarados): {path}";
            SelectModule("Relatorios");
        }
        catch (Exception ex)
        {
            _logger.LogError("Relatórios", $"Erro ao exportar relatório {format}.", ex);
            NotificationMessage = $"Falha ao exportar relatório: {ex.Message}";
        }
    }

    private void CopyExecutiveSummaryToClipboard()
    {
        try
        {
            var summary = _reportExportService.GenerateExecutiveSupportSummary(CurrentReport);
            ReportPreviewText = summary;
            Clipboard.SetText(summary);
            NotificationMessage = "Relatório resumido (com dados sensíveis mascarados) copiado para a área de transferência!";
        }
        catch (Exception ex)
        {
            _logger.LogError("Relatórios", "Falha ao copiar resumo para a área de transferência.", ex);
            NotificationMessage = "Não foi possível copiar para a área de transferência.";
        }
    }

    private void OpenUrlSafely(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Atalho", $"Não foi possível abrir o link '{url}': {ex.Message}");
            NotificationMessage = $"Link oficial: {url}";
        }
    }

    private async Task SaveUserSettingsAsync()
    {
        await _settingsStorage.SaveSettingsAsync(Settings);
        NotificationMessage = "Configurações salvas localmente em JSON com sucesso.";
    }

    private void CancelCurrentScan()
    {
        try
        {
            _scanCts?.Cancel();
            ProgressStatusText = "Cancelando verificação a pedido do usuário...";
            NotificationMessage = "Verificação cancelada pelo usuário.";
        }
        catch
        {
            // Ignorar
        }
    }

    private async Task RunWithProgressAsync(Func<IProgress<DiagnosticProgressUpdate>, CancellationToken, Task> action)
    {
        if (IsBusy)
            return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            ProgressPercentage = 5;

            var progress = new Progress<DiagnosticProgressUpdate>(update =>
            {
                ProgressPercentage = update.Percentage;
                ProgressStatusText = $"[{update.CurrentModule}] {update.CurrentOperation}";
            });

            await action(progress, _scanCts.Token);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText = "Diagnóstico cancelado pelo usuário.";
            _logger.LogWarning("Orquestrador", "Operação de diagnóstico cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            ProgressStatusText = $"Erro durante a verificação: {ex.Message}";
            _logger.LogError("Orquestrador", "Falha inesperada durante execução do diagnóstico.", ex);
        }
        finally
        {
            IsBusy = false;
            RefreshDashboardProperties();
        }
    }

    private void RecalculateReportAggregates()
    {
        CurrentReport.AllChecks.Clear();
        CurrentReport.AllChecks.AddRange(CurrentReport.Hardware.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.Network.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.Security.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.DriversAndServices.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.AntiCheat.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.Fortnite.Checks);
        CurrentReport.AllChecks.AddRange(CurrentReport.SystemIntegrity.Checks);

        var (score, problems, alerts, completed) = HealthScoreCalculator.Calculate(CurrentReport.AllChecks);
        CurrentReport.SystemHealthScore = score;
        CurrentReport.ProblemsFoundCount = problems;
        CurrentReport.AlertsFoundCount = alerts;
        CurrentReport.CompletedChecksCount = completed;
        CurrentReport.RecommendedSolutions = SolutionCatalogProvider.BuildSolutionsForReport(CurrentReport);
        RefreshDashboardProperties();
    }

    private void RefreshDashboardProperties()
    {
        OnPropertyChanged(nameof(CurrentReport));
        OnPropertyChanged(nameof(ComputerName));
        OnPropertyChanged(nameof(WindowsVersion));
        OnPropertyChanged(nameof(ProcessorModel));
        OnPropertyChanged(nameof(RamAmount));
        OnPropertyChanged(nameof(MotherboardModel));
        OnPropertyChanged(nameof(GpuModel));
        OnPropertyChanged(nameof(DiskCount));
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(SecureBootStatus));
        OnPropertyChanged(nameof(TpmStatus));
        OnPropertyChanged(nameof(AntiCheatComponentsStatus));
        OnPropertyChanged(nameof(LastDiagnosticDate));
        OnPropertyChanged(nameof(ProblemsFoundCount));
        OnPropertyChanged(nameof(AlertsCount));
        OnPropertyChanged(nameof(CompletedChecksCount));
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthScoreColorHex));
        OnPropertyChanged(nameof(CombinedEventLogs));
    }

    private static List<NavigationItemViewModel> BuildSidebarModules() =>
    [
        new() { Order = 1,  Key = "Dashboard",           Title = "1. Dashboard",              IconGlyph = "◈", Subtitle = "Visão geral e indicadores do sistema" },
        new() { Order = 2,  Key = "DiagnosticoCompleto", Title = "2. Diagnóstico completo",   IconGlyph = "⚡", Subtitle = "Todas as verificações técnicas consolidadas" },
        new() { Order = 3,  Key = "Hardware",            Title = "3. Hardware",               IconGlyph = "⚙", Subtitle = "Processador, Memória RAM e resumo físico" },
        new() { Order = 4,  Key = "BiosPlacaMae",        Title = "4. BIOS e placa-mãe",       IconGlyph = "▣", Subtitle = "SMBIOS, UEFI, Secure Boot e UUID mascarado" },
        new() { Order = 5,  Key = "SsdHdd",              Title = "5. SSD e HDD",              IconGlyph = "▤", Subtitle = "Armazenamento, NVMe/SATA, SMART e volumes" },
        new() { Order = 6,  Key = "PlacaVideo",          Title = "6. Placa de vídeo",         IconGlyph = "❖", Subtitle = "GPU, versão de driver WHQL, VRAM e DirectX" },
        new() { Order = 7,  Key = "RedeInternet",        Title = "7. Rede e internet",        IconGlyph = "⇄", Subtitle = "Adaptadores, DNS, VPN, latência e Epic Games" },
        new() { Order = 8,  Key = "SegurancaWindows",    Title = "8. Segurança do Windows",   IconGlyph = "🛡", Subtitle = "TPM 2.0, Secure Boot, Defender, HVCI e UAC" },
        new() { Order = 9,  Key = "DriversServicos",     Title = "9. Drivers e serviços",     IconGlyph = "☰", Subtitle = "Assinaturas digitais, serviços e erros PnP" },
        new() { Order = 10, Key = "Anticheat",           Title = "10. Anticheat",             IconGlyph = "✓", Subtitle = "Easy Anti-Cheat, EOS, BattlEye e Launcher" },
        new() { Order = 11, Key = "Fortnite",            Title = "11. Fortnite",              IconGlyph = "★", Subtitle = "Arquivos, erros de partida e análise de restrições" },
        new() { Order = 12, Key = "IntegridadeSistema",  Title = "12. Integridade do sistema", IconGlyph = "✚", Subtitle = "SFC, DISM CheckHealth e saúde do Windows" },
        new() { Order = 13, Key = "LogsEventos",         Title = "13. Logs e eventos",        IconGlyph = "▤", Subtitle = "Visualizador de Eventos e registros locais" },
        new() { Order = 14, Key = "Relatorios",          Title = "14. Relatórios",            IconGlyph = "⎙", Subtitle = "Exportação mascarada em PDF, HTML, JSON e TXT" },
        new() { Order = 15, Key = "CentralSolucoes",     Title = "15. Central de soluções",   IconGlyph = "✦", Subtitle = "Procedimentos oficiais de correção passo a passo" },
        new() { Order = 16, Key = "Configuracoes",       Title = "16. Configurações",         IconGlyph = "⚙", Subtitle = "Privacidade, timeouts e preferências locais" }
    ];
}
