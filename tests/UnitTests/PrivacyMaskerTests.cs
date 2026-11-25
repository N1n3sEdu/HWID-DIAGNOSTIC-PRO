using Xunit;
using HWIDDiagnosticPro.Core.Helpers;

namespace HWIDDiagnosticPro.UnitTests;

public sealed class PrivacyMaskerTests
{
    [Theory]
    [InlineData("Default string")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("System Serial Number")]
    [InlineData("Não disponível")]
    [InlineData("")]
    public void MaskSerialNumber_ReturnsNaoDisponivel_ForInvalidOemPlaceholders(string placeholder)
    {
        var masked = PrivacyMasker.MaskSerialNumber(placeholder);
        Assert.Equal("Não disponível", masked);
    }

    [Fact]
    public void MaskSerialNumber_PartiallyMasksValidSerial()
    {
        var masked = PrivacyMasker.MaskSerialNumber("SN987654321ABCD");
        Assert.EndsWith("ABCD", masked);
        Assert.StartsWith("****", masked);
        Assert.DoesNotContain("987654321", masked);
    }

    [Fact]
    public void MaskUuid_PartiallyMasksValidUuid()
    {
        var raw = "12345678-9ABC-DEF0-1234-56789ABCDEF9";
        var masked = PrivacyMasker.MaskUuid(raw);
        Assert.Equal("********-****-****-****-********DEF9", masked);
        Assert.DoesNotContain("12345678", masked);
    }

    [Fact]
    public void MaskIpAddress_MasksIpv4AndIpv6()
    {
        Assert.Equal("192.168.***.***", PrivacyMasker.MaskIpAddress("192.168.15.42"));
        Assert.Equal("fe80:1a2b:****:****", PrivacyMasker.MaskIpAddress("fe80:1a2b:3c4d:5e6f::1"));
    }

    [Fact]
    public void MaskMacAddress_PartiallyMasksMac()
    {
        var masked = PrivacyMasker.MaskMacAddress("A1-B2-C3-D4-E5-F6");
        Assert.Equal("A1:B2:C3:**:**:**", masked);
        Assert.DoesNotContain("D4", masked);
    }

    [Fact]
    public void SanitizeTextForReport_MasksUsernameIpMacUuidEmailAndToken()
    {
        var raw = @"Log path: C:\Users\CarlosEduardo\AppData\Local\FortniteGame\Saved\Logs " +
                  @"IP: 192.168.1.55 MAC: 11:22:33:44:55:66 UUID: 11111111-2222-3333-4444-555555559999 " +
                  @"Email: carlos@example.com token=eyJhbGciOiJIUzI1NiJ9";

        var sanitized = PrivacyMasker.SanitizeTextForReport(raw);

        Assert.Contains(@"C:\Users\***\AppData", sanitized);
        Assert.DoesNotContain("CarlosEduardo", sanitized);
        Assert.Contains("192.168.***.***", sanitized);
        Assert.DoesNotContain("192.168.1.55", sanitized);
        Assert.Contains("11:22:33:**:**:**", sanitized);
        Assert.Contains("********-****-****-****-********9999", sanitized);
        Assert.Contains("***@***.***", sanitized);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", sanitized);
    }
}
