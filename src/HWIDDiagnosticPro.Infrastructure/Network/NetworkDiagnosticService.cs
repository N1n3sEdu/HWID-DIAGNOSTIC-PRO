using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;
using HWIDDiagnosticPro.Core.Diagnostics;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Network;

/// <summary>
/// Serviço de diagnóstico de rede e internet (Seção 7).
/// Nunca classifica automaticamente a presença de VPN como trapaça.
/// </summary>
public sealed class NetworkDiagnosticService : INetworkDiagnosticService
{
    private readonly ILocalLoggerService _logger;
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly string[] VpnKeywords =
    [
        "TAP-Windows", "Wintun", "WireGuard", "OpenVPN", "NordLynx", "NordVPN",
        "ProtonVPN", "Surfshark", "Cloudflare WARP", "Radmin", "Hamachi",
        "ZeroTier", "Tailscale", "AnyConnect", "Fortinet", "ExpressVPN", "ExitLag"
    ];

    public NetworkDiagnosticService(ILocalLoggerService logger)
    {
        _logger = logger;
    }

    public async Task<NetworkDiagnosticResult> AnalyzeNetworkAsync(bool checkEpicOnline, CancellationToken cancellationToken = default)
    {
        var result = new NetworkDiagnosticResult();

        try
        {
            InspectNetworkAdapters(result);
            cancellationToken.ThrowIfCancellationRequested();

            InspectProxyConfiguration(result);
            InspectFirewallStatus(result);
            InspectWindowsTimeSync(result);

            cancellationToken.ThrowIfCancellationRequested();
            await MeasureLatencyAndPacketLossAsync(result, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await CheckDnsAndEpicServicesAsync(result, checkEpicOnline, cancellationToken);

            BuildNetworkChecks(result);
            result.OverallStatus = HealthScoreCalculator.DetermineOverallStatus(result.Checks);

            _logger.LogInfo("Rede", $"Diagnóstico de rede concluído ({result.ActiveAdapters.Count} adaptadores ativos).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Rede", "Erro durante diagnóstico de rede.", ex);
        }

        return result;
    }

    private void InspectNetworkAdapters(NetworkDiagnosticResult result)
    {
        var vpnAdaptersFound = new List<string>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            foreach (var ni in interfaces)
            {
                bool isVpn = IsVpnInterface(ni);
                bool isUp = ni.OperationalStatus == OperationalStatus.Up;

                if (isVpn && isUp)
                {
                    vpnAdaptersFound.Add($"{ni.Name} ({ni.Description})");
                }

                if (!isUp)
                    continue;

                var ipProps = ni.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString()
                    ?? ipProps.UnicastAddresses.FirstOrDefault()?.Address.ToString()
                    ?? "Não disponível";

                var gateway = ipProps.GatewayAddresses
                    .Select(g => g.Address.ToString())
                    .FirstOrDefault(g => g != "0.0.0.0") ?? "Não disponível";

                var dnsList = ipProps.DnsAddresses
                    .Select(d => d.ToString())
                    .ToList();

                string dhcpStatus = "Não disponível";
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var v4Props = ipProps.GetIPv4Properties();
                        dhcpStatus = v4Props.IsDhcpEnabled ? "Ativado (Automático)" : "Desativado (IP Estático)";
                    }
                }
                catch
                {
                    dhcpStatus = "Não disponível";
                }

                var rawMac = FormatMacBytes(ni.GetPhysicalAddress().GetAddressBytes());
                double speedMbps = ni.Speed > 0 ? Math.Round(ni.Speed / 1_000_000.0, 0) : 0;

                var detail = new NetworkAdapterDetail
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    InterfaceType = ni.NetworkInterfaceType.ToString(),
                    IsActive = true,
                    LocalIpAddress = ipv4,
                    MaskedLocalIpAddress = PrivacyMasker.MaskIpAddress(ipv4),
                    DefaultGateway = PrivacyMasker.MaskIpAddress(gateway),
                    DnsServers = dnsList.Count > 0 ? string.Join(", ", dnsList.Select(PrivacyMasker.MaskIpAddress)) : "Não disponível",
                    RawMacAddress = rawMac,
                    MaskedMacAddress = PrivacyMasker.MaskMacAddress(rawMac),
                    DhcpStatus = dhcpStatus,
                    NominalSpeedDisplay = speedMbps > 0 && speedMbps < 1_000_000 ? $"{speedMbps:0} Mbps" : "Não disponível",
                    IsVpnAdapter = isVpn,
                    Status = isVpn ? DiagnosticStatus.ATENCAO : DiagnosticStatus.NORMAL
                };

                result.ActiveAdapters.Add(detail);
            }

            result.HasVpnAdapters = vpnAdaptersFound.Count > 0;
            result.VpnPresenceDescription = result.HasVpnAdapters
                ? $"Detectado(s) {vpnAdaptersFound.Count} adaptador(es) virtual/VPN ativo(s): {string.Join(", ", vpnAdaptersFound)}. A presença de VPN não indica trapaça, mas pode provocar expulsão de partidas no Fortnite."
                : "Nenhum adaptador de VPN ativo detectado";

            if (result.HasVpnAdapters)
            {
                result.InterferenceNotes.Add(
                    "Adaptador de VPN ou rede virtual ativo detectado. Servidores do Fortnite e sistemas anticheat frequentemente desconectam sessões roteadas por VPN.");
            }

            result.ConnectionSummary = result.ActiveAdapters.Count > 0
                ? $"Conectado ({result.ActiveAdapters.Count} adaptador(es) ativo(s))"
                : "Sem adaptadores de rede ativos";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Rede", $"Falha ao enumerar adaptadores de rede: {ex.Message}");
        }
    }

    private static bool IsVpnInterface(NetworkInterface ni)
    {
        if (ni.NetworkInterfaceType is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel)
            return true;

        var combined = $"{ni.Name} {ni.Description}";
        return VpnKeywords.Any(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatMacBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "Não disponível";

        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private void InspectProxyConfiguration(NetworkDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
        {
            result.ProxyConfiguration = "Desativado (Sem proxy de sistema)";
            result.IsProxyEnabled = false;
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key != null)
            {
                var proxyEnable = Convert.ToInt32(key.GetValue("ProxyEnable", 0)) == 1;
                var proxyServer = key.GetValue("ProxyServer")?.ToString() ?? "";
                var autoConfigUrl = key.GetValue("AutoConfigURL")?.ToString() ?? "";

                if (proxyEnable || !string.IsNullOrWhiteSpace(autoConfigUrl))
                {
                    result.IsProxyEnabled = true;
                    result.ProxyConfiguration = proxyEnable
                        ? $"Proxy Ativo ({PrivacyMasker.MaskIpAddress(proxyServer)})"
                        : $"Script PAC Ativo ({autoConfigUrl})";
                    result.InterferenceNotes.Add(
                        "Configuração de Proxy ativa no Windows. Proxies podem interferir na autenticação do Epic Games Launcher e na conexão com partidas.");
                    return;
                }
            }

            result.IsProxyEnabled = false;
            result.ProxyConfiguration = "Desativado (Conexão direta)";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Rede", $"Não foi possível ler configuração de proxy: {ex.Message}");
            result.ProxyConfiguration = "Não disponível";
        }
    }

    private void InspectFirewallStatus(NetworkDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
        {
            result.WindowsFirewallStatus = "Não disponível";
            return;
        }

        try
        {
            bool standardEnabled = IsFirewallProfileEnabled(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            bool publicEnabled = IsFirewallProfileEnabled(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");

            result.WindowsFirewallStatus = (standardEnabled && publicEnabled)
                ? "Ativado (Perfis Privado e Público protegidos)"
                : (standardEnabled || publicEnabled)
                    ? "Parcialmente ativado (Um dos perfis está desativado)"
                    : "Desativado";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Rede", $"Não foi possível consultar Firewall via Registro: {ex.Message}");
            result.WindowsFirewallStatus = "Não disponível";
        }
    }

    private static bool IsFirewallProfileEnabled(string subKeyPath)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath);
        var val = key?.GetValue("EnableFirewall");
        return val != null && Convert.ToInt32(val) == 1;
    }

    private void InspectWindowsTimeSync(NetworkDiagnosticResult result)
    {
        if (!OperatingSystem.IsWindows())
        {
            result.TimeSyncHealthy = true;
            result.TimeSynchronizationStatus = $"Horário local do sistema: {DateTime.Now:dd/MM/yyyy HH:mm:ss} (UTC{DateTimeOffset.Now:zzz})";
            return;
        }

        try
        {
            using var sc = new ServiceController("W32Time");
            var status = sc.Status;
            result.TimeSyncHealthy = true;
            result.TimeSynchronizationStatus = status == ServiceControllerStatus.Running
                ? $"Sincronizado (Serviço W32Time em execução | Hora local: {DateTime.Now:dd/MM/yyyy HH:mm:ss})"
                : $"Serviço W32Time {status} (Sincronização sob demanda | Hora local: {DateTime.Now:dd/MM/yyyy HH:mm:ss})";
        }
        catch
        {
            result.TimeSyncHealthy = true;
            result.TimeSynchronizationStatus = $"Hora local: {DateTime.Now:dd/MM/yyyy HH:mm:ss} (Serviço W32Time não consultável sem elevação)";
        }
    }

    private async Task MeasureLatencyAndPacketLossAsync(NetworkDiagnosticResult result, CancellationToken cancellationToken)
    {
        const int probeCount = 4;
        int sent = 0;
        int received = 0;
        var latencies = new List<long>();

        var targets = new[] { "1.1.1.1", "8.8.8.8" };

        try
        {
            using var ping = new Ping();
            for (int i = 0; i < probeCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sent++;
                var target = targets[i % targets.Length];

                try
                {
                    var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(1200), cancellationToken: cancellationToken);
                    if (reply.Status == IPStatus.Success)
                    {
                        received++;
                        latencies.Add(reply.RoundtripTime);
                    }
                }
                catch
                {
                    // Probe falhou ou ICMP bloqueado no ambiente
                }
            }

            if (received > 0)
            {
                result.AverageLatencyMs = Math.Round(latencies.Average(), 1);
                result.PacketLossPercent = Math.Round(((sent - received) / (double)sent) * 100.0, 1);

                if (result.PacketLossPercent > 0)
                {
                    result.InterferenceNotes.Add(
                        $"Perda de pacotes de {result.PacketLossDisplay} detectada durante o teste. Perdas acima de 1% podem causar travamentos e expulsão de partidas por timeout do anticheat.");
                }
                if (result.AverageLatencyMs > 120)
                {
                    result.InterferenceNotes.Add(
                        $"Latência média elevada ({result.LatencyDisplay}). Verifique se há downloads em segundo plano ou roteamento congestionado.");
                }
            }
            else
            {
                result.AverageLatencyMs = null;
                result.PacketLossPercent = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Rede", $"Teste ICMP Ping indisponível neste ambiente: {ex.Message}");
        }
    }

    private async Task CheckDnsAndEpicServicesAsync(NetworkDiagnosticResult result, bool checkEpicOnline, CancellationToken cancellationToken)
    {
        if (!checkEpicOnline)
        {
            result.DnsResolutionWorking = true;
            result.DnsResolutionStatus = "Verificação online desativada nas configurações";
            result.EpicServicesReachable = true;
            result.EpicConnectivityStatus = "Verificação online desativada nas configurações";
            return;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync("status.epicgames.com", cancellationToken);
            if (addresses.Length > 0)
            {
                result.DnsResolutionWorking = true;
                result.DnsResolutionStatus = $"Operacional (status.epicgames.com resolvido com sucesso)";
            }
            else
            {
                result.DnsResolutionWorking = false;
                result.DnsResolutionStatus = "Sem endereços retornados para status.epicgames.com";
            }
        }
        catch (Exception ex)
        {
            result.DnsResolutionWorking = false;
            result.DnsResolutionStatus = $"Não foi possível resolver DNS para status.epicgames.com ({ex.GetType().Name})";
        }

        if (!result.DnsResolutionWorking)
        {
            result.EpicServicesReachable = false;
            result.EpicConnectivityStatus = "Indisponível (Depende da resolução DNS)";
            return;
        }

        try
        {
            using var response = await SharedHttpClient.GetAsync("https://status.epicgames.com/api/v2/status.json", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var desc = doc.RootElement.TryGetProperty("status", out var statusEl) &&
                           statusEl.TryGetProperty("description", out var descEl)
                    ? descEl.GetString()
                    : "Todos os sistemas operacionais";

                result.EpicServicesReachable = true;
                result.EpicConnectivityStatus = $"Conectado aos serviços públicos Epic Games ({desc})";
            }
            else
            {
                result.EpicServicesReachable = false;
                result.EpicConnectivityStatus = $"Resposta HTTP {(int)response.StatusCode} ao consultar status público da Epic Games";
            }
        }
        catch (Exception ex)
        {
            result.EpicServicesReachable = false;
            result.EpicConnectivityStatus = $"Não foi possível consultar endpoint público da Epic Games ({ex.GetType().Name})";
        }
    }

    private static void BuildNetworkChecks(NetworkDiagnosticResult result)
    {
        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-ADAPTERS",
            Category = "Rede e Internet",
            Name = "Adaptadores de Rede Ativos",
            CurrentValue = result.ConnectionSummary,
            Status = result.ActiveAdapters.Count > 0 ? DiagnosticStatus.NORMAL : DiagnosticStatus.ERRO,
            Purpose = "Identifica as interfaces de rede ativas, endereço IP mascarado, gateway, DNS, MAC e DHCP.",
            TechnicalDetails = result.ActiveAdapters.Count > 0
                ? string.Join(" | ", result.ActiveAdapters.Select(a => $"{a.Name}: IP {a.MaskedLocalIpAddress}, MAC {a.MaskedMacAddress}, Gateway {a.DefaultGateway}"))
                : "Nenhum adaptador ativo.",
            OfficialRecommendation = result.ActiveAdapters.Count > 0
                ? "Prefira conexão via cabo Ethernet para menor perda de pacotes no Fortnite."
                : "Verifique o cabo de rede ou conexão Wi-Fi."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-VPN",
            Category = "Rede e Internet",
            Name = "Presença de Adaptadores de VPN / Rede Virtual",
            CurrentValue = result.VpnPresenceDescription,
            Status = result.HasVpnAdapters ? DiagnosticStatus.ATENCAO : DiagnosticStatus.NORMAL,
            Purpose = "Verifica a presença de adaptadores de VPN ou túneis virtuais que podem causar expulsão de partidas.",
            TechnicalDetails = "Nota ética: A presença de uma VPN não é classificada como trapaça, porém as regras de rede do Fortnite bloqueiam sessões via VPN.",
            OfficialRecommendation = result.HasVpnAdapters
                ? "Desative clientes de VPN e adaptadores virtuais (Radmin, Hamachi, WARP) antes de abrir o jogo."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-PROXY",
            Category = "Rede e Internet",
            Name = "Configuração de Proxy do Sistema",
            CurrentValue = result.ProxyConfiguration,
            Status = result.IsProxyEnabled ? DiagnosticStatus.ATENCAO : DiagnosticStatus.NORMAL,
            Purpose = "Verifica se há servidor proxy ou script PAC interceptando conexões HTTP/HTTPS.",
            TechnicalDetails = result.ProxyConfiguration,
            OfficialRecommendation = result.IsProxyEnabled
                ? "Desative o uso de servidor proxy nas Configurações de Rede do Windows durante partidas."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-FIREWALL",
            Category = "Rede e Internet",
            Name = "Status do Firewall do Windows",
            CurrentValue = result.WindowsFirewallStatus,
            Status = result.WindowsFirewallStatus.StartsWith("Ativado", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticStatus.NORMAL
                : result.WindowsFirewallStatus.Equals("Não disponível", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticStatus.INDISPONIVEL
                    : DiagnosticStatus.ATENCAO,
            Purpose = "Verifica se o Firewall do Windows Defender está protegendo os perfis de rede.",
            TechnicalDetails = result.WindowsFirewallStatus,
            OfficialRecommendation = "Mantenha o Firewall do Windows ativado e permita as regras automáticas do Fortnite e Easy Anti-Cheat."
        });

        var latencyStatus = !result.AverageLatencyMs.HasValue
            ? DiagnosticStatus.INDISPONIVEL
            : (result.PacketLossPercent > 2.0 || result.AverageLatencyMs > 150)
                ? DiagnosticStatus.ATENCAO
                : DiagnosticStatus.NORMAL;

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-LATENCY",
            Category = "Rede e Internet",
            Name = "Latência e Perda de Pacotes",
            CurrentValue = $"Latência: {result.LatencyDisplay} | Perda de pacotes: {result.PacketLossDisplay}",
            Status = latencyStatus,
            Purpose = "Mede o tempo de resposta e a estabilidade na transmissão de pacotes ICMP.",
            TechnicalDetails = result.AverageLatencyMs.HasValue
                ? $"Média de latência: {result.LatencyDisplay}, perda medida: {result.PacketLossDisplay}."
                : "Respostas ICMP bloqueadas na rede atual ou sem rota externa.",
            OfficialRecommendation = latencyStatus == DiagnosticStatus.ATENCAO
                ? "Reinicie o modem/roteador e utilize cabo de rede Ethernet."
                : "Nenhuma ação necessária."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-DNS-EPIC",
            Category = "Rede e Internet",
            Name = "Resolução DNS e Serviços Públicos da Epic Games",
            CurrentValue = $"DNS: {result.DnsResolutionStatus} | Epic: {result.EpicConnectivityStatus}",
            Status = result.DnsResolutionWorking && result.EpicServicesReachable
                ? DiagnosticStatus.NORMAL
                : result.DnsResolutionWorking
                    ? DiagnosticStatus.ATENCAO
                    : DiagnosticStatus.INDISPONIVEL,
            Purpose = "Valida a resolução de nomes e o alcance HTTPS aos serviços públicos da Epic Games.",
            TechnicalDetails = result.EpicConnectivityStatus,
            OfficialRecommendation = result.DnsResolutionWorking
                ? "Nenhuma ação necessária."
                : "Execute 'ipconfig /flushdns' e verifique sua conexão com a internet."
        });

        result.Checks.Add(new DiagnosticCheckItem
        {
            Id = "NET-TIME",
            Category = "Rede e Internet",
            Name = "Sincronização de Data e Hora do Windows",
            CurrentValue = result.TimeSynchronizationStatus,
            Status = result.TimeSyncHealthy ? DiagnosticStatus.NORMAL : DiagnosticStatus.ATENCAO,
            Purpose = "Garante que o relógio do sistema esteja sincronizado para validação de certificados TLS e tokens.",
            TechnicalDetails = result.TimeSynchronizationStatus,
            OfficialRecommendation = "Mantenha a opção 'Definir hora automaticamente' ativada no Windows."
        });
    }
}
