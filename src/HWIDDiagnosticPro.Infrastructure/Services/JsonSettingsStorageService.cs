using System.Text.Json;
using System.Text.Json.Serialization;
using HWIDDiagnosticPro.Core.Models;
using HWIDDiagnosticPro.Core.Services;

namespace HWIDDiagnosticPro.Infrastructure.Services;

public sealed class JsonSettingsStorageService : ISettingsStorageService
{
    private readonly string _storageDir;
    private readonly string _settingsFilePath;
    private readonly string _lastReportFilePath;
    private readonly ILocalLoggerService _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonSettingsStorageService(ILocalLoggerService logger, string? customStorageDirectory = null)
    {
        _logger = logger;
        _storageDir = customStorageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HWIDDiagnosticPro");

        try
        {
            Directory.CreateDirectory(_storageDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Armazenamento", $"Não foi possível criar diretório padrão: {ex.Message}");
        }

        _settingsFilePath = Path.Combine(_storageDir, "settings.json");
        _lastReportFilePath = Path.Combine(_storageDir, "last_diagnostic_report.json");
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    if (string.IsNullOrWhiteSpace(settings.ReportsOutputDirectory))
                    {
                        settings.ReportsOutputDirectory = ResolveDefaultReportsDirectory();
                    }
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Armazenamento", "Falha ao carregar configurações locais JSON.", ex);
        }

        return new AppSettings
        {
            ReportsOutputDirectory = ResolveDefaultReportsDirectory()
        };
    }

    private string ResolveDefaultReportsDirectory()
    {
        try
        {
            var portableReportsDir = Path.Combine(AppContext.BaseDirectory, "Relatorios");
            Directory.CreateDirectory(portableReportsDir);
            return portableReportsDir;
        }
        catch
        {
            return Path.Combine(_storageDir, "Relatorios");
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_storageDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Armazenamento", "Falha ao salvar configurações locais JSON.", ex);
        }
    }

    public async Task SaveLastReportAsync(FullDiagnosticReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_storageDir);
            var json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(_lastReportFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Armazenamento", "Falha ao salvar histórico do último diagnóstico em JSON.", ex);
        }
    }

    public async Task<FullDiagnosticReport?> LoadLastReportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_lastReportFilePath))
            {
                var json = await File.ReadAllTextAsync(_lastReportFilePath, cancellationToken);
                return JsonSerializer.Deserialize<FullDiagnosticReport>(json, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Armazenamento", "Falha ao ler histórico do último diagnóstico em JSON.", ex);
        }

        return null;
    }
}
