using System.Text.RegularExpressions;

namespace HWIDDiagnosticPro.Core.Helpers;

/// <summary>
/// Utilitário de mascaramento automático de dados sensíveis (Seções 6, 7 e 16).
/// Garante proteção de endereços IP, MAC, números de série, UUID, caminhos de usuário,
/// e-mails, tokens e credenciais em relatórios e exibições.
/// </summary>
public static partial class PrivacyMasker
{
    private static readonly HashSet<string> InvalidOemPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "",
        "Não disponível",
        "N/A",
        "None",
        "Default string",
        "To be filled by O.E.M.",
        "System Serial Number",
        "System Product Name",
        "0",
        "00000000",
        "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
        "00000000-0000-0000-0000-000000000000"
    };

    public static bool IsValidHardwareIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return !InvalidOemPlaceholders.Contains(trimmed);
    }

    public static string MaskSerialNumber(string? serial)
    {
        if (!IsValidHardwareIdentifier(serial))
            return "Não disponível";

        var clean = serial!.Trim();
        if (clean.Length <= 4)
            return new string('*', clean.Length);

        var visibleTail = clean[^4..];
        var maskedPrefixLength = Math.Min(clean.Length - 4, 8);
        return $"{new string('*', maskedPrefixLength)}{visibleTail}";
    }

    public static string MaskUuid(string? uuid)
    {
        if (!IsValidHardwareIdentifier(uuid))
            return "Não disponível";

        var clean = uuid!.Trim();
        if (clean.Length < 8)
            return "********";

        var tail = clean[^4..];
        return $"********-****-****-****-********{tail}";
    }

    public static string MaskIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
            return "Não disponível";

        var clean = ip.Trim();

        // IPv4
        var parts = clean.Split('.');
        if (parts.Length == 4)
        {
            return $"{parts[0]}.{parts[1]}.***.***";
        }

        // IPv6
        if (clean.Contains(':'))
        {
            var v6Parts = clean.Split(':');
            if (v6Parts.Length >= 2)
                return $"{v6Parts[0]}:{v6Parts[1]}:****:****";
        }

        return "***.***.***.***";
    }

    public static string MaskMacAddress(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac) || mac.Equals("Não disponível", StringComparison.OrdinalIgnoreCase))
            return "Não disponível";

        var clean = mac.Trim().ToUpperInvariant().Replace("-", ":");
        var hexOnly = clean.Replace(":", "");
        if (hexOnly.Length == 12)
        {
            return $"{hexOnly[..2]}:{hexOnly[2..4]}:{hexOnly[4..6]}:**:**:**";
        }

        return "**:**:**:**:**:**";
    }

    public static string MaskUserPath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = Regex.Replace(
            text,
            @"([A-Za-z]:\\Users\\)([^\\/:*?""<>|\r\n]+)",
            "$1***",
            RegexOptions.IgnoreCase);

        result = Regex.Replace(
            result,
            @"(/home/)([^/\r\n]+)",
            "$1***",
            RegexOptions.IgnoreCase);

        return result;
    }

    public static string SanitizeTextForReport(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var masked = MaskUserPath(text);

        // Mascarar IPv4 no meio do texto
        masked = Regex.Replace(
            masked,
            @"\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\b",
            match =>
            {
                // Preservar versões óbvias quando precedidas por 'v' ou similar não capturado aqui, mas por segurança mascarar IPs de rede
                var first = int.TryParse(match.Groups[1].Value, out var o1) ? o1 : -1;
                if (first is 10 or 127 or 172 or 192 or 169 or 100)
                {
                    return $"{match.Groups[1].Value}.{match.Groups[2].Value}.***.***";
                }
                return match.Value;
            });

        // Mascarar endereços MAC no texto
        masked = Regex.Replace(
            masked,
            @"\b([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})\b",
            match => MaskMacAddress(match.Value));

        // Mascarar UUIDs / GUIDs no texto
        masked = Regex.Replace(
            masked,
            @"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-([0-9A-Fa-f]{12})\b",
            match => $"********-****-****-****-********{match.Groups[1].Value[^4..]}");

        // Mascarar e-mails
        masked = Regex.Replace(
            masked,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
            "***@***.***");

        // Mascarar tokens, bearer, authorization, password, secret, apikey
        masked = Regex.Replace(
            masked,
            @"(?i)\b(bearer|token|access_token|refresh_token|password|senha|secret|apikey|api_key|auth|exchange_code)\s*([:=])\s*([^\s""'&;]+)",
            "$1$2***MASKED***");

        return masked;
    }
}
