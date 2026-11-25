using System.Management;
using System.Runtime.Versioning;

namespace HWIDDiagnosticPro.Infrastructure.Helpers;

/// <summary>
/// Auxiliar seguro para consultas WMI e CIM no Windows com tratamento de exceções e tempo limite.
/// </summary>
public static class WmiQueryHelper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    [SupportedOSPlatform("windows")]
    public static List<Dictionary<string, object?>> Query(
        string wqlQuery,
        string scopeNamespace = @"\\.\root\CIMV2",
        TimeSpan? timeout = null)
    {
        var results = new List<Dictionary<string, object?>>();
        if (!OperatingSystem.IsWindows())
            return results;

        try
        {
            var effectiveTimeout = timeout ?? DefaultTimeout;
            var scope = new ManagementScope(scopeNamespace);
            scope.Connect();

            var options = new System.Management.EnumerationOptions
            {
                Timeout = effectiveTimeout,
                ReturnImmediately = true,
                Rewindable = false
            };

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wqlQuery), options);
            using var collection = searcher.Get();

            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyData prop in obj.Properties)
                    {
                        try
                        {
                            row[prop.Name] = prop.Value;
                        }
                        catch
                        {
                            row[prop.Name] = null;
                        }
                    }
                    results.Add(row);
                }
            }
        }
        catch
        {
            // Retorna lista vazia com segurança quando o namespace WMI requer elevação ou não está presente no hardware
        }

        return results;
    }

    public static string GetString(Dictionary<string, object?> row, string key, string fallback = "Não disponível")
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            return fallback;

        var str = val.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(str) ? fallback : str;
    }

    public static long GetInt64(Dictionary<string, object?> row, string key, long fallback = 0)
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            return fallback;

        try
        {
            return Convert.ToInt64(val);
        }
        catch
        {
            return fallback;
        }
    }

    public static int GetInt32(Dictionary<string, object?> row, string key, int fallback = 0)
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            return fallback;

        try
        {
            return Convert.ToInt32(val);
        }
        catch
        {
            return fallback;
        }
    }

    public static uint GetUInt32(Dictionary<string, object?> row, string key, uint fallback = 0)
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            return fallback;

        try
        {
            return Convert.ToUInt32(val);
        }
        catch
        {
            return fallback;
        }
    }

    public static bool? GetNullableBool(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            return null;

        try
        {
            return Convert.ToBoolean(val);
        }
        catch
        {
            return null;
        }
    }

    public static string FormatWmiDate(string? dmtfDate)
    {
        if (string.IsNullOrWhiteSpace(dmtfDate) || dmtfDate.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
            return "Não disponível";

        try
        {
            if (OperatingSystem.IsWindows() && dmtfDate.Length >= 14)
            {
                var dt = ManagementDateTimeConverter.ToDateTime(dmtfDate);
                return dt.ToString("dd/MM/yyyy");
            }

            if (DateTime.TryParse(dmtfDate, out var parsed))
            {
                return parsed.ToString("dd/MM/yyyy");
            }
        }
        catch
        {
            if (dmtfDate.Length >= 8 &&
                int.TryParse(dmtfDate[..4], out var y) &&
                int.TryParse(dmtfDate.Substring(4, 2), out var m) &&
                int.TryParse(dmtfDate.Substring(6, 2), out var d))
            {
                return $"{d:D2}/{m:D2}/{y:D4}";
            }
        }

        return dmtfDate;
    }
}
