using System.Globalization;
using System.Text;

namespace RentalHub.API.Services;

public static class SimplePdfBuilder
{
    private const int LinesPerPage = 44;
    private const int MaxLineLength = 105;

    public static byte[] CreateReport(SimplePdfReport report)
    {
        var lines = new List<string>
        {
            "RentalHub",
            report.Title,
            report.Subtitle,
            $"Emitido em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            new string('=', MaxLineLength),
            string.Empty
        };

        if (report.Summary.Count > 0)
        {
            lines.Add("RESUMO");
            lines.Add(new string('-', MaxLineLength));
            lines.AddRange(report.Summary.Select(FormatSummaryLine));
            lines.Add(string.Empty);
        }

        foreach (var table in report.Tables)
        {
            lines.Add(table.Title.ToUpperInvariant());
            lines.Add(new string('-', MaxLineLength));
            lines.AddRange(CreateTableLines(table));
            lines.Add(string.Empty);
        }

        if (report.Notes.Count > 0)
        {
            lines.Add("OBSERVACOES");
            lines.Add(new string('-', MaxLineLength));
            lines.AddRange(report.Notes);
        }

        return Create(lines);
    }

    public static byte[] Create(IReadOnlyCollection<string> rawLines)
    {
        var lines = rawLines.SelectMany(WrapLine).ToList();
        var pages = lines.Chunk(LinesPerPage).Select(chunk => chunk.ToArray()).ToList();
        if (pages.Count == 0)
        {
            pages.Add([]);
        }

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var pageObjectIds = new List<int>();
        foreach (var pageLines in pages)
        {
            var pageObjectId = objects.Count + 1;
            var contentObjectId = pageObjectId + 1;
            pageObjectIds.Add(pageObjectId);

            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>");
            objects.Add(CreateContentObject(pageLines));
        }

        objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>";

        var builder = new StringBuilder();
        var offsets = new List<int> { 0 };
        builder.Append("%PDF-1.4\n");

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n")
                .Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n")
            .Append("0 ").Append(objects.Count + 1).Append('\n')
            .Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        builder.Append("trailer\n")
            .Append("<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n")
            .Append("startxref\n")
            .Append(xrefOffset).Append('\n')
            .Append("%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string CreateContentObject(IReadOnlyCollection<string> lines)
    {
        var content = new StringBuilder();
        var y = 800;
        foreach (var line in lines)
        {
            var fontSize = y == 800 ? 14 : 9;
            content.Append("BT /F1 ").Append(fontSize).Append(" Tf 42 ")
                .Append(y.ToString(CultureInfo.InvariantCulture))
                .Append(" Td (").Append(EscapePdfText(line)).Append(") Tj ET\n");
            y -= y == 800 ? 22 : 15;
        }

        var stream = content.ToString();
        return $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream";
    }

    private static IEnumerable<string> WrapLine(string line)
    {
        var text = ToAscii(line);
        if (text.Length <= MaxLineLength)
        {
            yield return text;
            yield break;
        }

        for (var index = 0; index < text.Length; index += MaxLineLength)
        {
            yield return text.Substring(index, Math.Min(MaxLineLength, text.Length - index));
        }
    }

    private static string ToAscii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character <= 127 ? character : '?');
        }

        return builder.ToString();
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string FormatSummaryLine(SimplePdfSummaryItem item)
    {
        const int labelLength = 34;
        var label = NormalizeCell(item.Label, labelLength);
        return $"{label.PadRight(labelLength, '.')} {item.Value}";
    }

    private static IReadOnlyCollection<string> CreateTableLines(SimplePdfTable table)
    {
        if (table.Rows.Count == 0)
        {
            return ["Nenhum registro encontrado para os filtros selecionados."];
        }

        var columnCount = table.Headers.Count;
        if (columnCount == 0)
        {
            return table.Rows.Select(row => string.Join(" | ", row)).ToList();
        }

        var separatorWidth = Math.Max(0, (columnCount - 1) * 3);
        var availableWidth = Math.Max(columnCount * 8, MaxLineLength - separatorWidth);
        var baseWidth = Math.Max(8, availableWidth / columnCount);
        var widths = table.Headers.Select(header => Math.Min(24, Math.Max(baseWidth, Math.Min(18, header.Length + 4)))).ToArray();
        var currentWidth = widths.Sum() + separatorWidth;

        while (currentWidth > MaxLineLength)
        {
            var largestIndex = Array.IndexOf(widths, widths.Max());
            if (widths[largestIndex] <= 8)
            {
                break;
            }

            widths[largestIndex]--;
            currentWidth = widths.Sum() + separatorWidth;
        }

        var lines = new List<string>
        {
            FormatColumns(table.Headers, widths),
            FormatColumns(widths.Select(width => new string('-', width)).ToArray(), widths)
        };

        lines.AddRange(table.Rows.Select(row => FormatColumns(row, widths)));
        return lines;
    }

    private static string FormatColumns(IReadOnlyCollection<string> values, IReadOnlyList<int> widths)
    {
        return string.Join(" | ", values
            .Select((value, index) => NormalizeCell(value, widths[Math.Min(index, widths.Count - 1)]).PadRight(widths[Math.Min(index, widths.Count - 1)]))
            .Take(widths.Count));
    }

    private static string NormalizeCell(string? value, int width)
    {
        var normalized = ToAscii(value ?? string.Empty).ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= width)
        {
            return normalized;
        }

        return width <= 3 ? normalized[..width] : string.Concat(normalized.AsSpan(0, width - 3), "...");
    }
}

public sealed record SimplePdfReport(
    string Title,
    string Subtitle,
    IReadOnlyCollection<SimplePdfSummaryItem> Summary,
    IReadOnlyCollection<SimplePdfTable> Tables,
    IReadOnlyCollection<string> Notes);

public sealed record SimplePdfSummaryItem(string Label, string Value);

public sealed record SimplePdfTable(
    string Title,
    IReadOnlyCollection<string> Headers,
    IReadOnlyCollection<IReadOnlyCollection<string>> Rows);
