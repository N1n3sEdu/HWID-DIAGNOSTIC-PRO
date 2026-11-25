namespace HWIDDiagnosticPro.Core.Models;

public sealed class MotherboardInfo
{
    public string Manufacturer { get; set; } = "Não disponível";
    public string Model { get; set; } = "Não disponível";
    public string Version { get; set; } = "Não disponível";
    public string MaskedSerialNumber { get; set; } = "Não disponível";
    public string RawSerialNumber { get; set; } = "Não disponível";
    public bool SmbiosAvailable { get; set; }
    public string SmbiosAvailabilityText => SmbiosAvailable ? "Disponível" : "Não disponível";
    public string SmbiosVersion { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string Notes { get; set; } = string.Empty;
}

public sealed class BiosInfo
{
    public string Manufacturer { get; set; } = "Não disponível";
    public string Version { get; set; } = "Não disponível";
    public string ReleaseDate { get; set; } = "Não disponível";
    public string BootMode { get; set; } = "Não disponível"; // UEFI ou Legacy
    public string SecureBootStatus { get; set; } = "Não disponível";
    public bool UuidAvailable { get; set; }
    public string MaskedUuid { get; set; } = "Não disponível";
    public string RawUuid { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
    public string Notes { get; set; } = string.Empty;
}

public sealed class ProcessorInfo
{
    public string Manufacturer { get; set; } = "Não disponível";
    public string Model { get; set; } = "Não disponível";
    public string Architecture { get; set; } = "Não disponível";
    public string FrequencyDisplay { get; set; } = "Não disponível";
    public int MaxClockSpeedMHz { get; set; }
    public int CurrentClockSpeedMHz { get; set; }
    public int Cores { get; set; }
    public int Threads { get; set; }
    public string TemperatureDisplay { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class RamModuleInfo
{
    public string Slot { get; set; } = "Não disponível";
    public string BankLabel { get; set; } = "Não disponível";
    public string Manufacturer { get; set; } = "Não disponível";
    public double CapacityGB { get; set; }
    public string CapacityDisplay => CapacityGB > 0 ? $"{CapacityGB:0.##} GB" : "Não disponível";
    public int ConfiguredSpeedMHz { get; set; }
    public string SpeedDisplay => ConfiguredSpeedMHz > 0 ? $"{ConfiguredSpeedMHz} MHz" : "Não disponível";
    public string MemoryType { get; set; } = "Não disponível";
    public string MaskedSerialNumber { get; set; } = "Não disponível";
}

public sealed class RamSummaryInfo
{
    public string ManufacturerSummary { get; set; } = "Não disponível";
    public double TotalCapacityGB { get; set; }
    public string TotalCapacityDisplay => TotalCapacityGB > 0 ? $"{TotalCapacityGB:0.##} GB" : "Não disponível";
    public string ConfiguredSpeedDisplay { get; set; } = "Não disponível";
    public int ModuleCount { get; set; }
    public string MemoryType { get; set; } = "Não disponível";
    public string UsedSlotsIdentification { get; set; } = "Não disponível";
    public List<RamModuleInfo> Modules { get; set; } = [];
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class StorageDeviceInfo
{
    public string Manufacturer { get; set; } = "Não disponível";
    public string Model { get; set; } = "Não disponível";
    public double CapacityGB { get; set; }
    public string CapacityDisplay => CapacityGB > 0 ? $"{CapacityGB:0.##} GB" : "Não disponível";
    public string InterfaceType { get; set; } = "Não disponível"; // SATA, NVMe ou USB
    public string MaskedSerialNumber { get; set; } = "Não disponível";
    public string RawSerialNumber { get; set; } = "Não disponível";
    public string HealthStatus { get; set; } = "Não disponível";
    public string SmartStatus { get; set; } = "Não disponível";
    public double UsedSpaceGB { get; set; }
    public string UsedSpaceDisplay => UsedSpaceGB >= 0 && CapacityGB > 0 ? $"{UsedSpaceGB:0.##} GB" : "Não disponível";
    public double FreeSpaceGB { get; set; }
    public string FreeSpaceDisplay => FreeSpaceGB >= 0 && CapacityGB > 0 ? $"{FreeSpaceGB:0.##} GB" : "Não disponível";
    public double UsagePercentage => CapacityGB > 0 ? Math.Round(UsedSpaceGB / CapacityGB * 100.0, 1) : 0;
    public string TemperatureDisplay { get; set; } = "Não disponível";
    public string VolumesDescription { get; set; } = "Não disponível";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class GpuInfo
{
    public string Manufacturer { get; set; } = "Não disponível";
    public string Model { get; set; } = "Não disponível";
    public string DriverVersion { get; set; } = "Não disponível";
    public string DriverDate { get; set; } = "Não disponível";
    public double DedicatedMemoryMB { get; set; }
    public string DedicatedMemoryDisplay => DedicatedMemoryMB > 0
        ? DedicatedMemoryMB >= 1024 ? $"{DedicatedMemoryMB / 1024.0:0.##} GB ({DedicatedMemoryMB:0} MB)" : $"{DedicatedMemoryMB:0} MB"
        : "Não disponível";
    public string DirectXCompatibility { get; set; } = "Não disponível";
    public string DeviceStatus { get; set; } = "Não disponível";
    public uint ConfigManagerErrorCode { get; set; }
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class HardwareSnapshot
{
    public string ComputerName { get; set; } = "Não disponível";
    public string WindowsVersion { get; set; } = "Não disponível";
    public MotherboardInfo Motherboard { get; set; } = new();
    public BiosInfo Bios { get; set; } = new();
    public ProcessorInfo Processor { get; set; } = new();
    public RamSummaryInfo Ram { get; set; } = new();
    public List<StorageDeviceInfo> StorageDevices { get; set; } = [];
    public List<GpuInfo> Gpus { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
}
