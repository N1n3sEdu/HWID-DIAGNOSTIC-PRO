using System.Diagnostics;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;
using HWIDDiagnosticPro.Infrastructure.Helpers;

namespace HWIDDiagnosticPro.Infrastructure.Diagnostics;

/// <summary>
/// Módulo de Drivers e Serviços (Seção 9).
/// Lista e verifica drivers instalados, fabricantes, versões, datas, assinaturas digitais,
/// serviços do Windows e de terceiros, dispositivos com erros e falhas registradas.
/// Nunca classifica automaticamente um driver desconhecido ou sem assinatura como malware ou trapaça.
/// </summary>
public sealed class DriversAndServicesDiagnosticService : IDriversAndServicesDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public DriversAndServicesDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public Task<DriversAndServicesResult> AnalyzeDriversAndServicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new DriversAndServicesResult();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                QueryInstalledDrivers(result);

                cancellationToken.ThrowIfCancellationRequested();
                QueryServices(result);

                cancellationToken.ThrowIfCancellationRequested();
                QueryProblematicDevices(result);

                cancellationToken.ThrowIfCancellationRequested();
                QueryDriverFailuresInEventLog(result);

                BuildChecks(result);
                result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);

                _logger.LogInfo("Drivers", $"Inventário concluído: {result.InstalledDrivers.Count} drivers, {result.WindowsServices.Count + result.ThirdPartyServices.Count} serviços.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Drivers", "Erro ao consultar drivers e serviços.", ex);
            }

            return result;
        }, cancellationToken);
    }

    private void QueryInstalledDrivers(DriversAndServicesResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var rows = WmiQueryHelper.Query(
                "SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, IsSigned, Signer, DeviceClass FROM Win32_PnPSignedDriver");

            foreach (var row in rows)
            {
                var deviceName = WmiQueryHelper.GetString(row, "DeviceName");
                if (deviceName.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
                    continue;

                var isSigned = WmiQueryHelper.GetNullableBool(row, "IsSigned");
                var signer = WmiQueryHelper.GetString(row, "Signer");
                var manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                var version = WmiQueryHelper.GetString(row, "DriverVersion");
                var date = WmiQueryHelper.FormatWmiDate(WmiQueryHelper.GetString(row, "DriverDate"));
                var deviceClass = WmiQueryHelper.GetString(row, "DeviceClass");

                // Regra ética Seção 9: Um driver sem assinatura recebe status ATENCAO para análise,
                // nunca sendo classificado automaticamente como malware ou ferramenta de trapaça.
                var status = isSigned switch
                {
                    false => DiagnosticStatus.ATENCAO,
                    true => DiagnosticStatus.NORMAL,
                    null => DiagnosticStatus.INDISPONIVEL
                };

                result.InstalledDrivers.Add(new InstalledDriverInfo
                {
                    DeviceName = deviceName,
                    Manufacturer = manufacturer,
                    Version = version,
                    DriverDate = date,
                    IsDigitallySigned = isSigned,
                    Signer = signer,
                    DeviceClass = deviceClass,
                    Status = status
                });
            }

            result.InstalledDrivers = result.InstalledDrivers
                .OrderBy(d => d.IsDigitallySigned == false ? 0 : 1)
                .ThenBy(d => d.DeviceName)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Drivers", $"Falha ao listar Win32_PnPSignedDriver: {ex.Message}");
        }
    }

    private void QueryServices(DriversAndServicesResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var rows = WmiQueryHelper.Query("SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service");

            foreach (var row in rows)
            {
                var name = WmiQueryHelper.GetString(row, "Name");
                var displayName = WmiQueryHelper.GetString(row, "DisplayName");
                var state = WmiQueryHelper.GetString(row, "State");
                var startMode = WmiQueryHelper.GetString(row, "StartMode");
                var pathName = WmiQueryHelper.GetString(row, "PathName");

                bool isWindowsService = pathName.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) ||
                                        pathName.Contains("svchost.exe", StringComparison.OrdinalIgnoreCase);

                var svc = new SystemServiceInfo
                {
                    ServiceName = name,
                    DisplayName = displayName,
                    State = TranslateServiceState(state),
                    StartMode = TranslateStartMode(startMode),
                    Category = isWindowsService ? "Serviço do Windows" : "Serviço de Terceiros",
                    IsThirdParty = !isWindowsService,
                    BinaryPath = pathName,
                    Status = DiagnosticStatus.NORMAL
                };

                if (isWindowsService)
                    result.WindowsServices.Add(svc);
                else
                    result.ThirdPartyServices.Add(svc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Drivers", $"Falha ao consultar Win32_Service: {ex.Message}");
        }
    }

    private static string TranslateServiceState(string state) => state.ToUpperInvariant() switch
    {
        "RUNNING" => "Em execução",
        "STOPPED" => "Parado",
        "PAUSED" => "Pausado",
        "START PENDING" => "Iniciando",
        "STOP PENDING" => "Parando",
        _ => state
    };

    private static string TranslateStartMode(string mode) => mode.ToUpperInvariant() switch
    {
        "AUTO" => "Automático",
        "MANUAL" => "Manual",
        "DISABLED" => "Desativado",
        _ => mode
    };

    private void QueryProblematicDevices(DriversAndServicesResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var rows = WmiQueryHelper.Query(
                "SELECT Name, PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");

            foreach (var row in rows)
            {
                var code = WmiQueryHelper.GetUInt32(row, "ConfigManagerErrorCode");
                var name = WmiQueryHelper.GetString(row, "Name");
                var pnpId = WmiQueryHelper.GetString(row, "PNPDeviceID");

                var err = new ProblematicDeviceInfo
                {
                    DeviceName = name,
                    PnpDeviceId = pnpId,
                    ErrorCode = code,
                    ErrorExplanation = TranslateConfigManagerErrorCode(code),
                    Status = code == 22 ? DiagnosticStatus.ATENCAO : DiagnosticStatus.ERRO
                };

                result.DevicesWithErrors.Add(err);

                if (code == 52)
                {
                    result.KnownIncompatibilities.Add(
                        $"O dispositivo '{name}' apresentou Erro Código 52 (Assinatura digital não verificada pelo Windows), incompatível com sistemas anticheat.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Drivers", $"Falha ao consultar dispositivos com erros: {ex.Message}");
        }
    }

    private static string TranslateConfigManagerErrorCode(uint code) => code switch
    {
        1 => "Código 1: O dispositivo não está configurado corretamente.",
        10 => "Código 10: Este dispositivo não pode ser iniciado.",
        12 => "Código 12: Este dispositivo não está encontrando recursos livres suficientes.",
        14 => "Código 14: Este dispositivo não pode funcionar corretamente até que o computador seja reiniciado.",
        18 => "Código 18: Reinstale os drivers para este dispositivo.",
        19 => "Código 19: Informações de configuração no Registro incompletas ou danificadas.",
        22 => "Código 22: Este dispositivo está desativado no Gerenciador de Dispositivos.",
        28 => "Código 28: Os drivers para este dispositivo não estão instalados.",
        31 => "Código 31: Este dispositivo não está funcionando corretamente porque o Windows não pode carregar os drivers necessários.",
        32 => "Código 32: Um driver (serviço) para este dispositivo foi desativado.",
        37 => "Código 37: O Windows não pode inicializar o driver de dispositivo para este hardware.",
        39 => "Código 39: O Windows não pode carregar o driver de dispositivo para este hardware (driver corrompido ou ausente).",
        43 => "Código 43: O Windows interrompeu este dispositivo porque ele encontrou problemas.",
        45 => "Código 45: No momento, este dispositivo de hardware não está conectado ao computador.",
        52 => "Código 52: O Windows não pode verificar a assinatura digital dos drivers necessários para este dispositivo.",
        _ => $"Código {code}: Erro reportado pelo Gerenciador de Dispositivos do Windows."
    };

    private void QueryDriverFailuresInEventLog(DriversAndServicesResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var systemLog = new EventLog("System");
            int count = systemLog.Entries.Count;
            int inspected = 0;

            for (int i = count - 1; i >= 0 && inspected < 250 && result.RegisteredDriverFailures.Count < 15; i--, inspected++)
            {
                var entry = systemLog.Entries[i];
                if (entry.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                    continue;

                if (entry.Source.Contains("Kernel-PnP", StringComparison.OrdinalIgnoreCase) ||
                    entry.Source.Contains("DriverFrameworks", StringComparison.OrdinalIgnoreCase) ||
                    entry.Source.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) ||
                    (entry.Source.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase) && entry.InstanceId is 7000 or 7026))
                {
                    result.RegisteredDriverFailures.Add(
                        $"[{entry.TimeGenerated:dd/MM/yyyy HH:mm}] {entry.Source} (ID {entry.InstanceId & 0xFFFF}): {entry.Message.Split('\n').FirstOrDefault()?.Trim()}");
                }
            }
        }
        catch
        {
            // Leitura do log System pode estar restrita caso executado sem elevação em algumas políticas
        }
    }

    private static void BuildChecks(DriversAndServicesResult result)
    {
        int unsignedCount = result.InstalledDrivers.Count(d => d.IsDigitallySigned == false);

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "DRV-SIGNED",
            Category = "Drivers e Serviços",
            Name = "Inventário e Assinaturas Digitais de Drivers",
            CurrentValue = result.InstalledDrivers.Count > 0
                ? $"{result.InstalledDrivers.Count} drivers inventariados ({unsignedCount} sem assinatura digital identificada)"
                : "Não disponível",
            Status = result.InstalledDrivers.Count == 0
                ? DiagnosticStatus.INDISPONIVEL
                : unsignedCount > 0
                    ? DiagnosticStatus.ATENCAO
                    : DiagnosticStatus.NORMAL,
            Purpose = "Verifica versões, fabricantes e assinaturas digitais dos drivers PnP instalados.",
            TechnicalDetails = unsignedCount > 0
                ? $"Foram encontrados {unsignedCount} driver(s) sem assinatura digital WHQL confirmada. Um driver sem assinatura não é classificado automaticamente como malware ou ferramenta de trapaça, mas pode ser bloqueado por sistemas anticheat."
                : "Todos os drivers PnP consultados possuem assinatura digital válida.",
            OfficialRecommendation = unsignedCount > 0
                ? "Revise os drivers sem assinatura na aba 'Drivers e serviços' e atualize-os com pacotes WHQL oficiais do fabricante."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "DRV-DEVICES-ERR",
            Category = "Drivers e Serviços",
            Name = "Dispositivos com Erros no Gerenciador de Dispositivos",
            CurrentValue = result.DevicesWithErrors.Count == 0
                ? "Nenhum dispositivo com erro reportado (0 erros)"
                : $"{result.DevicesWithErrors.Count} dispositivo(s) com código de erro PnP",
            Status = result.DevicesWithErrors.Count == 0
                ? DiagnosticStatus.NORMAL
                : result.DevicesWithErrors.Any(d => d.Status == DiagnosticStatus.ERRO)
                    ? DiagnosticStatus.ERRO
                    : DiagnosticStatus.ATENCAO,
            Purpose = "Identifica componentes de hardware que reportam códigos de erro no Gerenciador de Dispositivos do Windows.",
            TechnicalDetails = result.DevicesWithErrors.Count > 0
                ? string.Join(" | ", result.DevicesWithErrors.Select(d => $"{d.DeviceName} ({d.ErrorExplanation})"))
                : "Todos os dispositivos PnP estão operacionais.",
            OfficialRecommendation = result.DevicesWithErrors.Count > 0
                ? "Reinstale os drivers oficiais dos dispositivos listados com erro no Gerenciador de Dispositivos."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "DRV-SERVICES",
            Category = "Drivers e Serviços",
            Name = "Serviços do Windows e de Terceiros",
            CurrentValue = $"{result.WindowsServices.Count} serviços do Windows | {result.ThirdPartyServices.Count} serviços de terceiros",
            Status = (result.WindowsServices.Count + result.ThirdPartyServices.Count) > 0
                ? DiagnosticStatus.NORMAL
                : DiagnosticStatus.INDISPONIVEL,
            Purpose = "Lista serviços nativos do sistema operacional e serviços instalados por softwares de terceiros.",
            TechnicalDetails = $"Total de serviços analisados: {result.WindowsServices.Count + result.ThirdPartyServices.Count}.",
            OfficialRecommendation = "Evite desativar serviços essenciais do Windows ou dos sistemas anticheat (EasyAntiCheat, BEService)."
        });
    }
}
