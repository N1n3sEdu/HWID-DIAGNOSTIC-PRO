namespace HWIDDiagnosticPro.Core.Models;

public sealed class NetworkAdapterDetail
{
    public string Name { get; set; } = "Não disponível";
    public string Description { get; set; } = "Não disponível";
    public string InterfaceType { get; set; } = "Não disponível";
    public bool IsActive { get; set; }
    public string LocalIpAddress { get; set; } = "Não disponível";
    public string MaskedLocalIpAddress { get; set; } = "Não disponível";
    public string DefaultGateway { get; set; } = "Não disponível";
    public string DnsServers { get; set; } = "Não disponível";
    public string MaskedMacAddress { get; set; } = "Não disponível";
    public string RawMacAddress { get; set; } = "Não disponível";
    public string DhcpStatus { get; set; } = "Não disponível";
    public string NominalSpeedDisplay { get; set; } = "Não disponível";
    public bool IsVpnAdapter { get; set; }
    public string VpnNote => IsVpnAdapter
        ? "Adaptador virtual/VPN identificado (não é trapaça, porém pode causar desconexão em partidas competitivas)."
        : "Adaptador padrão";
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.NORMAL;
    public string StatusDisplay => Status.ToDisplayText();
    public string StatusColorHex => Status.ToColorHex();
}

public sealed class NetworkDiagnosticResult
{
    public List<NetworkAdapterDetail> ActiveAdapters { get; set; } = [];
    public string ConnectionSummary { get; set; } = "Não disponível";
    public string ProxyConfiguration { get; set; } = "Não disponível";
    public bool IsProxyEnabled { get; set; }
    public bool HasVpnAdapters { get; set; }
    public string VpnPresenceDescription { get; set; } = "Não disponível";
    public string WindowsFirewallStatus { get; set; } = "Não disponível";
    public double? AverageLatencyMs { get; set; }
    public string LatencyDisplay => AverageLatencyMs.HasValue ? $"{AverageLatencyMs.Value:0.0} ms" : "Não disponível";
    public double? PacketLossPercent { get; set; }
    public string PacketLossDisplay => PacketLossPercent.HasValue ? $"{PacketLossPercent.Value:0.0}%" : "Não disponível";
    public bool DnsResolutionWorking { get; set; }
    public string DnsResolutionStatus { get; set; } = "Não disponível";
    public bool EpicServicesReachable { get; set; }
    public string EpicConnectivityStatus { get; set; } = "Não disponível";
    public string TimeSynchronizationStatus { get; set; } = "Não disponível";
    public bool TimeSyncHealthy { get; set; }
    public List<string> InterferenceNotes { get; set; } = [];
    public List<DiagnosticCheckItem> Checks { get; set; } = [];
    public DiagnosticStatus OverallStatus { get; set; } = DiagnosticStatus.INDISPONIVEL;
    public string OverallStatusDisplay => OverallStatus.ToDisplayText();
    public string OverallStatusColorHex => OverallStatus.ToColorHex();
}
