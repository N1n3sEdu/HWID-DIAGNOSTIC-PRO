using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using HWIDDiagnosticPro.App.ViewModels;
using HWIDDiagnosticPro.App.Views;
using HWIDDiagnosticPro.Core.Services;
using HWIDDiagnosticPro.Infrastructure.AntiCheat;
using HWIDDiagnosticPro.Infrastructure.Diagnostics;
using HWIDDiagnosticPro.Infrastructure.Hardware;
using HWIDDiagnosticPro.Infrastructure.Network;
using HWIDDiagnosticPro.Infrastructure.Security;
using HWIDDiagnosticPro.Infrastructure.Services;
using HWIDDiagnosticPro.Reporting;

namespace HWIDDiagnosticPro.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        DispatcherUnhandledException += (_, args) =>
        {
            var logger = _serviceProvider?.GetService<ILocalLoggerService>();
            logger?.LogError("Interface", "Exceção não tratada na interface gráfica.", args.Exception);
            args.Handled = true;
        };

        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        mainWindow.Show();
        await mainViewModel.InitializeAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ILocalLoggerService, LocalLoggerService>();
        services.AddSingleton<ISettingsStorageService, JsonSettingsStorageService>();
        services.AddSingleton<IHardwareDiagnosticService, HardwareDiagnosticService>();
        services.AddSingleton<INetworkDiagnosticService, NetworkDiagnosticService>();
        services.AddSingleton<IWindowsSecurityDiagnosticService, WindowsSecurityDiagnosticService>();
        services.AddSingleton<IDriversAndServicesDiagnosticService, DriversAndServicesDiagnosticService>();
        services.AddSingleton<IAntiCheatDiagnosticService, AntiCheatDiagnosticService>();
        services.AddSingleton<IFortniteDiagnosticService, FortniteDiagnosticService>();
        services.AddSingleton<ISystemIntegrityDiagnosticService, SystemIntegrityDiagnosticService>();
        services.AddSingleton<IDiagnosticOrchestratorService, DiagnosticOrchestratorService>();
        services.AddSingleton<IReportExportService, ReportExportService>();

        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
