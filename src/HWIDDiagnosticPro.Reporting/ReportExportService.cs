using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HWIDDiagnosticPro.Core.Helpers;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;
using HWIDDiagnosticPro.Reporting.Helpers;

namespace HWIDDiagnosticPro.Reporting;

/// <summary>
/// Sistema de Relatórios Técnicos (Seção 16).
/// Exporta relatórios completos em PDF, TXT, JSON e HTML com mascaramento automático
/// de endereços IP, MAC, números de série, UUID, caminhos com nome de usuário, tokens e credenciais.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string GenerateJsonReport(FullDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var masked = CreateMaskedCopy(report);
        var rawJson = JsonSerializer.Serialize(masked, JsonOptions);
        return PrivacyMasker.SanitizeTextForReport(rawJson);
    }

    public string GenerateTxtReport(FullDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var masked = CreateMaskedCopy(report);
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("                    HWID DIAGNOSTIC PRO — RELATÓRIO TÉCNICO                     ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"ID do Relatório      : {masked.ReportId}");
        sb.AppendLine($"Data e Horário       : {masked.GeneratedAtDisplay}");
        sb.AppendLine($"Versão do Programa   : {masked.ApplicationVersion}");
        sb.AppendLine($"Modo de Verificação  : {masked.ScanMode}");
        sb.AppendLine($"Índice Técnico Local : {masked.SystemHealthScore}/100");
        sb.AppendLine($"Nota do Índice       : {masked.SystemHealthScoreDisclaimer}");
        sb.AppendLine($"Verificações         : {masked.CompletedChecksCount} concluídas | {masked.ProblemsFoundCount} problemas | {masked.AlertsFoundCount} alertas");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("1. RESUMO DO COMPUTADOR E HARDWARE");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Nome do Computador   : {masked.Hardware.ComputerName}");
        sb.AppendLine($"Versão do Windows    : {masked.Hardware.WindowsVersion}");
        sb.AppendLine($"Placa-mãe            : {masked.Hardware.Motherboard.Manufacturer} {masked.Hardware.Motherboard.Model} (Versão: {masked.Hardware.Motherboard.Version})");
        sb.AppendLine($"Série Placa-mãe      : {masked.Hardware.Motherboard.MaskedSerialNumber} | SMBIOS: {masked.Hardware.Motherboard.SmbiosAvailabilityText}");
        sb.AppendLine($"BIOS                 : {masked.Hardware.Bios.Manufacturer} {masked.Hardware.Bios.Version} ({masked.Hardware.Bios.ReleaseDate})");
        sb.AppendLine($"Modo BIOS / SecBoot  : {masked.Hardware.Bios.BootMode} | Secure Boot: {masked.Hardware.Bios.SecureBootStatus}");
        sb.AppendLine($"UUID SMBIOS (Masc.)  : {masked.Hardware.Bios.MaskedUuid}");
        sb.AppendLine($"Processador          : {masked.Hardware.Processor.Model} ({masked.Hardware.Processor.Cores} núcleos / {masked.Hardware.Processor.Threads} threads @ {masked.Hardware.Processor.FrequencyDisplay})");
        sb.AppendLine($"Memória RAM          : {masked.Hardware.Ram.TotalCapacityDisplay} ({masked.Hardware.Ram.ModuleCount} módulos {masked.Hardware.Ram.MemoryType} @ {masked.Hardware.Ram.ConfiguredSpeedDisplay})");
        sb.AppendLine($"Slots RAM Utilizados : {masked.Hardware.Ram.UsedSlotsIdentification}");

        foreach (var disk in masked.Hardware.StorageDevices)
        {
            sb.AppendLine($"Armazenamento        : {disk.Model} ({disk.CapacityDisplay} | {disk.InterfaceType}) | Saúde: {disk.HealthStatus} | Série: {disk.MaskedSerialNumber}");
            sb.AppendLine($"  SMART / Espaço     : {disk.SmartStatus} | Livre: {disk.FreeSpaceDisplay}");
        }

        foreach (var gpu in masked.Hardware.Gpus)
        {
            sb.AppendLine($"Placa de Vídeo       : {gpu.Model} | Driver: {gpu.DriverVersion} ({gpu.DriverDate}) | VRAM: {gpu.DedicatedMemoryDisplay} | {gpu.DirectXCompatibility}");
        }
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("2. INFORMAÇÕES DE REDE E CONECTIVIDADE");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Status da Conexão    : {masked.Network.ConnectionSummary}");
        sb.AppendLine($"Latência / Perda     : {masked.Network.LatencyDisplay} | Perda de pacotes: {masked.Network.PacketLossDisplay}");
        sb.AppendLine($"Resolução DNS        : {masked.Network.DnsResolutionStatus}");
        sb.AppendLine($"Serviços Epic Games  : {masked.Network.EpicConnectivityStatus}");
        sb.AppendLine($"Configuração Proxy   : {masked.Network.ProxyConfiguration}");
        sb.AppendLine($"Presença de VPN      : {masked.Network.VpnPresenceDescription}");
        sb.AppendLine($"Firewall do Windows  : {masked.Network.WindowsFirewallStatus}");
        sb.AppendLine($"Sincronia de Horário : {masked.Network.TimeSynchronizationStatus}");
        foreach (var adapter in masked.Network.ActiveAdapters)
        {
            sb.AppendLine($"  Adaptador Ativo    : {adapter.Name} ({adapter.Description}) | IP: {adapter.MaskedLocalIpAddress} | MAC: {adapter.MaskedMacAddress} | Gateway: {adapter.DefaultGateway} | DHCP: {adapter.DhcpStatus}");
        }
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("3. STATUS DE SEGURANÇA DO WINDOWS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"TPM 2.0              : {masked.Security.Tpm20Status}");
        sb.AppendLine($"Secure Boot          : {masked.Security.SecureBootStatus}");
        sb.AppendLine($"Microsoft Defender   : {masked.Security.MicrosoftDefenderStatus}");
        sb.AppendLine($"Firewall             : {masked.Security.FirewallStatus}");
        sb.AppendLine($"Integridade Memória  : {masked.Security.MemoryIntegrityStatus}");
        sb.AppendLine($"Isolamento de Núcleo : {masked.Security.CoreIsolationStatus}");
        sb.AppendLine($"Windows Update       : {masked.Security.WindowsUpdateStatus}");
        sb.AppendLine($"Assinatura Drivers   : {masked.Security.DriverSignatureEnforcementStatus}");
        sb.AppendLine($"Status do UAC        : {masked.Security.UacStatus}");
        sb.AppendLine($"Inicialização        : {masked.Security.BootConfigurations}");
        sb.AppendLine($"Reinício Pendente    : {masked.Security.RebootRequiredDisplay}");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("4. STATUS DOS COMPONENTES ANTICHEAT E FORTNITE");
        sb.AppendLine("--------------------------------------------------------------------------------");
        foreach (var ac in masked.AntiCheat.AllComponents)
        {
            sb.AppendLine($"{ac.ComponentName,-21}: [{ac.StatusDisplay}] {ac.InstallationStatus} | Serviço: {ac.RegisteredServiceStatus} | Versão: {ac.LocalVersion}");
            sb.AppendLine($"  Assinatura / Rec.  : {ac.ExecutableDigitalSignature} | {ac.RepairRecommendation}");
        }
        sb.AppendLine($"Fortnite Instalado   : {(masked.Fortnite.IsInstalled ? "Sim" : "Não")} ({masked.Fortnite.InstallLocation})");
        sb.AppendLine($"Arquivos Principais  : {masked.Fortnite.MainFilesStatus}");
        sb.AppendLine($"Requisitos Mínimos   : {masked.Fortnite.MinimumRequirementsStatus}");
        sb.AppendLine($"Visual C++ Runtime   : {masked.Fortnite.VisualCppDependenciesStatus}");
        sb.AppendLine($"Análise Restrições   : {masked.AccessRestrictionAnalysis.OutcomeTitle} ({masked.AccessRestrictionAnalysis.DetailedExplanation})");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("5. ERROS ENCONTRADOS E ALERTAS NAS VERIFICAÇÕES");
        sb.AppendLine("--------------------------------------------------------------------------------");
        var issueChecks = masked.AllChecks.Where(c => c.Status is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO or DiagnosticStatus.ATENCAO).ToList();
        if (issueChecks.Count == 0)
        {
            sb.AppendLine("Nenhum erro ou alerta encontrado nas verificações locais concluídas.");
        }
        else
        {
            foreach (var check in issueChecks)
            {
                sb.AppendLine($"[{check.StatusDisplay}] {check.Category} - {check.Name}");
                sb.AppendLine($"  Valor Atual  : {check.CurrentValue}");
                sb.AppendLine($"  Recomendação : {check.OfficialRecommendation}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("6. LOGS E EVENTOS RELEVANTES");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (masked.SystemIntegrity.RelevantEvents.Count == 0)
        {
            sb.AppendLine("Nenhum evento crítico recente registrado pelo Visualizador de Eventos.");
        }
        else
        {
            foreach (var ev in masked.SystemIntegrity.RelevantEvents.Take(15))
            {
                sb.AppendLine($"[{ev.TimestampDisplay}] [{ev.Severity}] {ev.Source} (ID {ev.EventId}): {ev.Message}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("7. RECOMENDAÇÕES DE CORREÇÃO (CENTRAL DE SOLUÇÕES)");
        sb.AppendLine("--------------------------------------------------------------------------------");
        foreach (var sol in masked.RecommendedSolutions.Where(s => s.IsTriggeredByScan).DefaultIfEmpty(masked.RecommendedSolutions.First()))
        {
            if (sol == null) continue;
            sb.AppendLine($"* {sol.ProblemName} [Prioridade: {sol.PriorityLevel}]");
            sb.AppendLine($"  Causa Possível : {sol.PossibleCause}");
            sb.AppendLine($"  Procedimento   : {sol.RecommendedProcedure.Replace("\n", " ")}");
            sb.AppendLine($"  Documentação   : {sol.OfficialDocumentationUrl}");
        }
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("8. LIMITAÇÕES DO DIAGNÓSTICO E AVISO DE PRIVACIDADE");
        sb.AppendLine("--------------------------------------------------------------------------------");
        foreach (var lim in masked.DiagnosticLimitations)
        {
            sb.AppendLine($"- {lim}");
        }

        return PrivacyMasker.SanitizeTextForReport(sb.ToString());
    }

    public string GenerateHtmlReport(FullDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var masked = CreateMaskedCopy(report);
        var sb = new StringBuilder();

        string E(string? s) => WebUtility.HtmlEncode(PrivacyMasker.SanitizeTextForReport(s ?? "Não disponível"));

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>HWID Diagnostic Pro — Relatório Técnico #{E(masked.ReportId)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root { --bg: #0B0F19; --bg-sec: #111827; --card: #1A2233; --blue: #3B82F6; --text: #F3F4F6; --muted: #9CA3AF; --border: #26324B; }");
        sb.AppendLine("    * { box-sizing: border-box; }");
        sb.AppendLine("    body { margin: 0; padding: 32px; background: var(--bg); color: var(--text); font-family: 'Segoe UI', system-ui, sans-serif; line-height: 1.5; }");
        sb.AppendLine("    .container { max-width: 1180px; margin: 0 auto; }");
        sb.AppendLine("    .header { background: var(--bg-sec); border: 1px solid var(--border); border-left: 5px solid var(--blue); border-radius: 12px; padding: 24px; margin-bottom: 24px; display: flex; justify-content: space-between; align-items: center; }");
        sb.AppendLine("    .title { font-size: 24px; font-weight: 700; color: #fff; margin: 0 0 6px 0; }");
        sb.AppendLine("    .subtitle { color: var(--muted); font-size: 13px; margin: 0; }");
        sb.AppendLine("    .score-box { text-align: right; background: var(--card); padding: 14px 22px; border-radius: 10px; border: 1px solid var(--border); }");
        sb.AppendLine("    .score-val { font-size: 32px; font-weight: 800; color: var(--blue); }");
        sb.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 18px; margin-bottom: 24px; }");
        sb.AppendLine("    .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 20px; }");
        sb.AppendLine("    .card h2 { margin-top: 0; font-size: 16px; color: var(--blue); border-bottom: 1px solid var(--border); padding-bottom: 10px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }");
        sb.AppendLine("    th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--border); vertical-align: top; }");
        sb.AppendLine("    th { color: var(--muted); font-weight: 600; background: var(--bg-sec); }");
        sb.AppendLine("    .badge { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 700; color: #fff; }");
        sb.AppendLine("    .disclaimer { background: var(--bg-sec); border: 1px solid var(--border); border-radius: 10px; padding: 16px; color: var(--muted); font-size: 12.5px; margin-top: 24px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        sb.AppendLine("  <div class=\"header\">");
        sb.AppendLine("    <div>");
        sb.AppendLine("      <h1 class=\"title\">HWID Diagnostic Pro — Relatório Técnico</h1>");
        sb.AppendLine($"      <p class=\"subtitle\">ID: #{E(masked.ReportId)} | Gerado em: {E(masked.GeneratedAtDisplay)} | Versão: {E(masked.ApplicationVersion)} | Modo: {E(masked.ScanMode)}</p>");
        sb.AppendLine($"      <p class=\"subtitle\">Verificações concluídas: {masked.CompletedChecksCount} | Problemas: {masked.ProblemsFoundCount} | Alertas: {masked.AlertsFoundCount}</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"score-box\">");
        sb.AppendLine($"      <div class=\"score-val\">{masked.SystemHealthScore}/100</div>");
        sb.AppendLine("      <div class=\"subtitle\">Integridade Técnica Local</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"grid\">");
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <h2>Resumo do Computador e Hardware</h2>");
        sb.AppendLine("      <table>");
        sb.AppendLine($"        <tr><th>Computador</th><td>{E(masked.Hardware.ComputerName)}</td></tr>");
        sb.AppendLine($"        <tr><th>Windows</th><td>{E(masked.Hardware.WindowsVersion)}</td></tr>");
        sb.AppendLine($"        <tr><th>Processador</th><td>{E(masked.Hardware.Processor.Model)} ({masked.Hardware.Processor.Cores}C/{masked.Hardware.Processor.Threads}T)</td></tr>");
        sb.AppendLine($"        <tr><th>Memória RAM</th><td>{E(masked.Hardware.Ram.TotalCapacityDisplay)} ({E(masked.Hardware.Ram.MemoryType)} @ {E(masked.Hardware.Ram.ConfiguredSpeedDisplay)})</td></tr>");
        sb.AppendLine($"        <tr><th>Placa-mãe</th><td>{E(masked.Hardware.Motherboard.Manufacturer)} {E(masked.Hardware.Motherboard.Model)} (Série: {E(masked.Hardware.Motherboard.MaskedSerialNumber)})</td></tr>");
        sb.AppendLine($"        <tr><th>BIOS / UEFI</th><td>{E(masked.Hardware.Bios.Manufacturer)} {E(masked.Hardware.Bios.Version)} | {E(masked.Hardware.Bios.BootMode)} | UUID: {E(masked.Hardware.Bios.MaskedUuid)}</td></tr>");
        sb.AppendLine($"        <tr><th>Placa de Vídeo</th><td>{E(masked.Hardware.Gpus.FirstOrDefault()?.Model ?? "Não disponível")}</td></tr>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <h2>Rede, Segurança e Anticheat</h2>");
        sb.AppendLine("      <table>");
        sb.AppendLine($"        <tr><th>Conexão / Latência</th><td>{E(masked.Network.ConnectionSummary)} | {E(masked.Network.LatencyDisplay)} (Perda: {E(masked.Network.PacketLossDisplay)})</td></tr>");
        sb.AppendLine($"        <tr><th>VPN / Proxy</th><td>{E(masked.Network.VpnPresenceDescription)} | Proxy: {E(masked.Network.ProxyConfiguration)}</td></tr>");
        sb.AppendLine($"        <tr><th>TPM 2.0 / Secure Boot</th><td>TPM: {E(masked.Security.Tpm20Status)} | Secure Boot: {E(masked.Security.SecureBootStatus)}</td></tr>");
        sb.AppendLine($"        <tr><th>Assinatura de Drivers</th><td>{E(masked.Security.DriverSignatureEnforcementStatus)}</td></tr>");
        sb.AppendLine($"        <tr><th>Easy Anti-Cheat</th><td>{E(masked.AntiCheat.EasyAntiCheat.InstallationStatus)} ({E(masked.AntiCheat.EasyAntiCheat.RegisteredServiceStatus)})</td></tr>");
        sb.AppendLine($"        <tr><th>BattlEye</th><td>{E(masked.AntiCheat.BattlEye.InstallationStatus)} ({E(masked.AntiCheat.BattlEye.RegisteredServiceStatus)})</td></tr>");
        sb.AppendLine($"        <tr><th>Fortnite</th><td>{E(masked.Fortnite.InstallLocation)} ({E(masked.Fortnite.MainFilesStatus)})</td></tr>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"card\" style=\"margin-bottom:24px;\">");
        sb.AppendLine("    <h2>Todas as Verificações Realizadas</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead><tr><th>Status</th><th>Módulo</th><th>Verificação</th><th>Resultado Atual</th><th>Recomendação Oficial</th></tr></thead>");
        sb.AppendLine("      <tbody>");
        foreach (var check in masked.AllChecks)
        {
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td><span class=\"badge\" style=\"background:{check.StatusColorHex}\">{E(check.StatusDisplay)}</span></td>");
            sb.AppendLine($"          <td>{E(check.Category)}</td>");
            sb.AppendLine($"          <td><strong>{E(check.Name)}</strong></td>");
            sb.AppendLine($"          <td>{E(check.CurrentValue)}</td>");
            sb.AppendLine($"          <td>{E(check.OfficialRecommendation)}</td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"card\" style=\"margin-bottom:24px;\">");
        sb.AppendLine("    <h2>Recomendações de Correção</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead><tr><th>Prioridade</th><th>Problema</th><th>Causa Possível</th><th>Procedimento Oficial</th></tr></thead>");
        sb.AppendLine("      <tbody>");
        foreach (var sol in masked.RecommendedSolutions.Where(s => s.IsTriggeredByScan).DefaultIfEmpty(masked.RecommendedSolutions.First()))
        {
            if (sol == null) continue;
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td><span class=\"badge\" style=\"background:{sol.PriorityColorHex}\">{E(sol.PriorityLevel)}</span></td>");
            sb.AppendLine($"          <td><strong>{E(sol.ProblemName)}</strong></td>");
            sb.AppendLine($"          <td>{E(sol.PossibleCause)}</td>");
            sb.AppendLine($"          <td>{E(sol.RecommendedProcedure)}</td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"disclaimer\">");
        sb.AppendLine($"    <strong>Nota sobre a Pontuação ({masked.SystemHealthScore}/100):</strong> {E(masked.SystemHealthScoreDisclaimer)}<br/><br/>");
        sb.AppendLine("    <strong>Limitações do Diagnóstico e Privacidade:</strong><ul>");
        foreach (var lim in masked.DiagnosticLimitations)
        {
            sb.AppendLine($"      <li>{E(lim)}</li>");
        }
        sb.AppendLine("    </ul>");
        sb.AppendLine("  </div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public byte[] GeneratePdfReport(FullDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var masked = CreateMaskedCopy(report);
        var pdf = new SimplePdfDocumentWriter();

        pdf.AddTitle("Relatório Técnico de Diagnóstico — HWID Diagnostic Pro");
        pdf.AddSubtitle($"ID: {masked.ReportId} | Data: {masked.GeneratedAtDisplay} | Versão: {masked.ApplicationVersion} | Integridade Local: {masked.SystemHealthScore}/100");
        pdf.AddParagraph(masked.SystemHealthScoreDisclaimer);

        pdf.AddSectionHeader("1. Resumo do Computador e Hardware");
        pdf.AddKeyValue("Nome do Computador", masked.Hardware.ComputerName);
        pdf.AddKeyValue("Versão do Windows", masked.Hardware.WindowsVersion);
        pdf.AddKeyValue("Placa-mãe", $"{masked.Hardware.Motherboard.Manufacturer} {masked.Hardware.Motherboard.Model} | Série: {masked.Hardware.Motherboard.MaskedSerialNumber} | SMBIOS: {masked.Hardware.Motherboard.SmbiosAvailabilityText}");
        pdf.AddKeyValue("BIOS / UEFI", $"{masked.Hardware.Bios.Manufacturer} {masked.Hardware.Bios.Version} ({masked.Hardware.Bios.ReleaseDate}) | Modo: {masked.Hardware.Bios.BootMode} | Secure Boot: {masked.Hardware.Bios.SecureBootStatus}");
        pdf.AddKeyValue("UUID SMBIOS", masked.Hardware.Bios.MaskedUuid);
        pdf.AddKeyValue("Processador", $"{masked.Hardware.Processor.Model} ({masked.Hardware.Processor.Cores} núcleos / {masked.Hardware.Processor.Threads} threads @ {masked.Hardware.Processor.FrequencyDisplay})");
        pdf.AddKeyValue("Memória RAM", $"{masked.Hardware.Ram.TotalCapacityDisplay} ({masked.Hardware.Ram.ModuleCount} módulos {masked.Hardware.Ram.MemoryType} @ {masked.Hardware.Ram.ConfiguredSpeedDisplay})");
        foreach (var disk in masked.Hardware.StorageDevices)
        {
            pdf.AddKeyValue($"Armazenamento ({disk.InterfaceType})", $"{disk.Model} ({disk.CapacityDisplay}) | Saúde: {disk.HealthStatus} | Série: {disk.MaskedSerialNumber}");
        }
        foreach (var gpu in masked.Hardware.Gpus)
        {
            pdf.AddKeyValue("Placa de Vídeo", $"{gpu.Model} | Driver {gpu.DriverVersion} ({gpu.DriverDate}) | {gpu.DirectXCompatibility}");
        }

        pdf.AddSectionHeader("2. Rede, Segurança e Anticheat");
        pdf.AddKeyValue("Status da Conexão", $"{masked.Network.ConnectionSummary} | Latência: {masked.Network.LatencyDisplay} | Perda: {masked.Network.PacketLossDisplay}");
        pdf.AddKeyValue("Resolução DNS / Epic", $"{masked.Network.DnsResolutionStatus} | {masked.Network.EpicConnectivityStatus}");
        pdf.AddKeyValue("VPN / Proxy", $"{masked.Network.VpnPresenceDescription} | Proxy: {masked.Network.ProxyConfiguration}");
        pdf.AddKeyValue("TPM 2.0 e Secure Boot", $"TPM: {masked.Security.Tpm20Status} | Secure Boot: {masked.Security.SecureBootStatus}");
        pdf.AddKeyValue("Assinatura de Drivers", masked.Security.DriverSignatureEnforcementStatus);
        foreach (var ac in masked.AntiCheat.AllComponents)
        {
            pdf.AddKeyValue(ac.ComponentName, $"[{ac.StatusDisplay}] {ac.InstallationStatus} | Serviço: {ac.RegisteredServiceStatus} | Versão: {ac.LocalVersion}");
        }
        pdf.AddKeyValue("Fortnite", $"{masked.Fortnite.InstallLocation} | {masked.Fortnite.MainFilesStatus}");

        pdf.AddSectionHeader("3. Detalhamento das Verificações Locais");
        foreach (var check in masked.AllChecks)
        {
            pdf.AddCheckRow(
                check.StatusDisplay,
                check.Category,
                check.Name,
                PrivacyMasker.SanitizeTextForReport(check.CurrentValue),
                PrivacyMasker.SanitizeTextForReport(check.OfficialRecommendation));
        }

        pdf.AddSectionHeader("4. Recomendações de Correção");
        foreach (var sol in masked.RecommendedSolutions.Where(s => s.IsTriggeredByScan).DefaultIfEmpty(masked.RecommendedSolutions.First()))
        {
            if (sol == null) continue;
            pdf.AddParagraph($"• {sol.ProblemName} (Prioridade: {sol.PriorityLevel})", isBold: true);
            pdf.AddParagraph($"  Causa: {sol.PossibleCause}");
            pdf.AddParagraph($"  Procedimento: {sol.RecommendedProcedure}");
        }

        pdf.AddSectionHeader("5. Limitações do Diagnóstico");
        foreach (var lim in masked.DiagnosticLimitations)
        {
            pdf.AddParagraph($"- {lim}");
        }

        return pdf.BuildPdfBytes();
    }

    public string GenerateExecutiveSupportSummary(FullDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var masked = CreateMaskedCopy(report);
        var sb = new StringBuilder();

        sb.AppendLine("=== RESUMO TÉCNICO PARA SUPORTE (HWID DIAGNOSTIC PRO) ===");
        sb.AppendLine($"Data/Hora: {masked.GeneratedAtDisplay} | Versão: {masked.ApplicationVersion}");
        sb.AppendLine($"Índice Técnico Local: {masked.SystemHealthScore}/100 (Problemas: {masked.ProblemsFoundCount}, Alertas: {masked.AlertsFoundCount}, Verificações: {masked.CompletedChecksCount})");
        sb.AppendLine($"SO: {masked.Hardware.WindowsVersion}");
        sb.AppendLine($"CPU: {masked.Hardware.Processor.Model} ({masked.Hardware.Processor.Cores}C/{masked.Hardware.Processor.Threads}T)");
        sb.AppendLine($"RAM: {masked.Hardware.Ram.TotalCapacityDisplay} ({masked.Hardware.Ram.MemoryType} @ {masked.Hardware.Ram.ConfiguredSpeedDisplay})");
        sb.AppendLine($"Placa-mãe/BIOS: {masked.Hardware.Motherboard.Manufacturer} {masked.Hardware.Motherboard.Model} | BIOS {masked.Hardware.Bios.Version} ({masked.Hardware.Bios.BootMode})");
        sb.AppendLine($"GPU: {masked.Hardware.Gpus.FirstOrDefault()?.Model ?? "Não disponível"} (Driver: {masked.Hardware.Gpus.FirstOrDefault()?.DriverVersion ?? "N/A"})");
        sb.AppendLine($"TPM 2.0: {masked.Security.Tpm20Status} | Secure Boot: {masked.Security.SecureBootStatus}");
        sb.AppendLine($"Rede: {masked.Network.ConnectionSummary} | Latência: {masked.Network.LatencyDisplay} | Perda: {masked.Network.PacketLossDisplay} | VPN Ativa: {(masked.Network.HasVpnAdapters ? "Sim" : "Não")} | Proxy: {masked.Network.ProxyConfiguration}");
        sb.AppendLine($"Anticheat: EAC={masked.AntiCheat.EasyAntiCheat.StatusDisplay}, EOS={masked.AntiCheat.EasyAntiCheatEos.StatusDisplay}, BattlEye={masked.AntiCheat.BattlEye.StatusDisplay}");
        sb.AppendLine($"Fortnite: {(masked.Fortnite.IsInstalled ? "Instalado" : "Não detectado")} | {masked.Fortnite.MainFilesStatus}");

        var alerts = masked.AllChecks.Where(c => c.Status is DiagnosticStatus.ERRO or DiagnosticStatus.CRITICO or DiagnosticStatus.ATENCAO).ToList();
        if (alerts.Count > 0)
        {
            sb.AppendLine("Itens em Atenção/Erro:");
            foreach (var a in alerts)
            {
                sb.AppendLine($" - [{a.StatusDisplay}] {a.Name}: {a.CurrentValue}");
            }
        }
        sb.AppendLine("(Dados sensíveis como IP, MAC, Serial, UUID e nomes de usuário foram mascarados automaticamente)");

        return PrivacyMasker.SanitizeTextForReport(sb.ToString());
    }

    public async Task<string> ExportReportToFileAsync(
        FullDiagnosticReport report,
        string format,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var dir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HWIDDiagnosticPro", "Relatorios")
            : outputDirectory;

        Directory.CreateDirectory(dir);

        var normFormat = (format ?? "HTML").Trim().ToUpperInvariant();
        var timestamp = report.GeneratedAt.ToString("yyyyMMdd_HHmmss");
        var ext = normFormat switch
        {
            "JSON" => "json",
            "TXT" => "txt",
            "PDF" => "pdf",
            _ => "html"
        };

        var filePath = Path.Combine(dir, $"HWIDDiagnosticPro_Relatorio_{timestamp}.{ext}");

        switch (normFormat)
        {
            case "JSON":
                await File.WriteAllTextAsync(filePath, GenerateJsonReport(report), Encoding.UTF8, cancellationToken);
                break;
            case "TXT":
                await File.WriteAllTextAsync(filePath, GenerateTxtReport(report), Encoding.UTF8, cancellationToken);
                break;
            case "PDF":
                await File.WriteAllBytesAsync(filePath, GeneratePdfReport(report), cancellationToken);
                break;
            default:
                await File.WriteAllTextAsync(filePath, GenerateHtmlReport(report), Encoding.UTF8, cancellationToken);
                break;
        }

        return filePath;
    }

    private static FullDiagnosticReport CreateMaskedCopy(FullDiagnosticReport source)
    {
        // Serializa e desserializa para clonar profundamente e então aplica o mascaramento estrito em todos os campos
        var json = JsonSerializer.Serialize(source, JsonOptions);
        var copy = JsonSerializer.Deserialize<FullDiagnosticReport>(json, JsonOptions) ?? new FullDiagnosticReport();

        copy.Hardware.Motherboard.MaskedSerialNumber = PrivacyMasker.MaskSerialNumber(
            PrivacyMasker.IsValidHardwareIdentifier(copy.Hardware.Motherboard.RawSerialNumber)
                ? copy.Hardware.Motherboard.RawSerialNumber
                : copy.Hardware.Motherboard.MaskedSerialNumber);
        copy.Hardware.Motherboard.RawSerialNumber = "***MASKED***";

        copy.Hardware.Bios.MaskedUuid = PrivacyMasker.MaskUuid(
            PrivacyMasker.IsValidHardwareIdentifier(copy.Hardware.Bios.RawUuid)
                ? copy.Hardware.Bios.RawUuid
                : copy.Hardware.Bios.MaskedUuid);
        copy.Hardware.Bios.RawUuid = "***MASKED***";

        foreach (var mod in copy.Hardware.Ram.Modules)
        {
            mod.MaskedSerialNumber = PrivacyMasker.MaskSerialNumber(mod.MaskedSerialNumber);
        }

        foreach (var disk in copy.Hardware.StorageDevices)
        {
            disk.MaskedSerialNumber = PrivacyMasker.MaskSerialNumber(
                PrivacyMasker.IsValidHardwareIdentifier(disk.RawSerialNumber)
                    ? disk.RawSerialNumber
                    : disk.MaskedSerialNumber);
            disk.RawSerialNumber = "***MASKED***";
        }

        foreach (var adapter in copy.Network.ActiveAdapters)
        {
            adapter.MaskedLocalIpAddress = PrivacyMasker.MaskIpAddress(adapter.LocalIpAddress);
            adapter.LocalIpAddress = adapter.MaskedLocalIpAddress;
            adapter.MaskedMacAddress = PrivacyMasker.MaskMacAddress(adapter.RawMacAddress);
            adapter.RawMacAddress = adapter.MaskedMacAddress;
            adapter.DefaultGateway = PrivacyMasker.MaskIpAddress(adapter.DefaultGateway);
        }

        copy.Fortnite.InstallLocation = PrivacyMasker.MaskUserPath(copy.Fortnite.InstallLocation);

        foreach (var ac in copy.AntiCheat.AllComponents)
        {
            ac.InstallationPath = PrivacyMasker.MaskUserPath(ac.InstallationPath);
            ac.PublicLogErrors = ac.PublicLogErrors.Select(PrivacyMasker.SanitizeTextForReport).ToList();
        }

        foreach (var check in copy.AllChecks)
        {
            check.CurrentValue = PrivacyMasker.SanitizeTextForReport(check.CurrentValue);
            check.TechnicalDetails = PrivacyMasker.SanitizeTextForReport(check.TechnicalDetails);
        }

        foreach (var ev in copy.SystemIntegrity.RelevantEvents)
        {
            ev.Message = PrivacyMasker.SanitizeTextForReport(ev.Message);
        }

        return copy;
    }
}
