using System.Globalization;
using System.Text;

namespace HWIDDiagnosticPro.Reporting.Helpers;

/// <summary>
/// Gerador nativo de documentos PDF 1.4 multipágina com suporte completo à codificação
/// WinAnsi (português brasileiro com acentuação), cabeçalhos técnicos e quebra automática de linhas.
/// Não depende de bibliotecas C++ externas não gerenciadas.
/// </summary>
public sealed class SimplePdfDocumentWriter
{
    private sealed record PdfLine(string Text, float FontSize, bool IsBold, float R, float G, float B, float SpacingAfter);

    private readonly List<PdfLine> _lines = [];

    static SimplePdfDocumentWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public void AddTitle(string text)
    {
        _lines.Add(new PdfLine(text, 16f, true, 0.23f, 0.51f, 0.96f, 8f));
    }

    public void AddSubtitle(string text)
    {
        _lines.Add(new PdfLine(text, 10f, false, 0.35f, 0.40f, 0.48f, 12f));
    }

    public void AddSectionHeader(string title)
    {
        _lines.Add(new PdfLine(string.Empty, 8f, false, 0f, 0f, 0f, 4f));
        _lines.Add(new PdfLine(title.ToUpperInvariant(), 12f, true, 0.12f, 0.25f, 0.69f, 6f));
    }

    public void AddKeyValue(string key, string? value)
    {
        var safeVal = string.IsNullOrWhiteSpace(value) ? "Não disponível" : value;
        foreach (var wrapped in WrapText($"{key}: {safeVal}", 88))
        {
            _lines.Add(new PdfLine(wrapped, 9.5f, false, 0.10f, 0.12f, 0.16f, 3.5f));
        }
    }

    public void AddCheckRow(string statusText, string category, string name, string value, string recommendation)
    {
        var (r, g, b) = statusText switch
        {
            "NORMAL" => (0.06f, 0.55f, 0.38f),
            "ATENÇÃO" => (0.78f, 0.48f, 0.04f),
            "ERRO" or "CRÍTICO" => (0.82f, 0.15f, 0.15f),
            _ => (0.35f, 0.38f, 0.44f)
        };

        foreach (var wrapped in WrapText($"[{statusText}] [{category}] {name}", 86))
        {
            _lines.Add(new PdfLine(wrapped, 9.5f, true, r, g, b, 2.5f));
        }

        foreach (var wrapped in WrapText($"   Resultado: {value}", 90))
        {
            _lines.Add(new PdfLine(wrapped, 9f, false, 0.15f, 0.18f, 0.22f, 2.5f));
        }

        if (!string.IsNullOrWhiteSpace(recommendation))
        {
            foreach (var wrapped in WrapText($"   Recomendação: {recommendation}", 90))
            {
                _lines.Add(new PdfLine(wrapped, 8.5f, false, 0.28f, 0.33f, 0.41f, 4.5f));
            }
        }
    }

    public void AddParagraph(string text, bool isBold = false)
    {
        foreach (var wrapped in WrapText(text, 90))
        {
            _lines.Add(new PdfLine(wrapped, 9.5f, isBold, 0.15f, 0.18f, 0.22f, 3.5f));
        }
    }

    private static IEnumerable<string> WrapText(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (var rawParagraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = rawParagraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var sb = new StringBuilder();
            foreach (var word in words)
            {
                if (sb.Length + word.Length + 1 > maxCharsPerLine && sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(word);
            }

            if (sb.Length > 0)
                yield return sb.ToString();
        }
    }

    public byte[] BuildPdfBytes()
    {
        // Paginar linhas em páginas A4 (595 x 842 pt)
        const float pageHeight = 842f;
        const float topMargin = 790f;
        const float bottomMargin = 55f;
        const float leftMargin = 45f;

        var pages = new List<List<PdfLine>>();
        var currentPage = new List<PdfLine>();
        float currentY = topMargin;

        foreach (var line in _lines)
        {
            float lineHeight = line.FontSize + line.SpacingAfter;
            if (currentY - lineHeight < bottomMargin && currentPage.Count > 0)
            {
                pages.Add(currentPage);
                currentPage = [];
                currentY = topMargin;
            }

            currentPage.Add(line);
            currentY -= lineHeight;
        }

        if (currentPage.Count > 0 || pages.Count == 0)
        {
            pages.Add(currentPage);
        }

        // Construir estrutura de objetos PDF 1.4:
        // Obj 1: Catalog
        // Obj 2: Pages
        // Obj 3: Font Regular (Helvetica / WinAnsiEncoding)
        // Obj 4: Font Bold (Helvetica-Bold / WinAnsiEncoding)
        // Para cada página i: Obj (5 + i*2) = Page, Obj (6 + i*2) = Content Stream
        using var ms = new MemoryStream();
        var ascii = Encoding.ASCII;
        var winAnsi = Encoding.GetEncoding(1252);

        void WriteAscii(string s)
        {
            var bytes = ascii.GetBytes(s);
            ms.Write(bytes, 0, bytes.Length);
        }

        var offsets = new Dictionary<int, long>();

        WriteAscii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        // Obj 1: Catalog
        offsets[1] = ms.Position;
        WriteAscii("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 3: Helvetica Regular
        offsets[3] = ms.Position;
        WriteAscii("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Obj 4: Helvetica Bold
        offsets[4] = ms.Position;
        WriteAscii("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        var pageObjectIds = new List<int>();

        for (int i = 0; i < pages.Count; i++)
        {
            int pageObjId = 5 + (i * 2);
            int contentObjId = 6 + (i * 2);
            pageObjectIds.Add(pageObjId);

            var streamBytes = BuildPageContentStream(pages[i], i + 1, pages.Count, topMargin, leftMargin, pageHeight, winAnsi);

            offsets[contentObjId] = ms.Position;
            WriteAscii($"{contentObjId} 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
            ms.Write(streamBytes, 0, streamBytes.Length);
            WriteAscii("\nendstream\nendobj\n");

            offsets[pageObjId] = ms.Position;
            WriteAscii($"{pageObjId} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjId} 0 R >>\nendobj\n");
        }

        // Obj 2: Pages
        offsets[2] = ms.Position;
        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        WriteAscii($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageObjectIds.Count} >>\nendobj\n");

        int totalObjects = 4 + (pages.Count * 2);
        long xrefOffset = ms.Position;

        WriteAscii($"xref\n0 {totalObjects + 1}\n");
        WriteAscii("0000000000 65535 f \n");
        for (int objId = 1; objId <= totalObjects; objId++)
        {
            WriteAscii($"{offsets[objId]:D10} 00000 n \n");
        }

        WriteAscii($"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    private static byte[] BuildPageContentStream(
        List<PdfLine> pageLines,
        int pageNumber,
        int totalPages,
        float topMargin,
        float leftMargin,
        float pageHeight,
        Encoding winAnsi)
    {
        using var stream = new MemoryStream();
        var inv = CultureInfo.InvariantCulture;

        void AppendCmd(string cmd)
        {
            var b = winAnsi.GetBytes(cmd);
            stream.Write(b, 0, b.Length);
        }

        // Faixa superior azul institucional
        AppendCmd("0.04 0.06 0.10 rg 0 808 595 34 re f\n");
        AppendCmd("0.23 0.51 0.96 rg 0 805 595 3 re f\n");
        AppendCmd("BT /F2 11 Tf 0.95 0.97 1.00 rg 45 820 Td (HWID DIAGNOSTIC PRO - RELATORIO TECNICO DE DIAGNOSTICO) Tj ET\n");

        float y = topMargin - 10f;
        foreach (var line in pageLines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                string fontTag = line.IsBold ? "/F2" : "/F1";
                string rStr = line.R.ToString("0.00", inv);
                string gStr = line.G.ToString("0.00", inv);
                string bStr = line.B.ToString("0.00", inv);
                string sizeStr = line.FontSize.ToString("0.0", inv);
                string xStr = leftMargin.ToString("0.0", inv);
                string yStr = y.ToString("0.0", inv);
                string escaped = EscapePdfString(line.Text);

                AppendCmd($"BT {fontTag} {sizeStr} Tf {rStr} {gStr} {bStr} rg {xStr} {yStr} Td ({escaped}) Tj ET\n");
            }
            y -= (line.FontSize + line.SpacingAfter);
        }

        // Rodapé com numeração de página e aviso de privacidade
        string footer = $"HWID Diagnostic Pro | Dados sensiveis mascarados automaticamente | Pagina {pageNumber} de {totalPages}";
        AppendCmd($"BT /F1 8 Tf 0.45 0.48 0.54 rg 45 28 Td ({EscapePdfString(footer)}) Tj ET\n");

        return stream.ToArray();
    }

    private static string EscapePdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "")
            .Replace("\n", " ");
    }
}
