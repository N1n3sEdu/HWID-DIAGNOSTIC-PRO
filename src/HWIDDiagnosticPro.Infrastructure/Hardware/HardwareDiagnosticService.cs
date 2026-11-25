using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;
using HWIDDiagnosticPro.Infrastructure.Helpers;

namespace HWIDDiagnosticPro.Infrastructure.Hardware;

/// <summary>
/// Serviço oficial de identificação de hardware (Seção 6).
/// Consulta Placa-mãe, BIOS, Processador, Memória RAM, Armazenamento (SSD/HDD) e Placa de Vídeo
/// utilizando WMI, CIM e APIs documentadas do Windows.
/// </summary>
public sealed class HardwareDiagnosticService : IHardwareDiagnosticService
{
    private readonly ILocalLoggerService _logger;

    public HardwareDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public Task<HardwareSnapshot> AnalyzeHardwareAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var snapshot = new HardwareSnapshot
            {
                ComputerName = string.IsNullOrWhiteSpace(Environment.MachineName) ? "Não disponível" : Environment.MachineName,
                WindowsVersion = GetWindowsVersionDisplay()
            };

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Motherboard = QueryMotherboard();

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Bios = QueryBios();

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Processor = QueryProcessor();

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Ram = QueryRam();

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.StorageDevices = QueryStorageDevices();

            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Gpus = QueryGpus();

            BuildHardwareChecks(snapshot);
            snapshot.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(snapshot.Checks);

            _logger.LogInfo("Hardware", $"Inventário de hardware concluído ({snapshot.Checks.Count} verificações).");
            return snapshot;
        }, cancellationToken);
    }

    private string GetWindowsVersionDisplay()
    {
        if (!OperatingSystem.IsWindows())
            return "Não disponível";

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var productName = key.GetValue("ProductName")?.ToString() ?? "Windows";
                var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString() ?? "";
                var buildStr = key.GetValue("CurrentBuildNumber")?.ToString() ?? key.GetValue("CurrentBuild")?.ToString() ?? "";
                var ubr = key.GetValue("UBR")?.ToString() ?? "";

                if (int.TryParse(buildStr, out var buildNum) && buildNum >= 22000 && productName.Contains("Windows 10"))
                {
                    productName = productName.Replace("Windows 10", "Windows 11");
                }

                var fullBuild = string.IsNullOrWhiteSpace(ubr) ? buildStr : $"{buildStr}.{ubr}";
                return $"{productName} {displayVersion} (Build {fullBuild})".Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Hardware", $"Erro ao consultar versão do Windows via Registro: {ex.Message}");
        }

        return RuntimeInformation.OSDescription;
    }

    private MotherboardInfo QueryMotherboard()
    {
        var info = new MotherboardInfo();
        if (!OperatingSystem.IsWindows())
            return info;

        try
        {
            var rows = WmiQueryHelper.Query("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard");
            var biosRows = WmiQueryHelper.Query("SELECT SMBIOSPresent, SMBIOSMajorVersion, SMBIOSMinorVersion FROM Win32_BIOS");

            if (rows.Count > 0)
            {
                var row = rows[0];
                info.Manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                info.Model = WmiQueryHelper.GetString(row, "Product");
                info.Version = WmiQueryHelper.GetString(row, "Version");
                var rawSerial = WmiQueryHelper.GetString(row, "SerialNumber");
                info.RawSerialNumber = rawSerial;
                info.MaskedSerialNumber = PrivacyMasker.MaskSerialNumber(rawSerial);
            }

            if (biosRows.Count > 0)
            {
                var bRow = biosRows[0];
                var smbiosPresent = WmiQueryHelper.GetNullableBool(bRow, "SMBIOSPresent");
                var major = WmiQueryHelper.GetInt32(bRow, "SMBIOSMajorVersion");
                var minor = WmiQueryHelper.GetInt32(bRow, "SMBIOSMinorVersion");
                info.SmbiosAvailable = smbiosPresent == true || major > 0;
                info.SmbiosVersion = major > 0 ? $"{major}.{minor}" : (info.SmbiosAvailable ? "Disponível" : "Não disponível");
            }

            bool hasValidModel = !info.Model.Equals("Não disponível", StringComparison.OrdinalIgnoreCase);
            bool hasValidSerial = !info.MaskedSerialNumber.Equals("Não disponível", StringComparison.OrdinalIgnoreCase);

            if (hasValidModel && info.SmbiosAvailable && hasValidSerial)
            {
                info.Status = DiagnosticStatus.NORMAL;
                info.Notes = "Informações SMBIOS da placa-mãe íntegras e acessíveis.";
            }
            else if (hasValidModel && info.SmbiosAvailable)
            {
                info.Status = DiagnosticStatus.ATENCAO;
                info.Notes = "Placa-mãe identificada via SMBIOS, porém o fabricante não preencheu o número de série no firmware OEM.";
            }
            else
            {
                info.Status = DiagnosticStatus.INDISPONIVEL;
                info.Notes = "Não foi possível obter todas as tabelas SMBIOS da placa-mãe.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar placa-mãe via WMI.", ex);
        }

        return info;
    }

    private BiosInfo QueryBios()
    {
        var info = new BiosInfo();
        if (!OperatingSystem.IsWindows())
            return info;

        try
        {
            var biosRows = WmiQueryHelper.Query("SELECT Manufacturer, SMBIOSBIOSVersion, Version, ReleaseDate FROM Win32_BIOS");
            if (biosRows.Count > 0)
            {
                var row = biosRows[0];
                info.Manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                info.Version = WmiQueryHelper.GetString(row, "SMBIOSBIOSVersion", WmiQueryHelper.GetString(row, "Version"));
                info.ReleaseDate = WmiQueryHelper.FormatWmiDate(WmiQueryHelper.GetString(row, "ReleaseDate"));
            }

            info.BootMode = QueryBootMode();
            info.SecureBootStatus = QuerySecureBootStatus();

            var cspRows = WmiQueryHelper.Query("SELECT UUID FROM Win32_ComputerSystemProduct");
            if (cspRows.Count > 0)
            {
                var rawUuid = WmiQueryHelper.GetString(cspRows[0], "UUID");
                info.RawUuid = rawUuid;
                info.UuidAvailable = PrivacyMasker.IsValidHardwareIdentifier(rawUuid);
                info.MaskedUuid = PrivacyMasker.MaskUuid(rawUuid);
            }

            if (info.BootMode.Equals("UEFI", StringComparison.OrdinalIgnoreCase) &&
                info.SecureBootStatus.Equals("Ativado", StringComparison.OrdinalIgnoreCase) &&
                info.UuidAvailable)
            {
                info.Status = DiagnosticStatus.NORMAL;
                info.Notes = "Firmware UEFI com Secure Boot ativo e UUID SMBIOS disponível.";
            }
            else if (!info.Manufacturer.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
            {
                info.Status = DiagnosticStatus.ATENCAO;
                info.Notes = "Recomenda-se utilizar modo UEFI com Secure Boot ativado para compatibilidade total com anticheats modernos.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar BIOS via WMI/Registro.", ex);
        }

        return info;
    }

    [SupportedOSPlatform("windows")]
    private static string QueryBootMode()
    {
        try
        {
            using var secureBootKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (secureBootKey != null)
                return "UEFI";

            using var setupKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control");
            var fwType = setupKey?.GetValue("PEFirmwareType");
            if (fwType != null && Convert.ToInt32(fwType) == 2)
                return "UEFI";
            if (fwType != null && Convert.ToInt32(fwType) == 1)
                return "Legacy";
        }
        catch
        {
            // Fallback
        }
        return "Não disponível";
    }

    [SupportedOSPlatform("windows")]
    private static string QuerySecureBootStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var val = key?.GetValue("UEFISecureBootEnabled");
            if (val != null)
            {
                return Convert.ToInt32(val) == 1 ? "Ativado" : "Desativado";
            }
        }
        catch
        {
            // Fallback
        }
        return "Não disponível";
    }

    private ProcessorInfo QueryProcessor()
    {
        var info = new ProcessorInfo();
        if (!OperatingSystem.IsWindows())
            return info;

        try
        {
            var rows = WmiQueryHelper.Query(
                "SELECT Manufacturer, Name, Architecture, MaxClockSpeed, CurrentClockSpeed, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");

            if (rows.Count > 0)
            {
                var row = rows[0];
                info.Manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                info.Model = WmiQueryHelper.GetString(row, "Name");
                var archCode = WmiQueryHelper.GetInt32(row, "Architecture", -1);
                info.Architecture = archCode switch
                {
                    9 => "x64 (64 bits)",
                    12 => "ARM64",
                    0 => "x86 (32 bits)",
                    _ => RuntimeInformation.OSArchitecture.ToString()
                };
                info.MaxClockSpeedMHz = WmiQueryHelper.GetInt32(row, "MaxClockSpeed");
                info.CurrentClockSpeedMHz = WmiQueryHelper.GetInt32(row, "CurrentClockSpeed");
                info.FrequencyDisplay = info.MaxClockSpeedMHz > 0
                    ? $"{info.MaxClockSpeedMHz} MHz ({info.MaxClockSpeedMHz / 1000.0:0.00} GHz)"
                    : "Não disponível";
                info.Cores = WmiQueryHelper.GetInt32(row, "NumberOfCores", Environment.ProcessorCount / 2);
                info.Threads = WmiQueryHelper.GetInt32(row, "NumberOfLogicalProcessors", Environment.ProcessorCount);
                info.TemperatureDisplay = QueryCpuTemperature();
                info.Status = info.Cores >= 4 ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar processador via WMI.", ex);
        }

        return info;
    }

    [SupportedOSPlatform("windows")]
    private static string QueryCpuTemperature()
    {
        try
        {
            var thermalRows = WmiQueryHelper.Query(
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature",
                @"\\.\root\WMI",
                TimeSpan.FromSeconds(3));

            if (thermalRows.Count > 0)
            {
                var rawKelvinTenths = WmiQueryHelper.GetInt32(thermalRows[0], "CurrentTemperature");
                if (rawKelvinTenths > 2732)
                {
                    var celsius = (rawKelvinTenths / 10.0) - 273.15;
                    if (celsius is > 5 and < 125)
                        return $"{celsius:0.0} °C";
                }
            }

            var perfRows = WmiQueryHelper.Query(
                "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation",
                @"\\.\root\CIMV2",
                TimeSpan.FromSeconds(3));

            if (perfRows.Count > 0)
            {
                var tempKelvin = WmiQueryHelper.GetInt32(perfRows[0], "Temperature");
                if (tempKelvin > 273)
                {
                    var celsius = tempKelvin - 273.15;
                    if (celsius is > 5 and < 125)
                        return $"{celsius:0.0} °C";
                }
            }
        }
        catch
        {
            // Sensores térmicos ACPI muitas vezes não são expostos sem driver OEM específico
        }

        return "Não disponível";
    }

    private RamSummaryInfo QueryRam()
    {
        var summary = new RamSummaryInfo();
        if (!OperatingSystem.IsWindows())
            return summary;

        try
        {
            var rows = WmiQueryHelper.Query(
                "SELECT Manufacturer, Capacity, ConfiguredClockSpeed, Speed, SMBIOSMemoryType, MemoryType, DeviceLocator, BankLabel, SerialNumber FROM Win32_PhysicalMemory");

            var manufacturers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var slots = new List<string>();
            int maxSpeed = 0;
            double totalGb = 0;

            foreach (var row in rows)
            {
                var capBytes = WmiQueryHelper.GetInt64(row, "Capacity");
                var capGb = capBytes > 0 ? Math.Round(capBytes / (1024.0 * 1024.0 * 1024.0), 2) : 0;
                var speed = WmiQueryHelper.GetInt32(row, "ConfiguredClockSpeed");
                if (speed <= 0)
                    speed = WmiQueryHelper.GetInt32(row, "Speed");

                var smbiosType = WmiQueryHelper.GetInt32(row, "SMBIOSMemoryType");
                if (smbiosType <= 0)
                    smbiosType = WmiQueryHelper.GetInt32(row, "MemoryType");

                var memTypeStr = MapSmbiosMemoryType(smbiosType);
                var manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                var locator = WmiQueryHelper.GetString(row, "DeviceLocator");
                var bank = WmiQueryHelper.GetString(row, "BankLabel");
                var serial = PrivacyMasker.MaskSerialNumber(WmiQueryHelper.GetString(row, "SerialNumber"));

                var module = new RamModuleInfo
                {
                    Slot = locator,
                    BankLabel = bank,
                    Manufacturer = manufacturer,
                    CapacityGB = capGb,
                    ConfiguredSpeedMHz = speed,
                    MemoryType = memTypeStr,
                    MaskedSerialNumber = serial
                };

                summary.Modules.Add(module);
                totalGb += capGb;
                if (speed > maxSpeed) maxSpeed = speed;
                if (!manufacturer.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
                    manufacturers.Add(manufacturer);
                if (!memTypeStr.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
                    types.Add(memTypeStr);
                if (!locator.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
                    slots.Add(locator);
            }

            summary.TotalCapacityGB = Math.Round(totalGb, 2);
            summary.ModuleCount = summary.Modules.Count;
            summary.ManufacturerSummary = manufacturers.Count > 0 ? string.Join(", ", manufacturers) : "Não disponível";
            summary.ConfiguredSpeedDisplay = maxSpeed > 0 ? $"{maxSpeed} MHz" : "Não disponível";
            summary.MemoryType = types.Count > 0 ? string.Join(", ", types) : "Não disponível";
            summary.UsedSlotsIdentification = slots.Count > 0 ? string.Join(", ", slots) : "Não disponível";

            if (summary.TotalCapacityGB >= 8)
                summary.Status = DiagnosticStatus.NORMAL;
            else if (summary.TotalCapacityGB > 0)
                summary.Status = DiagnosticStatus.ATENCAO;
            else
                summary.Status = DiagnosticStatus.INDISPONIVEL;
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar memória RAM via WMI.", ex);
        }

        return summary;
    }

    private static string MapSmbiosMemoryType(int code) => code switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => "Não disponível"
    };

    private List<StorageDeviceInfo> QueryStorageDevices()
    {
        var list = new List<StorageDeviceInfo>();
        if (!OperatingSystem.IsWindows())
            return list;

        try
        {
            // Consultar espaço utilizado e disponível nos volumes locais
            double totalUsedGb = 0;
            double totalFreeGb = 0;
            var volumeSummaries = new List<string>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                    {
                        var total = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        var free = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        var used = Math.Max(0, total - free);
                        totalUsedGb += used;
                        totalFreeGb += free;
                        volumeSummaries.Add($"{drive.Name} ({free:0.#} GB livres de {total:0.#} GB)");
                    }
                }
                catch
                {
                    // Ignorar unidades inacessíveis
                }
            }

            // Consultar MSFT_PhysicalDisk no namespace Storage para BusType (NVMe/SATA/USB) e HealthStatus
            var physicalDisks = WmiQueryHelper.Query(
                "SELECT FriendlyName, SerialNumber, BusType, HealthStatus, MediaType FROM MSFT_PhysicalDisk",
                @"\\.\root\Microsoft\Windows\Storage",
                TimeSpan.FromSeconds(4));

            var diskRows = WmiQueryHelper.Query(
                "SELECT Model, Manufacturer, Size, InterfaceType, PNPDeviceID, SerialNumber, Status FROM Win32_DiskDrive");

            for (int i = 0; i < diskRows.Count; i++)
            {
                var row = diskRows[i];
                var model = WmiQueryHelper.GetString(row, "Model");
                var manufacturer = WmiQueryHelper.GetString(row, "Manufacturer");
                if (manufacturer.Contains("Standard disk drives", StringComparison.OrdinalIgnoreCase) && !model.Equals("Não disponível"))
                {
                    manufacturer = ExtractVendorFromModel(model);
                }

                var sizeBytes = WmiQueryHelper.GetInt64(row, "Size");
                var capGb = sizeBytes > 0 ? Math.Round(sizeBytes / (1024.0 * 1024.0 * 1024.0), 2) : 0;
                var rawInterface = WmiQueryHelper.GetString(row, "InterfaceType");
                var pnpId = WmiQueryHelper.GetString(row, "PNPDeviceID");
                var rawSerial = WmiQueryHelper.GetString(row, "SerialNumber");
                var wmiStatus = WmiQueryHelper.GetString(row, "Status", "OK");

                var busInterface = DetermineStorageInterface(model, rawInterface, pnpId, physicalDisks);
                var health = DetermineDiskHealth(model, wmiStatus, physicalDisks);

                var device = new StorageDeviceInfo
                {
                    Manufacturer = manufacturer,
                    Model = model,
                    CapacityGB = capGb,
                    InterfaceType = busInterface,
                    RawSerialNumber = rawSerial,
                    MaskedSerialNumber = PrivacyMasker.MaskSerialNumber(rawSerial),
                    HealthStatus = health,
                    SmartStatus = wmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase)
                        ? "SMART Operacional (Sem alerta iminente reportado pelo Windows)"
                        : $"Alerta SMART: {wmiStatus}",
                    UsedSpaceGB = i == 0 ? Math.Round(totalUsedGb, 2) : 0,
                    FreeSpaceGB = i == 0 ? Math.Round(totalFreeGb, 2) : 0,
                    TemperatureDisplay = "Não disponível",
                    VolumesDescription = i == 0 && volumeSummaries.Count > 0
                        ? string.Join(" | ", volumeSummaries)
                        : "Unidade física secundária",
                    Status = wmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase)
                        ? DiagnosticStatus.NORMAL
                        : DiagnosticStatus.ERRO
                };

                list.Add(device);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar dispositivos de armazenamento via WMI.", ex);
        }

        return list;
    }

    private static string ExtractVendorFromModel(string model)
    {
        var firstWord = model.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstWord) ? "Não disponível" : firstWord;
    }

    private static string DetermineStorageInterface(
        string model,
        string wmiInterface,
        string pnpId,
        List<Dictionary<string, object?>> physicalDisks)
    {
        foreach (var pd in physicalDisks)
        {
            var friendly = WmiQueryHelper.GetString(pd, "FriendlyName", "");
            if (!string.IsNullOrWhiteSpace(friendly) && model.Contains(friendly, StringComparison.OrdinalIgnoreCase))
            {
                var busType = WmiQueryHelper.GetInt32(pd, "BusType", 0);
                switch (busType)
                {
                    case 17: return "NVMe";
                    case 11:
                    case 3:
                    case 8: return "SATA";
                    case 7: return "USB";
                }
            }
        }

        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
            pnpId.Contains("NVME", StringComparison.OrdinalIgnoreCase))
            return "NVMe";

        if (wmiInterface.Contains("USB", StringComparison.OrdinalIgnoreCase) ||
            pnpId.Contains("USBSTOR", StringComparison.OrdinalIgnoreCase))
            return "USB";

        if (wmiInterface.Contains("IDE", StringComparison.OrdinalIgnoreCase) ||
            wmiInterface.Contains("SCSI", StringComparison.OrdinalIgnoreCase) ||
            wmiInterface.Contains("SATA", StringComparison.OrdinalIgnoreCase))
            return "SATA";

        return string.IsNullOrWhiteSpace(wmiInterface) ? "Não disponível" : wmiInterface;
    }

    private static string DetermineDiskHealth(string model, string wmiStatus, List<Dictionary<string, object?>> physicalDisks)
    {
        foreach (var pd in physicalDisks)
        {
            var friendly = WmiQueryHelper.GetString(pd, "FriendlyName", "");
            if (!string.IsNullOrWhiteSpace(friendly) && model.Contains(friendly, StringComparison.OrdinalIgnoreCase))
            {
                var healthCode = WmiQueryHelper.GetInt32(pd, "HealthStatus", -1);
                return healthCode switch
                {
                    0 => "Saudável",
                    1 => "Atenção",
                    2 => "Crítico / Não saudável",
                    _ => wmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "Saudável" : wmiStatus
                };
            }
        }

        return wmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "Saudável" : wmiStatus;
    }

    private List<GpuInfo> QueryGpus()
    {
        var list = new List<GpuInfo>();
        if (!OperatingSystem.IsWindows())
            return list;

        try
        {
            var rows = WmiQueryHelper.Query(
                "SELECT AdapterCompatibility, Name, DriverVersion, DriverDate, AdapterRAM, Status, ConfigManagerErrorCode FROM Win32_VideoController");

            string dxSupport = File.Exists(Path.Combine(Environment.SystemDirectory, "d3d12.dll"))
                ? "DirectX 12 / DirectX 11 Compatível"
                : File.Exists(Path.Combine(Environment.SystemDirectory, "d3d11.dll"))
                    ? "DirectX 11 Compatível"
                    : "Não disponível";

            foreach (var row in rows)
            {
                var rawRamBytes = WmiQueryHelper.GetInt64(row, "AdapterRAM");
                double vramMb = rawRamBytes > 0 ? Math.Round(rawRamBytes / (1024.0 * 1024.0), 0) : 0;
                var errorCode = WmiQueryHelper.GetUInt32(row, "ConfigManagerErrorCode");
                var statusStr = WmiQueryHelper.GetString(row, "Status");

                var gpu = new GpuInfo
                {
                    Manufacturer = WmiQueryHelper.GetString(row, "AdapterCompatibility"),
                    Model = WmiQueryHelper.GetString(row, "Name"),
                    DriverVersion = WmiQueryHelper.GetString(row, "DriverVersion"),
                    DriverDate = WmiQueryHelper.FormatWmiDate(WmiQueryHelper.GetString(row, "DriverDate")),
                    DedicatedMemoryMB = vramMb,
                    DirectXCompatibility = dxSupport,
                    DeviceStatus = errorCode == 0 ? $"Operacional ({statusStr})" : $"Código de Erro PnP {errorCode}",
                    ConfigManagerErrorCode = errorCode,
                    Status = errorCode == 0 ? DiagnosticStatus.NORMAL : DiagnosticStatus.ERRO
                };

                list.Add(gpu);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Hardware", "Falha ao consultar placa de vídeo via WMI.", ex);
        }

        return list;
    }

    private static void BuildHardwareChecks(HardwareSnapshot snapshot)
    {
        snapshot.Checks.Add(new DiagnosticCheckItem
        {
            Id = "HW-MB",
            Category = "Hardware",
            Name = "Placa-mãe e SMBIOS",
            CurrentValue = $"{snapshot.Motherboard.Manufacturer} {snapshot.Motherboard.Model} | Série: {snapshot.Motherboard.MaskedSerialNumber} | SMBIOS: {snapshot.Motherboard.SmbiosAvailabilityText}",
            Status = snapshot.Motherboard.Status,
            Purpose = "Identifica o modelo da placa-mãe e a integridade das tabelas SMBIOS utilizadas pelo Windows.",
            TechnicalDetails = snapshot.Motherboard.Notes,
            OfficialRecommendation = snapshot.Motherboard.Status == DiagnosticStatus.NORMAL
                ? "Nenhuma ação necessária."
                : "Mantenha a BIOS da placa-mãe atualizada com a versão oficial do fabricante."
        });

        snapshot.Checks.Add(new DiagnosticCheckItem
        {
            Id = "HW-BIOS",
            Category = "BIOS e Placa-mãe",
            Name = "BIOS / Modo UEFI / UUID",
            CurrentValue = $"{snapshot.Bios.Manufacturer} v{snapshot.Bios.Version} ({snapshot.Bios.ReleaseDate}) | Modo: {snapshot.Bios.BootMode} | Secure Boot: {snapshot.Bios.SecureBootStatus} | UUID: {snapshot.Bios.MaskedUuid}",
            Status = snapshot.Bios.Status,
            Purpose = "Verifica a versão da BIOS, modo de inicialização UEFI/Legacy, Secure Boot e presença do UUID SMBIOS.",
            TechnicalDetails = snapshot.Bios.Notes,
            OfficialRecommendation = snapshot.Bios.Status == DiagnosticStatus.NORMAL
                ? "Nenhuma ação necessária."
                : "Configure a inicialização em modo UEFI com Secure Boot ativado nas opções da BIOS."
        });

        snapshot.Checks.Add(new DiagnosticCheckItem
        {
            Id = "HW-CPU",
            Category = "Hardware",
            Name = "Processador (CPU)",
            CurrentValue = $"{snapshot.Processor.Model} ({snapshot.Processor.Cores} núcleos / {snapshot.Processor.Threads} threads @ {snapshot.Processor.FrequencyDisplay})",
            Status = snapshot.Processor.Status,
            Purpose = "Analisa a capacidade de processamento e arquitetura x64 exigida pelo Fortnite e sistemas anticheat.",
            TechnicalDetails = $"Fabricante: {snapshot.Processor.Manufacturer} | Arquitetura: {snapshot.Processor.Architecture} | Temperatura: {snapshot.Processor.TemperatureDisplay}",
            OfficialRecommendation = "Verifique o plano de energia do Windows e o resfriamento do processador durante jogos."
        });

        snapshot.Checks.Add(new DiagnosticCheckItem
        {
            Id = "HW-RAM",
            Category = "Hardware",
            Name = "Memória RAM",
            CurrentValue = $"{snapshot.Ram.TotalCapacityDisplay} ({snapshot.Ram.ModuleCount} módulo(s) {snapshot.Ram.MemoryType} @ {snapshot.Ram.ConfiguredSpeedDisplay})",
            Status = snapshot.Ram.Status,
            Purpose = "Verifica a capacidade total, frequência configurada e distribuição nos slots de memória.",
            TechnicalDetails = $"Fabricante(s): {snapshot.Ram.ManufacturerSummary} | Slots utilizados: {snapshot.Ram.UsedSlotsIdentification}",
            OfficialRecommendation = snapshot.Ram.TotalCapacityGB >= 8
                ? "Capacidade de memória RAM adequada para o Fortnite (mínimo oficial: 8 GB)."
                : "Recomenda-se no mínimo 8 GB de memória RAM (16 GB recomendado) para evitar travamentos."
        });

        if (snapshot.StorageDevices.Count > 0)
        {
            foreach (var disk in snapshot.StorageDevices)
            {
                snapshot.Checks.Add(new DiagnosticCheckItem
                {
                    Id = $"HW-DISK-{disk.Model}",
                    Category = "SSD e HDD",
                    Name = $"Armazenamento: {disk.Model}",
                    CurrentValue = $"{disk.CapacityDisplay} ({disk.InterfaceType}) | Saúde: {disk.HealthStatus} | Série: {disk.MaskedSerialNumber}",
                    Status = disk.Status,
                    Purpose = "Avalia a saúde SMART, interface (NVMe/SATA/USB) e espaço livre da unidade de armazenamento.",
                    TechnicalDetails = $"SMART: {disk.SmartStatus} | Volumes: {disk.VolumesDescription}",
                    OfficialRecommendation = disk.Status == DiagnosticStatus.NORMAL
                        ? "Mantenha ao menos 15% de espaço livre no SSD/HDD para atualizações do jogo e paginação."
                        : "Faça backup dos seus dados e execute diagnóstico oficial do fabricante do disco."
                });
            }
        }
        else
        {
            snapshot.Checks.Add(new DiagnosticCheckItem
            {
                Id = "HW-DISK-NONE",
                Category = "SSD e HDD",
                Name = "Unidades de Armazenamento",
                CurrentValue = "Não disponível",
                Status = DiagnosticStatus.INDISPONIVEL,
                Purpose = "Consulta unidades físicas de armazenamento.",
                TechnicalDetails = "Nenhuma unidade retornada pela consulta WMI atual.",
                OfficialRecommendation = "Execute em ambiente Windows com o serviço WMI ativo."
            });
        }

        if (snapshot.Gpus.Count > 0)
        {
            foreach (var gpu in snapshot.Gpus)
            {
                snapshot.Checks.Add(new DiagnosticCheckItem
                {
                    Id = $"HW-GPU-{gpu.Model}",
                    Category = "Placa de Vídeo",
                    Name = $"Placa de Vídeo: {gpu.Model}",
                    CurrentValue = $"Driver {gpu.DriverVersion} ({gpu.DriverDate}) | VRAM: {gpu.DedicatedMemoryDisplay} | {gpu.DirectXCompatibility}",
                    Status = gpu.Status,
                    Purpose = "Verifica o adaptador gráfico, versão do driver WHQL, memória dedicada e suporte DirectX.",
                    TechnicalDetails = $"Fabricante: {gpu.Manufacturer} | Status PnP: {gpu.DeviceStatus}",
                    OfficialRecommendation = gpu.Status == DiagnosticStatus.NORMAL
                        ? "Mantenha o driver gráfico atualizado pelo site oficial (NVIDIA, AMD ou Intel)."
                        : "Reinstale o driver de vídeo oficial para corrigir o código de erro reportado pelo Windows."
                });
            }
        }
        else
        {
            snapshot.Checks.Add(new DiagnosticCheckItem
            {
                Id = "HW-GPU-NONE",
                Category = "Placa de Vídeo",
                Name = "Adaptador Gráfico",
                CurrentValue = "Não disponível",
                Status = DiagnosticStatus.INDISPONIVEL,
                Purpose = "Consulta placas de vídeo instaladas.",
                TechnicalDetails = "Nenhum controlador de vídeo retornado pelo WMI.",
                OfficialRecommendation = "Verifique no Gerenciador de Dispositivos se o driver de vídeo está instalado."
            });
        }
    }
}
