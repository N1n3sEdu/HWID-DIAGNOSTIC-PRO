using System.Text;
using Xunit;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Infrastructure.AntiCheat;
using HWIDDiagnosticPro.Infrastructure.Diagnostics;
using HWIDDiagnosticPro.Infrastructure.Hardware;
using HWIDDiagnosticPro.Infrastructure.Network;
using HWIDDiagnosticPro.Infrastructure.Security;
using HWIDDiagnosticPro.Infrastructure.Services;
using HWIDDiagnosticPro.Reporting;

namespace HWIDDiagnosticPro.IntegrationTests;

public sealed class DiagnosticAndReportIntegrationTests
{
    [Fact]
    public async Task OrchestratorAndReportExporter_RunFullCycleAndMaskSensitiveDataInAllFormats()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"HWIDDiagTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var logger = new LocalLoggerService(tempDir);
            var storage = new JsonSettingsStorageService(logger, tempDir);
            var hw = new HardwareDiagnosticService(logger);
            var net = new NetworkDiagnosticService(logger);
            var sec = new WindowsSecurityDiagnosticService(logger);
            var drv = new DriversAndServicesDiagnosticService(logger);
            var ac = new AntiCheatDiagnosticService(logger);
            var fn = new FortniteDiagnosticService(logger);
            var sys = new SystemIntegrityDiagnosticService(logger);

            var orchestrator = new DiagnosticOrchestratorService(hw, net, sec, drv, ac, fn, sys, storage, logger);
            var report = await orchestrator.RunFullDiagnosticAsync();

            Assert.NotNull(report);
            Assert.InRange(report.SystemHealthScore, 0, 100);
            Assert.NotEmpty(report.RecommendedSolutions);

            // Injetar dados sensíveis de teste para garantir que todos os 4 formatos mascarem automaticamente
            report.Hardware.Motherboard.RawSerialNumber = "MB-SECRET-99887766";
            report.Hardware.Bios.RawUuid = "ABCDEF12-3456-7890-ABCD-EF1234567890";
            report.Fortnite.InstallLocation = @"C:\Users\JoaoJogadorSecreto\Games\Fortnite";
            report.Network.ActiveAdapters.Add(new NetworkAdapterDetail
            {
                Name = "Ethernet0",
                LocalIpAddress = "192.168.50.123",
                RawMacAddress = "AA:BB:CC:11:22:33",
                DefaultGateway = "192.168.50.1"
            });

            var exporter = new ReportExportService();

            var jsonPath = await exporter.ExportReportToFileAsync(report, "JSON", tempDir);
            var txtPath = await exporter.ExportReportToFileAsync(report, "TXT", tempDir);
            var htmlPath = await exporter.ExportReportToFileAsync(report, "HTML", tempDir);
            var pdfPath = await exporter.ExportReportToFileAsync(report, "PDF", tempDir);

            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(txtPath));
            Assert.True(File.Exists(htmlPath));
            Assert.True(File.Exists(pdfPath));

            var jsonText = await File.ReadAllTextAsync(jsonPath);
            var txtText = await File.ReadAllTextAsync(txtPath);
            var htmlText = await File.ReadAllTextAsync(htmlPath);
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            var summaryText = exporter.GenerateExecutiveSupportSummary(report);

            // Verificar que dados brutos sensíveis não vazam em nenhum formato
            foreach (var content in new[] { jsonText, txtText, htmlText, summaryText })
            {
                Assert.DoesNotContain("MB-SECRET-99887", content);
                Assert.DoesNotContain("ABCDEF12-3456", content);
                Assert.DoesNotContain("JoaoJogadorSecreto", content);
                Assert.DoesNotContain("192.168.50.123", content);
                Assert.DoesNotContain("AA:BB:CC:11:22:33", content);
            }

            // Verificar cabeçalho e rodapé PDF 1.4 válidos
            var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 8);
            var pdfTail = Encoding.ASCII.GetString(pdfBytes, Math.Max(0, pdfBytes.Length - 32), Math.Min(32, pdfBytes.Length));
            Assert.StartsWith("%PDF-1.4", pdfHeader);
            Assert.Contains("%%EOF", pdfTail);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
